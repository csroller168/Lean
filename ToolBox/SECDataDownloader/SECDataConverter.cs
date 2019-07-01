﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Newtonsoft.Json;
using QuantConnect.Data.Custom.SEC;
using QuantConnect.Logging;
using QuantConnect.Util;
using Formatting = Newtonsoft.Json.Formatting;

namespace QuantConnect.ToolBox.SECDataDownloader
{
    /// <summary>
    /// Converts SEC data from raw format (sourced from https://www.sec.gov/Archives/edgar/feed/)
    /// to a format usable by LEAN. We do not do any XBRL parsing of the data. We only process
    /// the metadata of the data so that it can be loaded onto LEAN. The parsing of the data is a task
    /// left to the consumer of the data.
    /// </summary>
    public class SECDataConverter
    {
        private DirectoryInfo _tickerFolder;

        /// <summary>
        /// Raw data source path
        /// </summary>
        public string RawSource;

        /// <summary>
        /// Destination of formatted data
        /// </summary>
        public string Destination;

        /// <summary>
        /// Assets keyed by CIK used to resolve underlying ticker 
        /// </summary>
        public readonly Dictionary<string, List<string>> CikTicker = new Dictionary<string, List<string>>();

        /// <summary>
        /// Keyed by CIK, keyed by accession number, contains the publication date for a report
        /// </summary>
        public ConcurrentDictionary<string, Dictionary<string, DateTime>> PublicationDates = new ConcurrentDictionary<string, Dictionary<string, DateTime>>();

        /// <summary>
        /// Keyed by ticker (CIK if ticker not found); contains SEC report(s) that we pass to <see cref="WriteReport"/>
        /// </summary>
        public ConcurrentDictionary<string, ConcurrentDictionary<DateTime, List<ISECReport>>> Reports = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, List<ISECReport>>>();
        
        /// <summary>
        /// Public constructor creates CIK -> Ticker list from various sources
        /// </summary>
        /// <param name="rawSource">Source of raw data</param>
        /// <param name="destination">Destination of formatted data</param>
        /// <param name="tickerFolder">Known ticker folder</param>
        public SECDataConverter(string rawSource, string destination, string tickerFolder)
        {
            RawSource = rawSource;
            Destination = destination;
            
            _tickerFolder = new DirectoryInfo(tickerFolder);
        }

        /// <summary>
        /// Converts the data from raw format (*.nz.tar.gz) to json files consumable by LEAN
        /// </summary>
        /// <param name="startDate">Starting date to start process files</param>
        /// <param name="endDate">Ending date to stop processing files</param>
        public void Process(DateTime processingDate)
        {
            if (!_tickerFolder.Exists)
            {
                throw new DirectoryNotFoundException("Known ticker folder does not exist.");
            }

            // Process data into dictionary of CIK -> List{T} of tickers
            foreach (var line in File.ReadLines(Path.Combine(RawSource, "cik-ticker-mappings.txt")))
            {
                var tickerCik = line.Split('\t');

                // tickerCik[0] = symbol, tickerCik[1] = CIK
                // Note that SEC tickers come in lowercase, so we don't have to alter the ticker
                var cikFormatted = tickerCik[1].PadLeft(10, '0');

                List<string> symbol;
                if (!CikTicker.TryGetValue(cikFormatted, out symbol))
                {
                    symbol = new List<string>();
                    CikTicker[cikFormatted] = symbol;
                }

                symbol.Add(tickerCik[0]);
            }

            // Merge both data sources to a single CIK -> List{T} of tickers
            foreach (var line in File.ReadLines(Path.Combine(RawSource, "cik-ticker-mappings-rankandfile.txt")))
            {
                var tickerInfo = line.Split('|');

                var companyCik = tickerInfo[0].PadLeft(10, '0');
                var companyTicker = tickerInfo[1].ToLower();

                List<string> symbol;
                if (!CikTicker.TryGetValue(companyCik, out symbol))
                {
                    symbol = new List<string>() { companyTicker };
                    CikTicker[companyCik] = symbol;
                }
                else if (!symbol.Contains(companyTicker))
                {
                    symbol.Add(companyTicker);
                }
            }

            var formattedDate = processingDate.ToString(DateFormat.EightCharacter);
            var remoteRawData = new FileInfo(Path.Combine(RawSource, $"{formattedDate}.nc.tar.gz"));
            if (!remoteRawData.Exists)
            {
                throw new Exception($"SECDataConverter.Process(): Raw data {remoteRawData} not found. No process can be done.");
            }

            Log.Trace($"SECDataConverter.Process(): Copying raw data locally...");

            var localRawData = remoteRawData.CopyTo(Path.Combine(Path.GetTempPath(), remoteRawData.Name));
            var extractDataPath = Path.Combine(Path.GetTempPath(), formattedDate);

            Log.Trace($"SECDataConverter.Process(): Extract raw data...");

            using (var data = localRawData.OpenRead())
            {
                using (var archive = TarArchive.CreateInputTarArchive(new GZipInputStream(data)))
                {
                    Directory.CreateDirectory(extractDataPath);
                    archive.ExtractContents(extractDataPath);

                    Log.Trace($"SECDataConverter.Process(): Extracted SEC data to path {extractDataPath}");
                }
            }
            
            // Create known ticker list from the data folder on disk used by LEAN
            var tickerList = _tickerFolder.EnumerateDirectories().AsParallel()
                .Where(d => d.EnumerateFiles($"{formattedDate}*").Any())
                .Select(d => d.Name)
                .ToHashSet();

            Log.Trace($"SECDataConverter.Process(): Start processing..."); 
            // For the meantime, let's only process .nc files, and deal with correction files later.
            Parallel.ForEach(
                Directory.GetFiles(extractDataPath, "*.nc", SearchOption.AllDirectories),
                rawReportFilePath =>
                {
                    var factory = new SECReportFactory();
                    var xmlText = new StringBuilder();

                    // We need to escape any nested XML to ensure our deserialization happens smoothly
                    var parsingText = false;

                    foreach (var line in File.ReadLines(rawReportFilePath))
                    {
                        var newTextLine = line;
                        var currentTagName = GetTagNameFromLine(newTextLine);

                        // This tag is present rarely in SEC reports, but is unclosed without value when encountered.
                        // Verified by searching with ripgrep for "CONFIRMING-COPY"
                        if (currentTagName == "CONFIRMING-COPY")
                        {
                            continue;
                        }

                        // Indicates that the form is a paper submission and that the current file has no contents
                        if (currentTagName == "PAPER")
                        {
                            continue;
                        }

                        // Don't encode the closing tag
                        if (currentTagName == "/TEXT")
                        {
                            parsingText = false;
                        }

                        // To ensure that we can serialize/deserialize data with hours, minutes, seconds
                        if (currentTagName == "FILING-DATE" || currentTagName == "PERIOD" ||
                            currentTagName == "DATE-OF-FILING-CHANGE" || currentTagName == "DATE-CHANGED")
                        {
                            newTextLine = $"{newTextLine.TrimEnd()} 00:00:00";
                        }

                        // Encode all contents inside tags to prevent errors in XML parsing.
                        // The json deserializer will convert these values back to their original form
                        if (!parsingText && HasValue(newTextLine))
                        {
                            newTextLine =
                                $"<{currentTagName}>{SecurityElement.Escape(GetTagValueFromLine(newTextLine))}</{currentTagName}>";
                        }
                        // Escape all contents inside TEXT tags
                        else if (parsingText)
                        {
                            newTextLine = SecurityElement.Escape(newTextLine);
                        }

                        // Don't encode the opening tag
                        if (currentTagName == "TEXT")
                        {
                            parsingText = true;
                        }

                        xmlText.AppendLine(newTextLine);
                    }

                    ISECReport report;
                    try
                    {
                        report = factory.CreateSECReport(xmlText.ToString());
                    }
                    // Ignore unsupported form types for now
                    catch (DataException)
                    {
                        return;
                    }
                    catch (XmlException e)
                    {
                        Log.Error(e, $"SECDataConverter.Process(): Failed to parse XML from file path: {rawReportFilePath}");
                        return;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "SECDataConverter.Process(): Unknown error encountered");
                        return;
                    }

                    // First filer listed in SEC report is usually the company listed on stock exchanges
                    var companyCik = report.Report.Filers.First().CompanyData.Cik;

                    // Some companies can operate under two tickers, but have the same CIK.
                    // Don't bother continuing if we don't find any tickers for the given CIK
                    List<string> tickers;
                    if (!CikTicker.TryGetValue(companyCik, out tickers))
                    {
                        return;
                    }
                    if (!File.Exists(Path.Combine(RawSource, "indexes", $"{companyCik}.json")))
                    {
                        Log.Error($"SECDataConverter.Process(): Failed to find index file for ticker {tickers.FirstOrDefault()} with CIK: {companyCik}");
                        return;
                    }

                    try
                    {
                        // The index file can potentially be corrupted
                        GetPublicationDate(report, companyCik);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, $"SECDataConverter.Process(): {report.Report.FilingDate:yyyy-MM-dd} - Index file lookup failed for ticker: {tickers.FirstOrDefault()} with CIK: {companyCik}");
                    }

                    // Default to company CIK if no known ticker is found.
                    // If we don't find a known equity in our list, the equity is probably not worth our time
                    foreach (var ticker in tickers.Where(tickerList.Contains))
                    {
                        var tickerReports = Reports.GetOrAdd(
                            ticker,
                            _ => new ConcurrentDictionary<DateTime, List<ISECReport>>()
                        );
                        var reports = tickerReports.GetOrAdd(
                            report.Report.FilingDate.Date,
                            _ => new List<ISECReport>()
                        );

                        reports.Add(report);
                    }
                }
            );

            Parallel.ForEach(
                Reports.Keys,
                ticker =>
                {
                    List<ISECReport> reports;
                    if (!Reports[ticker].TryRemove(processingDate, out reports))
                    {
                        return;
                    }

                    WriteReport(reports, ticker);
                }
            );

            // This will clean up after ourselves without having to pay
            // the expense of deleting every single file inside the raw_data folder
            Directory.Delete(extractDataPath, true);
        }


        /// <summary>
        /// Writes the report to disk, where it will be used by LEAN.
        /// If a ticker is not found, the company being reported
        /// will be stored with its CIK value as the ticker.
        ///
        /// Any existing duplicate files will be overwritten.
        /// </summary>
        /// <param name="reports">List of SEC Report objects</param>
        /// <param name="ticker">Symbol ticker</param>
        /// <param name="destination">Folder to write the reports to</param>
        public void WriteReport(List<ISECReport> reports, string ticker)
        {
            var report = reports.First();
            var reportPath = Path.Combine(Destination, ticker.ToLower(), $"{report.Report.FilingDate:yyyyMMdd}");
            var formTypeNormalized = report.Report.FormType.Replace("-", "");
            var reportFilePath = $"{reportPath}_{formTypeNormalized}";
            var reportFile = Path.Combine(reportFilePath, $"{formTypeNormalized}.json");

            Directory.CreateDirectory(reportFilePath);

            var reportSubmissions = reports.Select(r => r.Report);

            using (var writer = new StreamWriter(reportFile, false))
            {
                writer.Write(JsonConvert.SerializeObject(reportSubmissions, new JsonSerializerSettings()
                {
                    Formatting = Formatting.None,
                    NullValueHandling = NullValueHandling.Ignore
                }));
            }

            Compression.ZipDirectory(reportFilePath, $"{reportFilePath}.zip", false);
            Directory.Delete(reportFilePath, true);
        }

        /// <summary>
        /// Takes instance of <see cref="ISECReport"/> and gets publication date information for the given equity, then mutates the instance. 
        /// </summary>
        /// <param name="report">SEC report <see cref="BaseData"/> instance</param>
        /// <param name="companyCik">Company CIK to use to lookup filings for</param>
        /// <remarks>This method caches the results on a per company basis (by CIK), so subsequent lookups for the publication date of the same equity by CIK will be fast</remarks>
        public void GetPublicationDate(ISECReport report, string companyCik)
        {
            Dictionary<string, DateTime> companyPublicationDates;
            if (!PublicationDates.TryGetValue(companyCik, out companyPublicationDates))
            {
                PublicationDates.TryAdd(companyCik, GetReportPublicationTimes(companyCik));
                companyPublicationDates = PublicationDates[companyCik];
            }

            DateTime reportPublicationDate;
            if (companyPublicationDates.TryGetValue(report.Report.AccessionNumber.Replace("-", ""), out reportPublicationDate))
            {
                // Update the filing date to reflect SEC's publication date on their servers
                report.Report.MadeAvailableAt = reportPublicationDate;
            }
        }

        /// <summary>
        /// Gets company CIK values keyed by accession number
        /// </summary>
        /// <param name="cik">Company CIK</param>
        /// <returns><see cref="Dictionary{TKey,TValue}"/> keyed by accession number containing publication date of SEC reports</returns>
        private Dictionary<string, DateTime> GetReportPublicationTimes(string cik)
        {
            var index = JsonConvert.DeserializeObject<SECReportIndexFile>(File.ReadAllText(Path.Combine(RawSource, "indexes", $"{cik}.json")))
                .Directory;

            // Sometimes, SEC folders results are duplicated. We check for duplicates
            // before creating a dictionary to avoid a duplicate key error.
            return index.Items
                .Where(publication => publication.FileType == "folder.gif")
                .DistinctBy(publication => publication.Name)
                .ToDictionary(publication => publication.Name, publication => publication.LastModified);
        }

        /// <summary>
        /// Determines if the given line has a value associated with the tag
        /// </summary>
        /// <param name="line">Line of text from SEC report</param>
        /// <returns>Boolean indicating whether the line contains a value</returns>
        public static bool HasValue(string line)
        {
            var tagEnd = line.IndexOf(">", StringComparison.Ordinal);

            if (!line.StartsWith("<") || tagEnd == -1)
            {
                return false;
            }

            return line.Length > tagEnd + 1;
        }

        /// <summary>
        /// Gets the line's value (if there is one)
        /// </summary>
        /// <param name="line">Line of text from SEC report</param>
        /// <returns>Value associated with the tag</returns>
        public static string GetTagValueFromLine(string line)
        {
            return line.Substring(line.IndexOf(">", StringComparison.Ordinal) + 1);
        }

        /// <summary>
        /// Gets the tag name from a given line
        /// </summary>
        /// <param name="line">Line of text from SEC report</param>
        /// <returns>Tag name from the line</returns>
        public static string GetTagNameFromLine(string line)
        {
            var start = line.IndexOf("<", StringComparison.Ordinal) + 1;
            var length = line.IndexOf(">", StringComparison.Ordinal) - start;

            if (start == -1 || length <= 0)
            {
                return string.Empty;
            }

            return line.Substring(start, length);
        }
    }
}


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

using QuantConnect.Logging;
using System;
using System.Linq;
using System.IO;
using System.Text;
using QuantConnect.Data.Custom.SmartInsider;
using QuantConnect.Util;
using QuantConnect.Interfaces;
using QuantConnect.Configuration;
using QuantConnect.Data.Auxiliary;
using System.Collections.Generic;

namespace QuantConnect.ToolBox.SmartInsider
{
    public class SmartInsiderConverter
    {
        private readonly DirectoryInfo _sourceDirectory;
        private readonly DirectoryInfo _destinationDirectory;
        private readonly MapFileResolver _mapFileResolver;

        /// <summary>
        /// Creates an instance of the converter
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="destinationFile"></param>
        public SmartInsiderConverter(DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory)
        {
            _sourceDirectory = sourceDirectory;
            _destinationDirectory = destinationDirectory;
            _mapFileResolver = Composer.Instance.GetExportedValueByTypeName<IMapFileProvider>(Config.Get("map-file-provider", "LocalDiskMapFileProvider"))
                .Get(Market.USA);

            Directory.CreateDirectory(Path.Combine(_destinationDirectory.FullName, "intentions"));
            Directory.CreateDirectory(Path.Combine(_destinationDirectory.FullName, "transactions"));
        }

        /// <summary>
        /// Converts raw data from Smart Insider
        /// </summary>
        /// <param name="date">Date to process</param>
        /// <returns>Boolean value indicating success status</returns>
        public bool Convert(DateTime date)
        {
            try
            {
                Log.Trace($"SmartInsiderConverter.Convert(): Begin converting {_sourceDirectory.FullName}");

                var rawIntentionsFile = new FileInfo(Path.Combine(_sourceDirectory.FullName, "intentions", $"{date:yyyyMMdd}.ttx"));
                var rawTransactionsFile = new FileInfo(Path.Combine(_sourceDirectory.FullName, "transactions", $"{date:yyyyMMdd}.ttx"));

                var intentionsDirectory = new DirectoryInfo(Path.Combine(_destinationDirectory.FullName, "intentions"));
                var transactionsDirectory = new DirectoryInfo(Path.Combine(_destinationDirectory.FullName, "transactions"));

                if (rawIntentionsFile.Exists)
                {
                    var data = Process<SmartInsiderIntention>(rawIntentionsFile);
                    if (!data.Any())
                    {
                        Log.Trace("SmartInsiderConverter.Convert(): Intentions file contains no data to write");
                    }

                    // We can call this method with no data available because it will do nothing without any data
                    WriteToFile(intentionsDirectory, data);
                }
                else
                {
                    Log.Error($"SmartInsiderConverter.Convert(): Raw intentions file does not exist: {rawIntentionsFile.FullName}");
                }

                if (rawTransactionsFile.Exists)
                {
                    var data = Process<SmartInsiderTransaction>(rawTransactionsFile);
                    if (!data.Any())
                    {
                        Log.Trace("SmartInsiderConverter.Convert(): Transactions file contains no data to write");
                    }

                    // We can call this method with no data available because it will do nothing without any data
                    WriteToFile(transactionsDirectory, data);
                }
                else
                {
                    Log.Error($"SmartInsiderConverter.Convert(): Raw transactions file does not exist: {rawTransactionsFile.FullName}");
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "SmartInsiderConverter.Convert(): Failed to parse raw SmartInsider data");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Processes the data
        /// </summary>
        /// <typeparam name="T"><see cref="SmartInsiderEvent"/> inherited instance</typeparam>
        /// <param name="sourceFile">File to read data from</param>
        /// <returns>Dictionary keyed by ticker that contains all the lines that appeared in the file</returns>
        private Dictionary<string, List<T>> Process<T>(FileInfo sourceFile)
            where T : SmartInsiderEvent, new()
        {
            var previousMarket = string.Empty;
            var previousTicker = string.Empty;
            var lines = new Dictionary<string, List<T>>();
            var i = 0;

            Log.Trace($"SmartInsiderConverter.Process(): Processing file: {sourceFile.FullName}");

            foreach (var line in File.ReadLines(sourceFile.FullName))
            {
                i++;

                // First line is the header row, but make sure we don't encounter it anywhere else in the data
                if (line.StartsWith("\"TransactionID"))
                {
                    Log.Trace($"SmartInsiderConverter.Process(): Header row on line {i}. Skipping...");
                    continue;
                }

                try
                {
                    // Yes, there are ONE HUNDRED total fields in this dataset.
                    // However, we will only take the first 60 since the rest are reserved fields
                    var csv = line.Split('\t')
                        .Take(60)
                        .Select(x => x.Replace("\"", ""))
                        .ToList();

                    // If we have a null value on a non-nullable field, consider it invalid data
                    if (string.IsNullOrWhiteSpace(csv[2]))
                    {
                        Log.Trace($"SmartInsiderConverter.Process(): Null value encountered on non-nullable value on line {i}");
                        continue;
                    }

                    // Remove in descending order to maintain index order
                    // while we delete lower indexed values
                    csv.RemoveAt(46); // ShowOriginal
                    csv.RemoveAt(36); // PreviousClosePrice
                    csv.RemoveAt(14); // ShortCompanyName
                    csv.RemoveAt(7);  // CompanyPageURL

                    var finalLine = string.Join("\t", csv);

                    var dataInstance = new T();
                    dataInstance.FromRawCsv(finalLine);

                    var ticker = dataInstance.TickerSymbol;

                    // For now, only support US markets
                    if (dataInstance.TickerCountry != "US")
                    {
                        if (dataInstance.TickerCountry != previousMarket && ticker != previousTicker)
                        {
                            Log.Error($"SmartInsiderConverter.Process(): Market {dataInstance.TickerCountry} is not supported at this time for ticker {ticker} on line {i}");
                        }

                        previousMarket = dataInstance.TickerCountry;
                        previousTicker = ticker;

                        continue;
                    }

                    var mapFile = _mapFileResolver.ResolveMapFile(ticker, dataInstance.LastUpdate);
                    if (!mapFile.Any())
                    {
                        Log.Error($"SmartInsiderConverter.Process(): Failed to find mapfile for ticker {ticker} on {dataInstance.LastUpdate} on line {i}");

                        previousMarket = dataInstance.TickerCountry;
                        previousTicker = ticker;

                        continue;
                    }

                    var newTicker = mapFile.GetMappedSymbol(dataInstance.LastUpdate);
                    if (string.IsNullOrEmpty(newTicker))
                    {
                        Log.Error($"SmartInsiderConverter.Process(): Failed to resolve ticker for old ticker {ticker} on line {i}");

                        previousMarket = dataInstance.TickerCountry;
                        previousTicker = ticker;

                        continue;
                    }

                    // Log any mapping events since this can be a point of failure
                    if (ticker != newTicker)
                    {
                        Log.Trace($"SmartInsiderConverter.Process(): Mapped ticker from {ticker} to {newTicker}");
                    }

                    List<T> symbolLines;
                    if (!lines.TryGetValue(newTicker, out symbolLines))
                    {
                        symbolLines = new List<T>();
                        lines[newTicker] = symbolLines;
                    }

                    symbolLines.Add(dataInstance);

                    previousMarket = dataInstance.TickerCountry;
                    previousTicker = ticker;
                }
                catch (Exception e)
                {
                    Log.Error(e, $"SmartInsiderConverter.Process(): Error on line {i}");
                }
            }

            return lines;
        }

        /// <summary>
        /// Writes to a temp file and moves the content to the final directory
        /// </summary>
        /// <param name="finalFile">Final file to write to</param>
        /// <param name="contents">Contents to write to file</param>
        private void WriteToFile<T>(DirectoryInfo destinationDirectory, Dictionary<string, List<T>> contents)
            where T : SmartInsiderEvent
        {
            foreach (var kvp in contents)
            {
                var ticker = kvp.Key.ToLower();

                var finalFile = new FileInfo(Path.Combine(destinationDirectory.FullName, $"{ticker}.csv"));
                var fileContents = new List<T>();

                if (finalFile.Exists)
                {
                    Log.Trace($"SmartInsiderConverter.WriteToFile(): Writing to existing file: {finalFile.FullName}");
                    fileContents = File.ReadAllLines(finalFile.FullName)
                        .Select(x => (T)CreateSmartInsiderInstance<T>(x))
                        .ToList();
                }
                else
                {
                    Log.Trace($"SmartInsiderConverter.WriteToFile(): Writing to new file: {finalFile.FullName}");
                }

                fileContents.AddRange(kvp.Value);

                var csvContents = fileContents
                    .OrderBy(x => x.LastUpdate)
                    .Select(x => x.ToCsv())
                    .Distinct();

                File.WriteAllLines(finalFile.FullName, csvContents);
            }
        }

        /// <summary>
        /// Resolves type parameter to corresponding <see cref="SmartInsiderEvent"/> derived class
        /// </summary>
        /// <typeparam name="T"><see cref="SmartInsiderEvent"/> derived class</typeparam>
        /// <param name="line">CSV line</param>
        /// <returns>SmartInsiderEvent derived class</returns>
        private SmartInsiderEvent CreateSmartInsiderInstance<T>(string line)
        {
            if (typeof(T) == typeof(SmartInsiderIntention))
            {
                return new SmartInsiderIntention(line);
            }
            if (typeof(T) == typeof(SmartInsiderTransaction))
            {
                return new SmartInsiderTransaction(line);
            }

            throw new InvalidOperationException($"Smart Insider custom data source '{typeof(T).Name}' is not supported");
        }
    }
}

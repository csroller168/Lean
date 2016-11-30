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
using System.IO;
using System.Linq;
using QuantConnect.Logging;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.DataFeeds.Enumerators;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace QuantConnect.ToolBox.AlgoSeekFuturesConverter
{
    using Processors = Dictionary<Symbol, List<List<AlgoSeekFuturesProcessor>>>;
    /// <summary>
    /// Process a directory of algoseek futures files into separate resolutions.
    /// </summary>
    public class AlgoSeekFuturesConverter
    {
        private const int execTimeout = 180;// sec
        private string _source;
        private string _remote;
        private string _destination;
        private List<Resolution> _resolutions;
        private DateTime _referenceDate;

        private readonly ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 5 };

        /// <summary>
        /// Create a new instance of the AlgoSeekFutures Converter. Parse a single input directory into an output.
        /// </summary>
        /// <param name="resolutions">Convert this resolution</param>
        /// <param name="referenceDate">Datetime to be added to the milliseconds since midnight. Algoseek data is stored in channel files (XX.bz2) and in a source directory</param>
        /// <param name="source">Remote directory of the .bz algoseek files</param>
        /// <param name="source">Source directory of the .csv algoseek files</param>
        /// <param name="destination">Data directory of LEAN</param>
        public AlgoSeekFuturesConverter(List<Resolution> resolutions, DateTime referenceDate, string remote, string source, string destination)
        {
            _source = source;
            _remote = remote;
            _referenceDate = referenceDate;
            _destination = destination;
            _resolutions = resolutions;
        }

        /// <summary>
        /// Give the reference date and source directory, convert the algoseek data into n-resolutions LEAN format.
        /// </summary>
        public void Convert()
        {
            //Get the list of all the files, then for each file open a separate streamer.
            var files = Directory.EnumerateFiles(_remote, "*.bz2");
            files = files.Where(x => Path.GetFileNameWithoutExtension(x).ToLower().IndexOf("option") == -1);

            Log.Trace("AlgoSeekFuturesConverter.Convert(): Loading {0} AlgoSeekFuturesReader for {1} ", files.Count(), _referenceDate);

            //Initialize parameters
            var totalLinesProcessed = 0L;
            var totalFiles = files.Count();
            var totalFilesProcessed = 0;
            var start = DateTime.MinValue;

            var zipper = OS.IsWindows ? "C:/Program Files/7-Zip/7z.exe" : "7z";
            var random = new Random((int)DateTime.Now.Ticks);

            var symbolMultipliers = LoadSymbolMultipliers();

            //Extract each file massively in parallel.
            Parallel.ForEach(files, parallelOptions, file =>
            {
                Log.Trace("Remote File :" + file);

                var csvFile = Path.Combine(_source, Path.GetFileName(file).Replace(".bz2", ""));

                Log.Trace("Source File :" + csvFile);

                if (!File.Exists(csvFile))
                {
                    Log.Trace("AlgoSeekFuturesConverter.Convert(): Extracting " + file);
                    var psi = new ProcessStartInfo(zipper, " e " + file + " -o" + _source)
                    {
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };

                    var process = new Process();
                    process.StartInfo = psi;
                    process.Start();

                    while (!process.StandardOutput.EndOfStream)
                    {
                        process.StandardOutput.ReadLine();
                    }

                    if (!process.WaitForExit(execTimeout * 1000))
                    {
                        Log.Error("7Zip timed out: " + file);
                    }
                    else
                    {
                        if (process.ExitCode > 0)
                        {
                            Log.Error("7Zip Exited Unsuccessfully: " + file);
                        }
                    }
                }
                Log.Trace("Source File :" + csvFile);

                // setting up local processors and the flush event
                var processors = new Processors();
                var waitForFlush = new ManualResetEvent(true);

                // symbol filters 
                // var symbolFilterNames = new string[] { "AAPL", "TWX", "NWSA", "FOXA", "AIG", "EGLE", "EGEC" };
                // var symbolFilter = symbolFilterNames.SelectMany(name => new[] { name, name + "1", name + ".1" }).ToHashSet();
                // var reader = new AlgoSeekFuturesReader(csvFile, symbolFilter);

                var reader = new AlgoSeekFuturesReader(csvFile, symbolMultipliers);
                if (start == DateTime.MinValue)
                {
                    start = DateTime.Now;
                }

                var flushStep = TimeSpan.FromMinutes(15 + random.NextDouble() * 5);

                if (reader.Current != null) // reader contains the data
                {
                    var previousFlush = reader.Current.Time.RoundDown(flushStep);

                    do
                    {
                        var tick = reader.Current as Tick;

                        //If the next minute has clocked over; flush the consolidators; serialize and store data to disk.
                        if (tick.Time.RoundDown(flushStep) > previousFlush)
                        {
                            previousFlush = WriteToDisk(processors, waitForFlush, tick.Time, flushStep);
                            processors = new Processors();
                        }

                        //Add or create the consolidator-flush mechanism for symbol:
                        List<List<AlgoSeekFuturesProcessor>> symbolProcessors;
                        if (!processors.TryGetValue(tick.Symbol, out symbolProcessors))
                        {
                            symbolProcessors = new List<List<AlgoSeekFuturesProcessor>>(3)
                                        {
                                            { _resolutions.Select(x => new AlgoSeekFuturesProcessor(tick.Symbol, _referenceDate, TickType.Trade, x, _destination)).ToList() },
                                            { _resolutions.Select(x => new AlgoSeekFuturesProcessor(tick.Symbol, _referenceDate, TickType.Quote, x, _destination)).ToList() },
                                            { _resolutions.Select(x => new AlgoSeekFuturesProcessor(tick.Symbol, _referenceDate, TickType.OpenInterest, x, _destination)).ToList() }
                                        };

                            processors[tick.Symbol] = symbolProcessors;
                        }

                        // Pass current tick into processor: enum 0 = trade; 1 = quote, 2 = oi
                        foreach (var processor in symbolProcessors[(int)tick.TickType])
                        {
                            processor.Process(tick);
                        }

                        if (Interlocked.Increment(ref totalLinesProcessed) % 1000000m == 0)
                        {
                            var pro = (double)processors.Values.SelectMany( p => p.SelectMany( x => x.Select( c => c ))).Count();
                            var symbols = (double)processors.Keys.Count();
                            Log.Trace("AlgoSeekFuturesConverter.Convert(): Processed {0,3}M ticks( {1}k / sec); Memory in use: {2} MB; Total progress: {3}%, Processor per symbol {4}", Math.Round(totalLinesProcessed / 1000000m, 2), Math.Round(totalLinesProcessed / 1000L / (DateTime.Now - start).TotalSeconds), Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024), 100 * totalFilesProcessed / totalFiles, pro / symbols);
                        }

                    }
                    while (reader.MoveNext());

                    Log.Trace("AlgoSeekFuturesConverter.Convert(): Performing final flush to disk... ");
                    Flush(processors, DateTime.MaxValue, true);
                    WriteToDisk(processors, waitForFlush, DateTime.MaxValue, flushStep, true);
                }

                Log.Trace("AlgoSeekFuturesConverter.Convert(): Finished processing file: " + file);
                Interlocked.Increment(ref totalFilesProcessed);
            });


        }

        /// <summary>
        /// Private method loads symbol multipliers from algoseek csv file
        /// </summary>
        /// <returns></returns>

        private Dictionary<string, decimal> LoadSymbolMultipliers()
        {
            const int columnsCount = 4;
            const int columnUnderlying = 0;
            const int columnProductName = 1;
            const int columnMultipleFactor = 2;
            const int columnInfo = 3;

            return File.ReadAllLines("AlgoSeekFuturesConverter/AlgoSeek.US.Futures.PriceMultipliers.1.1.csv")
                    .Select(line => line.ToCsvData())
                    // skipping empty fields
                    .Where(line => !string.IsNullOrEmpty(line[columnUnderlying]) && 
                                   !string.IsNullOrEmpty(line[columnMultipleFactor]))
                    // skipping header
                    .Skip(1)
                    .ToDictionary(line => line[columnUnderlying],
                                  line => System.Convert.ToDecimal(line[columnMultipleFactor]));
        }

        /// <summary>
        /// Write the processor queues to disk
        /// </summary>
        /// <param name="peekTickTime">Time of the next tick in the stream</param>
        /// <param name="step">Period between flushes to disk</param>
        /// <param name="final">Final push to disk</param>
        /// <returns></returns>
        private DateTime WriteToDisk(Processors processors, ManualResetEvent waitForFlush, DateTime peekTickTime, TimeSpan step, bool final = false)
        {
            waitForFlush.WaitOne();
            waitForFlush.Reset();
            Flush(processors, peekTickTime, final);

            Task.Run(() =>
            {
                foreach (var type in Enum.GetValues(typeof(TickType)))
                {
                    var tickType = type;
                    var groups = processors.Values.SelectMany(x => x[(int)tickType]).Where(x => x.Queue.Count > 0).GroupBy(process => process.Symbol.Underlying.Value);

                    Parallel.ForEach(groups, parallelOptions, group =>
                    {
                        var symbol = group.Key;

                        foreach (var processor in group)
                        {
                            var zip = processor.ZipPath.Replace(".zip", string.Empty);
                            var tempFileName = Path.Combine(zip, processor.EntryPath);

                            Directory.CreateDirectory(zip);
                            File.AppendAllText(tempFileName, FileBuilder(processor));
                        }
                    });
                }
                waitForFlush.Set();
            });

            //Pause while writing the final flush.
            if (final) waitForFlush.WaitOne();

            return peekTickTime.RoundDown(step);
        }


        /// <summary>
        /// Output a list of basedata objects into a string csv line.
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        private string FileBuilder(AlgoSeekFuturesProcessor processor)
        {
            var sb = new StringBuilder();
            foreach (var data in processor.Queue)
            {
                sb.AppendLine(LeanData.GenerateLine(data, SecurityType.Future, processor.Resolution));
            }
            return sb.ToString();
        }

        private void Flush(Processors processors, DateTime time, bool final)
        {
            foreach (var symbol in processors.Keys)
            {
                processors[symbol].ForEach(p => p.ForEach(x => x.FlushBuffer(time, final)));
            }
        }

        /// <summary>
        /// Compress the queue buffers directly to a zip file. Lightening fast as streaming ram-> compressed zip.
        /// </summary>
        public void Package(DateTime date)
        {
            var zipper = OS.IsWindows ? "C:/Program Files/7-Zip/7z.exe" : "7z";

            Log.Trace("AlgoSeekFuturesConverter.Package(): Zipping all files ...");

            var destination = Path.Combine(_destination, "future");
            var dateMask = date.ToString(DateFormat.EightCharacter);

            var files =
                Directory.EnumerateFiles(destination, dateMask + "*.csv", SearchOption.AllDirectories)
                .GroupBy(x => Directory.GetParent(x).FullName)
                .ToList();

            //Zip each file massively in parallel.
            Parallel.ForEach(files, parallelOptions, file =>
            {
                var outputFileName = file.Key + ".zip";
                var inputFileNames = Path.Combine(file.Key, "*.csv");
                var cmdArgs = " a " + outputFileName + " " + inputFileNames;

                Log.Trace("AlgoSeekFuturesConverter.Convert(): Zipping " + outputFileName);
                var psi = new ProcessStartInfo(zipper, cmdArgs)
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                var process = new Process();
                process.StartInfo = psi;
                process.Start();

                while (!process.StandardOutput.EndOfStream)
                {
                    process.StandardOutput.ReadLine();
                }

                if (!process.WaitForExit(execTimeout * 1000))
                {
                    Log.Error("7Zip timed out: " + outputFileName);
                }
                else
                {
                    if (process.ExitCode > 0)
                    {
                        Log.Error("7Zip Exited Unsuccessfully: " + outputFileName);
                    }
                    else
                    {
                        try
                        {
                            Directory.Delete(file.Key, true);
                        }
                        catch (Exception err)
                        {
                            Log.Error("Directory.Delete returned error: " + err.Message);
                        }
                    }
                }
            });
        }

    }
}
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NodaTime;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Util;

namespace QuantConnect.ToolBox.KaikoDataConverter
{
    /// <summary>
    /// Console application for converting Kaiko data into Lean data format
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("The arguments must be specified as [market] [tickType (quote/trade)] [kaiko raw data directory path]");
            }

            var market = args[0];
            var tickType = args[1] == "quote" ? TickType.Quote : TickType.Trade;
            var fileLocation = args[2];

            if (tickType == TickType.Quote)
            {
                CreateCryptoTicks(fileLocation, market, tickType, ParseKaikoQuoteFile);
                AggregateTicksInAllResolutions(market, tickType);
            }
            else
            {
                CreateCryptoTicks(fileLocation, market, tickType, ParseKaikoTradeFile);
                AggregateTicksInAllResolutions(market, tickType);
            }
        }

        /// <summary>
        /// Create ticks from Raw Kaiko data
        /// </summary>
        /// <param name="fileLocation">Path to the raw Kaiko data</param>
        /// <param name="market">The exchange the data represents</param>
        /// <param name="tickType">The tick type being processed</param>
        /// <param name="aggregateFunction">Function that parses the Kaiko data file and returns the enumerable of ticks</param>
        private static void CreateCryptoTicks(string fileLocation, string market, TickType tickType, Func<Symbol, string, IEnumerable<Tick>> aggregateFunction)
        {
            foreach (var symbolFolder in Directory.EnumerateDirectories(fileLocation))
            {
                var symbolDirectoryInfo = new DirectoryInfo(symbolFolder);

                // Create symbol from folder name
                var symbol = Symbol.Create(symbolDirectoryInfo.Name, SecurityType.Crypto, market);

                foreach (var symbolMonthDirectory in Directory.EnumerateDirectories(symbolDirectoryInfo.FullName))
                {
                    foreach (var tradeFile in Directory.EnumerateFiles(symbolMonthDirectory))
                    {
                        // Unzip file
                        var unzippedFile = Compression.UnGZip(tradeFile, symbolMonthDirectory);

                        // Write the ticks
                        var writer = new LeanDataWriter(Resolution.Tick, symbol, Globals.DataFolder, tickType);
                        writer.Write(aggregateFunction(symbol, unzippedFile));

                        // Clean up unzipped file
                        File.Delete(unzippedFile);
                    }
                }
            }
        }

        /// <summary>
        /// Aggregate the ticks into all second, minute, hour and daily resolution
        /// </summary>
        /// <param name="market">The market the ticks represent</param>
        /// <param name="tickType">The TickType being processed</param>
        private static void AggregateTicksInAllResolutions(string market, TickType tickType)
        {
            var tickBasePath = Path.Combine(Globals.DataFolder, "crypto", market, "tick");

            foreach (var tickDirectory in Directory.EnumerateDirectories(tickBasePath))
            {
                var symbolDirectoryInfo = new DirectoryInfo(tickDirectory);
                var symbol = Symbol.Create(symbolDirectoryInfo.Name, SecurityType.Crypto, market);

                foreach (var tickDateFile in Directory.EnumerateFiles(symbolDirectoryInfo.FullName))
                {
                    // There are both trade and quote files in directory - we only want one type
                    if (!tickDateFile.Contains(tickType.ToLower())) continue;

                    var consolidators = GetDataAggregatorsForTickType(tickType);
                    var reader = GetLeanDataTickReader(symbol, tickType, tickDateFile);

                    foreach (var tickBar in reader.Parse().Select(x => x as Tick))
                    {
                        foreach (var consolidator in consolidators)
                        {
                            consolidator.Consolidator.Update(tickBar);
                        }
                    }

                    foreach (var consolidator in consolidators)
                    {
                        WriteTradeTicksForResolution(symbol, consolidator.Resolution, tickType, consolidator.Flush());
                    }
                }
            }
        }

        /// <summary>
        /// Get data aggregators for specified tick type at every resolution
        /// </summary>
        /// <param name="tickType">The tick type being processed</param>
        /// <returns>A collection of <see cref="KaikoDataAggregator"/></returns>
        private static List<KaikoDataAggregator> GetDataAggregatorsForTickType(TickType tickType)
        {
            if (tickType == TickType.Quote)
            {
                return new List<KaikoDataAggregator>
                {
                    new KaikoQuoteDataAggregator(Resolution.Second),
                    new KaikoQuoteDataAggregator(Resolution.Minute),
                    new KaikoQuoteDataAggregator(Resolution.Hour),
                    new KaikoQuoteDataAggregator(Resolution.Daily),
                };
            }

            return new List<KaikoDataAggregator>
            {
                new KaikoTradeDataAggregator(Resolution.Second),
                new KaikoTradeDataAggregator(Resolution.Minute),
                new KaikoTradeDataAggregator(Resolution.Hour),
                new KaikoTradeDataAggregator(Resolution.Daily),
            };
        }

        /// <summary>
        /// Use the lean data writer to write the ticks for a specific resolution
        /// </summary>
        /// <param name="symbol">The symbol these ticks represent</param>
        /// <param name="resolution">The resolution that should be written</param>
        /// <param name="tickType">The tpye (Trades/Quotes) </param>
        /// <param name="bars">The aggregated bars being written to disk</param>
        private static void WriteTradeTicksForResolution(Symbol symbol, Resolution resolution, TickType tickType, List<BaseData> bars)
        {
            var writer = new LeanDataWriter(resolution, symbol, Globals.DataFolder, tickType);
            writer.Write(bars);
        }


        /// <summary>
        /// Get a lean data reader for a specific symbol to read the ticks
        /// </summary>
        /// <param name="symbol">The symbol being read</param>
        /// <param name="tickDateFile">The path to the tick file</param>
        /// <returns>A <see cref="LeanDataReader"/></returns>
        private static LeanDataReader GetLeanDataTickReader(Symbol symbol, TickType type, string tickDateFile)
        {
            Symbol sym;
            DateTime date;
            Resolution res;
            var subscription = new SubscriptionDataConfig(typeof(Tick), symbol, Resolution.Tick,
                DateTimeZone.Utc, DateTimeZone.Utc, false, false, false, false, type);
            LeanData.TryParsePath(tickDateFile, out sym, out date, out res);
            return new LeanDataReader(subscription, symbol, Resolution.Tick, date, Globals.DataFolder);
        }

        /// <summary>
        /// Parse order book information for Kaiko data files
        /// </summary>
        /// <param name="symbol">The symbol being converted</param>
        /// <param name="unzippedFile">The path to the unzipped file</param>
        /// <returns>Lean quote ticks representing the Kaiko data</returns>
        private static IEnumerable<Tick> ParseKaikoQuoteFile(Symbol symbol, string unzippedFile)
        {
            using (var sr = new StreamReader(unzippedFile))
            {
                var headerLine = sr.ReadLine();
                var headerCsv = headerLine.ToCsv();
                var typeColumn = headerCsv.FindIndex(x => x == "type");
                var dateColumn = headerCsv.FindIndex(x => x == "date");
                var priceColumn = headerCsv.FindIndex(x => x == "price");
                var quantityColumn = headerCsv.FindIndex(x => x == "amount");

                long currentEpoch = 0;
                var currentEpochTicks = new List<KaikoTick>();

                while (sr.Peek() >= 0)
                {

                    var line = sr.ReadLine();
                    if (line == null) continue;

                    var lineParts = line.Split(',');

                    var tickEpoch = Convert.ToInt64(lineParts[dateColumn]);
                    var currentTick = new KaikoTick
                    {
                        TickType = TickType.Quote,
                        Time = Time.UnixMillisecondTimeStampToDateTime(tickEpoch),
                        Quantity = ParseQuantity(lineParts, quantityColumn),
                        Value = Convert.ToDecimal(lineParts[priceColumn]),
                        OrderDirection = lineParts[typeColumn]
                    };

                    if (currentEpoch != tickEpoch)
                    {
                        var quoteTick = CreateQuoteTick(symbol, Time.UnixMillisecondTimeStampToDateTime(currentEpoch), currentEpochTicks);

                        if (quoteTick != null) yield return quoteTick;

                        currentEpochTicks.Clear();
                        currentEpoch = tickEpoch;
                    }

                    currentEpochTicks.Add(currentTick);
                }
            }
        }

        /// <summary>
        /// Take a minute snapshot of order book information and make a single Lean quote tick
        /// </summary>
        /// <param name="symbol">The symbol being processed</param>
        /// <param name="date">The data being processed</param>
        /// <param name="currentEpcohTicks">The snapshot of bid/ask Kaiko data</param>
        /// <returns>A single Lean quote tick</returns>
        private static Tick CreateQuoteTick(Symbol symbol, DateTime date, List<KaikoTick> currentEpcohTicks)
        {
            var bestAsk = currentEpcohTicks.Where(x => x.OrderDirection == "a")
                                        .OrderByDescending(x => x.Value)
                                        .FirstOrDefault();

            var bestBid = currentEpcohTicks.Where(x => x.OrderDirection == "b")
                                        .OrderByDescending(x => x.Value)
                                        .FirstOrDefault();

            if (bestAsk == null && bestBid == null)
            {
                // Did not have enough data to create a tick
                return null;
            }

            var tick = new Tick()
            {
                Symbol = symbol,
                Time = date,
                TickType = TickType.Quote
            };

            if (bestBid != null)
            {
                tick.BidPrice = bestBid.Price;
                tick.BidSize = bestBid.Quantity;
            }

            if (bestAsk != null)
            {
                tick.AskPrice = bestAsk.Price;
                tick.AskSize = bestAsk.Quantity;
            }

            return tick;
        }

        /// <summary>
        /// Parse a kaiko trade file
        /// </summary>
        /// <param name="symbol">The symbol being processed</param>
        /// <param name="unzippedFile">The path to the unzipped file</param>
        /// <returns>Lean Ticks in the Kaiko file</returns>
        private static IEnumerable<Tick> ParseKaikoTradeFile(Symbol symbol, string unzippedFile)
        {
            using (var sr = new StreamReader(unzippedFile))
            {
                var headerLine = sr.ReadLine();
                var headerCsv = headerLine.ToCsv();
                var dateColumn = headerCsv.FindIndex(x => x == "date");
                var priceColumn = headerCsv.FindIndex(x => x == "price");
                var quantityColumn = headerCsv.FindIndex(x => x == "amount");

                while (sr.Peek() >= 0)
                {
                    var line = sr.ReadLine();
                    if (line == null) continue;

                    var lineParts = line.Split(',');
                    yield return new Tick
                    {
                        Symbol = symbol,
                        TickType = TickType.Trade,
                        Time = Time.UnixMillisecondTimeStampToDateTime(Convert.ToInt64(lineParts[dateColumn])),
                        Quantity = ParseQuantity(lineParts, quantityColumn),
                        Value = Convert.ToDecimal(lineParts[priceColumn])
                    };
                }
            }
        }

        /// <summary>
        /// Parse the quantity field of the kaiko ticks - can sometimes be expressed in scientific notation
        /// </summary>
        /// <param name="lineParts">The line from the Kaiko file</param>
        /// <param name="quantityColumn">The index of the quantity column </param>
        /// <returns>The quantity as a decimal</returns>
        private static decimal ParseQuantity(string[] lineParts, int quantityColumn)
        {
            var quantity = lineParts[quantityColumn];
            if (quantity.Contains("e"))
            {
                return Decimal.Parse(quantity, System.Globalization.NumberStyles.Float);
            }

            return Convert.ToDecimal(lineParts[quantityColumn]);
        }

        /// <summary>
        /// Simple class to add order direction to Tick
        /// used for aggregating Kaiko order book snapshots
        /// </summary>
        internal class KaikoTick : Tick
        {
            public string OrderDirection { get; set; }
        }

        /// <summary>
        /// Base class for consolidator
        /// </summary>
        internal abstract class KaikoDataAggregator
        {
            public abstract List<BaseData> Flush();
            public abstract IDataConsolidator Consolidator { get; }
            public List<BaseData> Consolidated { get; protected set; }
            public Resolution Resolution { get; protected set; }
        }

        /// <summary>
        /// Use <see cref="TickQuoteBarConsolidator"/> to consolidate quote ticks into a specified resolution
        /// </summary>
        internal class KaikoQuoteDataAggregator : KaikoDataAggregator
        {
            public override IDataConsolidator Consolidator => _consolidator;

            private readonly TickQuoteBarConsolidator _consolidator;

            public KaikoQuoteDataAggregator(Resolution resolution)
            {
                Resolution = resolution;
                Consolidated = new List<BaseData>();
                _consolidator = new TickQuoteBarConsolidator(resolution.ToTimeSpan());
                _consolidator.DataConsolidated += (sender, consolidated) =>
                {
                    Consolidated.Add(consolidated);
                };
            }

            public override List<BaseData> Flush()
            {
                // Add the last bar
                Consolidated.Add(Consolidator.WorkingData as QuoteBar);
                return Consolidated;
            }
        }

        /// <summary>
        /// Use <see cref="TickQuoteBarConsolidator"/> to consolidate trade ticks into a specified resolution
        /// </summary>
        internal class KaikoTradeDataAggregator : KaikoDataAggregator
        {
            public override IDataConsolidator Consolidator => _consolidator;

            private readonly TickConsolidator _consolidator;

            public KaikoTradeDataAggregator(Resolution resolution)
            {
                Resolution = resolution;
                Consolidated = new List<BaseData>();
                _consolidator = new TickConsolidator(resolution.ToTimeSpan());
                _consolidator.DataConsolidated += (sender, consolidated) =>
                {
                    Consolidated.Add(consolidated);
                };
            }

            public override List<BaseData> Flush()
            {
                // Add the last bar
                Consolidated.Add(Consolidator.WorkingData as TradeBar);
                return Consolidated;
            }
        }
    }
}
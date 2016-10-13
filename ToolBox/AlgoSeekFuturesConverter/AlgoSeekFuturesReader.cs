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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using QuantConnect.Data.Market;
using QuantConnect.Util;
using QuantConnect.Logging;
using System.Globalization;

namespace QuantConnect.ToolBox.AlgoSeekFuturesConverter
{
    /// <summary>
    /// Enumerator for converting AlgoSeek futures files into Ticks.
    /// </summary>
    public class AlgoSeekFuturesReader : IEnumerator<Tick>
    {
        private Stream _stream;
        private StreamReader _streamReader;
        private HashSet<string> _symbolFilter;

        private readonly int _columnTimestamp = -1;
        private readonly int _columnSecID = -1;
        private readonly int _columnTicker = -1;
        private readonly int _columnType = -1;
        private readonly int _columnSide = -1;
        private readonly int _columnQuantity = -1;
        private readonly int _columnPrice = -1;

        /// <summary>
        /// Enumerate through the lines of the algoseek files.
        /// </summary>
        /// <param name="file">BZ File for algoseek</param>
        /// <param name="date">Reference date of the folder</param>
        public AlgoSeekFuturesReader(string file, HashSet<string> symbolFilter = null)
        {
            var streamProvider = StreamProvider.ForExtension(Path.GetExtension(file));
            _stream = streamProvider.Open(file).First();
            _streamReader = new StreamReader(_stream);
            _symbolFilter = symbolFilter;

            // detecting column order in the file
            var header = _streamReader.ReadLine().ToCsv();
            _columnTimestamp = header.FindIndex(x => x == "Timestamp");
            _columnTicker = header.FindIndex(x => x == "Ticker");
            _columnType = header.FindIndex(x => x == "Type");
            _columnSide = header.FindIndex(x => x == "Side");
            _columnSecID = header.FindIndex(x => x == "SecurityID");
            _columnQuantity = header.FindIndex(x => x == "Quantity");
            _columnPrice = header.FindIndex(x => x == "Price");

            //Prime the data pump, set the current.
            Current = null;
            MoveNext();
        }

        /// <summary>
        /// Parse the next line of the algoseek future file.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            string line;
            Tick tick = null;
            while ((line = _streamReader.ReadLine()) != null && tick == null)
            {
                // If line is invalid continue looping to find next valid line.
                tick = Parse(line);
            }
            Current = tick;
            return Current != null;
        }

        /// <summary>
        /// Current top of the tick file.
        /// </summary>
        public Tick Current
        {
            get; private set;

        }

        /// <summary>
        /// Gets the current element in the collection.
        /// </summary>
        /// <returns>
        /// The current element in the collection.
        /// </returns>
        object IEnumerator.Current
        {
            get { return Current; }
        }

        /// <summary>
        /// Reset the enumerator for the AlgoSeekFuturesReader
        /// </summary>
        public void Reset()
        {
            throw new NotImplementedException("Reset not implemented for AlgoSeekFuturesReader.");
        }

        /// <summary>
        /// Dispose of the underlying AlgoSeekFuturesReader
        /// </summary>
        public void Dispose()
        {
            _stream.Close();
            _stream.Dispose();
            _streamReader.Close();
            _streamReader.Dispose();
        }

        /// <summary>
        /// Parse a string line into a future tick.
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private Tick Parse(string line)
        {
            try
            {
                const int TradeMask = 2;
                const int QuoteMask = 1;
                const int MessageTypeMask = 15;

                // parse csv check column count
                const int columns = 11;
                var csv = line.ToCsv(columns);
                if (csv.Count < columns)
                {
                    return null;
                }

                var ticker = csv[_columnTicker];
                
                // we filter out options and spreads
                if (ticker.IndexOfAny(new [] { ' ', '-'}) != -1)
                {
                    return null;
                }

                // detecting tick type (trade or quote)
                TickType tickType;
                bool isAsk = false;

                var type = Convert.ToInt32(csv[_columnType]);
                if ((type & MessageTypeMask) == TradeMask)
                {
                    tickType = TickType.Trade;
                }
                else if ((type & MessageTypeMask) == QuoteMask)
                {
                    tickType = TickType.Quote;

                    switch (csv[_columnSide])
                    {
                        case "B":
                            isAsk = false;
                            break;
                        case "S":
                            isAsk = true;
                            break;
                        default:
                            return null;
                    }
                }
                else
                {
                    return null;
                }

                ticker = ticker.Trim(new char[] { '"' });

                if (_symbolFilter != null && !_symbolFilter.Contains(ticker))
                {
                    return null;
                }

                // ignoring time zones completely -- this is all in the 'data-time-zone'
                var timeString = csv[_columnTimestamp];
                var time = DateTime.ParseExact(timeString, "yyyyMMddHHmmssFFF", CultureInfo.InvariantCulture);

                var futuresExpirationSymbology = new Dictionary<string, int>
                        {
                            { "F", 1 },
                            { "G", 2 },
                            { "H", 3 },
                            { "J", 4 },
                            { "K", 5 },
                            { "M", 6 },
                            { "N", 7 },
                            { "Q", 8 },
                            { "U", 9 },
                            { "V", 10 },
                            { "X", 11 },
                            { "Z", 12 }
                        };

                var expirationYearString = ticker.Substring(ticker.Length - 1, 1);
                var expirationMonthString = ticker.Substring(ticker.Length - 2, 1);
                var underlyingString = ticker.Substring(0, ticker.Length - 2);

                int expirationYearShort;

                if (!int.TryParse(expirationYearString, out expirationYearShort))
                {
                    return null;
                }

                if (!futuresExpirationSymbology.ContainsKey(expirationMonthString))
                {
                    return null;
                }

                var expirationMonth = futuresExpirationSymbology[expirationMonthString];
                var exprirationYear = 2010 + expirationYearShort;
                var expirationYearMonth = new DateTime(exprirationYear, expirationMonth, DateTime.DaysInMonth(exprirationYear, expirationMonth));
                var symbol = Symbol.CreateFuture(underlyingString, Market.USA, expirationYearMonth);
              
                var price = csv[_columnPrice].ToDecimal() / 100000000m;
                var quantity = csv[_columnQuantity].ToInt32();

                var tick = new Tick
                {
                    Symbol = symbol,
                    Time = time,
                    TickType = tickType,
                    Exchange = Market.USA,
                    Value = price
                };

                if (tickType == TickType.Quote)
                {
                    if (isAsk)
                    {
                        tick.AskPrice = price;
                        tick.AskSize = quantity;
                    }
                    else
                    {
                        tick.BidPrice = price;
                        tick.BidSize = quantity;
                    }
                }
                else
                {
                    tick.Quantity = quantity;
                }
             
                return tick;
            }
            catch (Exception err)
            {
                Log.Error(err);
                Log.Trace("Line: {0}", line);
                return null;
            }
        }
    }
}

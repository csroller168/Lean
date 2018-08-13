/*
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

using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Indicators;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// First attempt
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class SectorMomentumAlgorithm : QCAlgorithm //, IRegressionAlgorithmDefinition
    {
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        private int FastPeriod = 50;
        private int SlowPeriod = 200;
        private int MaxNumPositions = 6;

        private string _benchmarkStr = "SPY";
        private string[] _universeStr = {
            "IYM",
            "IYC",
            "IYK",
            "IYE",
            "IYF",
            "IYH",
            "IYR",
            "IYW",
            "IDU"
        };

        private Symbol _benchmark;
        private Dictionary<string, Symbol> _universe;

        private Dictionary<string,SimpleMovingAverage> _fastUniverse;
        private Dictionary<string, SimpleMovingAverage> _slowUniverse;
        private SimpleMovingAverage _fastBenchmark;
        private SimpleMovingAverage _slowBenchmark;

        private bool IsMarketFavorable => _fastBenchmark > _slowBenchmark;

        public override void Initialize()
        {
            SetStartDate(2015, 1, 02);
            SetEndDate(2015, 10, 30);
            SetCash(100000);

            // Add benchmark
            _benchmark = QuantConnect.Symbol.Create(_benchmarkStr, SecurityType.Equity, Market.USA);
            _fastBenchmark = new SimpleMovingAverage(_benchmark.Value, FastPeriod);
            _slowBenchmark = new SimpleMovingAverage(_benchmark.Value, SlowPeriod);
            AddEquity(_benchmarkStr, Resolution.Hour);

            // Add universe
            _universe = new Dictionary<string, Symbol>();
            _fastUniverse = new Dictionary<string, SimpleMovingAverage>();
            _slowUniverse = new Dictionary<string, SimpleMovingAverage>();
            foreach(var ticker in _universeStr)
            {
                var symbol = QuantConnect.Symbol.Create(ticker, SecurityType.Equity, Market.USA);
                _universe[symbol.Value] = symbol;
                _fastUniverse[symbol.Value] = new SimpleMovingAverage(symbol.Value, FastPeriod);
                _slowUniverse[symbol.Value] = new SimpleMovingAverage(symbol.Value, SlowPeriod);
                AddEquity(ticker, Resolution.Hour);
            }
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="slice">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice slice)
        {
            // TODO: Cancel open orders and pair new ones with trailing stop
            // TODO: warm sma?

            // Rebalance once per week
            if (Time.DayOfWeek != System.DayOfWeek.Monday)
                return;

            // Sell all if market momentum is down
            if (!IsMarketFavorable)
            {
                foreach (var symbol in Portfolio.Keys)
                    Liquidate(symbol);
                return;
            }

            // Figure out what to buy
            var momentums = new Dictionary<string, decimal>();
            foreach(KeyValuePair<string, Symbol> entry in _universe)
            {
                momentums[entry.Key] = _fastUniverse[entry.Value.Value] / _slowUniverse[entry.Value.Value];
            }

            var stocksToBuy = momentums
                .Where(kvp => kvp.Value > 1)
                .OrderByDescending(kvp => kvp.Value)
                .Take(MaxNumPositions)
                .Select(kvp => kvp.Key);

            // Sell anything we aren't going to buy
            Portfolio
                .Keys
                .Where(symbol => !stocksToBuy.Contains(symbol.Value))
                .ToList()
                .ForEach(symbol => Liquidate(symbol));

            // Rebalance
            foreach(var ticker in stocksToBuy)
            {
                var pct = 1.0 / stocksToBuy.Count();
                var symbol = _universe[ticker];
                SetHoldings(symbol, pct);
                Debug("Buy " + ticker);
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        //public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        //{
        //    // TBD
        //    {"Total Trades", "1"},
        //    {"Average Win", "0%"},
        //    {"Average Loss", "0%"},
        //    {"Compounding Annual Return", "264.583%"},
        //    {"Drawdown", "2.200%"},
        //    {"Expectancy", "0"},
        //    {"Net Profit", "1.668%"},
        //    {"Sharpe Ratio", "4.41"},
        //    {"Loss Rate", "0%"},
        //    {"Win Rate", "0%"},
        //    {"Profit-Loss Ratio", "0"},
        //    {"Alpha", "0.007"},
        //    {"Beta", "76.354"},
        //    {"Annual Standard Deviation", "0.193"},
        //    {"Annual Variance", "0.037"},
        //    {"Information Ratio", "4.354"},
        //    {"Tracking Error", "0.193"},
        //    {"Treynor Ratio", "0.011"},
        //    {"Total Fees", "$3.27"}
        //};
    }
}

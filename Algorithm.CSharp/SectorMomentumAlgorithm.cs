/*
 * Copyright Chris Short 2018
 * All rights reserved
*/

using System.Collections.Generic;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System.Linq;
using System;

using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Indicators;
using QuantConnect.Data.Market;
using System;

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
        public bool CanRunLocally { get; } = true;
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        private int FastPeriod = 10;
        private int SlowPeriod = 15;
        private int MaxNumPositions = 6;

        private string _benchmarkStr = "SPY";
        private List<string> _universe = new List<string> {
            "IYM",
            "IYC",
            //"IYK",
            "IYE",
            "IYF",
            "IYH",
            "IYR",
            "IYW",
            "IDU"
        };

        private Dictionary<string, ExponentialMovingAverage> _fastIndicators;
        private Dictionary<string, ExponentialMovingAverage> _slowIndicators;
        private DateTime _lastTradeDt;
        private static readonly int RebalanceIntervalDays = 7;
        private static readonly decimal Tolerance = 0.00015m;

        private bool IsMarketFavorable => _fastIndicators[_benchmarkStr] > _slowIndicators[_benchmarkStr] * (1 + Tolerance);

        public override void Initialize()
        {
            // TODO: Set commission
            SetStartDate(2014, 1, 02);
            SetEndDate(2015, 10, 30);
            SetCash(100000);

            // Add securities
            AddEquity(_benchmarkStr, Resolution.Minute, null, true);
            _universe.ForEach(x => AddEquity(x, Resolution.Minute, null, true));
            //AddSecurity(SecurityType.Equity, _benchmarkStr, Resolution.Daily);
            //_universe.ForEach(x => AddSecurity(SecurityType.Equity, x, Resolution.Daily));

            // Add indicators
            _fastIndicators = new Dictionary<string, ExponentialMovingAverage>
            {
                {"SPY", EMA(_benchmarkStr, FastPeriod, Resolution.Daily, x => ((TradeBar)x).Open) }
            };
            _slowIndicators = new Dictionary<string, ExponentialMovingAverage>
            {
                {"SPY", EMA(_benchmarkStr, SlowPeriod, Resolution.Daily, x => ((TradeBar)x).Open) }
            };
            foreach(var symbol in _universe) 
            {
                _fastIndicators.Add(symbol, EMA(symbol, FastPeriod, Resolution.Daily, x => ((TradeBar)x).Open));
                _slowIndicators.Add(symbol, EMA(symbol, SlowPeriod, Resolution.Daily, x => ((TradeBar)x).Open));
            }

            // Warm indicators
            var allHistory = History(Securities.Keys, TimeSpan.FromDays(SlowPeriod));
            foreach(var indicatorPair in _fastIndicators.Union(_slowIndicators)) 
            {
                allHistory.PushThrough(data => indicatorPair.Value.Update(
                    new IndicatorDataPoint
                    {
                        Value = data.Price,
                        DataType = data.DataType,
                        Symbol = data.Symbol,
                        Time = data.Time,
                        EndTime = data.EndTime
                    }));
            }
        }


        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        //public void OnData(TradeBars data)
        public override void OnData(Slice data)
        {
            if (!_slowIndicators[_benchmarkStr].IsReady) 
                return;

            // only once per week
            if ((Time.Date - _lastTradeDt.Date).Days < RebalanceIntervalDays) 
                return;

            if(IsMarketFavorable)
            {
                var momentums = new Dictionary<string, decimal>();
                _universe.ForEach(x => momentums[x] = _fastIndicators[x] / _slowIndicators[x]);
                var stocksToBuy = momentums
                    .Where(kvp => kvp.Value > 1)
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(MaxNumPositions)
                    .Select(kvp => kvp.Key)
                    .ToList();
                stocksToBuy.ForEach(x => Rebalance(x, 1.0 / stocksToBuy.Count));
            }
            else
            {
                _universe.ForEach(x => Rebalance(x, 0.0));
            }

            _lastTradeDt = Time;
        }

        private void Rebalance(string symbol, double pct)
        {
            if (pct > 0)
            {
                Log($"{Time.Date}: BUY {symbol} @ {Securities[symbol].Price}");
                SetHoldings(symbol, pct);
            }
            else
            {
                if (Portfolio.ContainsKey(symbol))
                {
                    Log($"{Time.Date}: SELL {symbol} @ {Securities[symbol].Price}");
                    Liquidate(symbol);
                }
            }
        }

        /*

        public override void Initialize()
        {
            SetStartDate(2015, 1, 02);
            SetEndDate(2015, 10, 30);
            SetCash(100000);

            // Add benchmark
            _benchmark = QuantConnect.Symbol.Create(_benchmarkStr, SecurityType.Equity, Market.USA);
            _fastBenchmark = new SimpleMovingAverage(_benchmark.Value, FastPeriod);
            _slowBenchmark = new SimpleMovingAverage(_benchmark.Value, SlowPeriod);
            AddEquity(_benchmarkStr, Resolution.Daily);

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
                AddEquity(ticker, Resolution.Daily);
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
            if (Time.DayOfWeek != System.DayOfWeek.Tuesday)
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
        */



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

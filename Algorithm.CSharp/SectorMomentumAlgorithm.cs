/*
 * Copyright Chris Short 2018
 * All rights reserved
*/

using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System.Linq;
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
        private Symbol _benchmark = QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA);
        private Dictionary<string, Symbol> _universe = new Dictionary<string, Symbol> {
            { "IYC", QuantConnect.Symbol.Create("IYC", SecurityType.Equity, Market.USA) },
            { "IYE", QuantConnect.Symbol.Create("IYE", SecurityType.Equity, Market.USA) },
            { "IYF", QuantConnect.Symbol.Create("IYF", SecurityType.Equity, Market.USA) },
            { "IYH", QuantConnect.Symbol.Create("IYH", SecurityType.Equity, Market.USA) },
            { "IYR", QuantConnect.Symbol.Create("IYR", SecurityType.Equity, Market.USA) },
            { "IYM", QuantConnect.Symbol.Create("IYM", SecurityType.Equity, Market.USA) },
            { "IYW", QuantConnect.Symbol.Create("IYW", SecurityType.Equity, Market.USA) },
            { "IDU", QuantConnect.Symbol.Create("IDU", SecurityType.Equity, Market.USA) }
            //{ "IYM", QuantConnect.Symbol.Create("IYM", SecurityType.Equity, Market.USA) },
            // IYK
        };

        private Dictionary<string, ExponentialMovingAverage> _fastIndicators;
        private Dictionary<string, ExponentialMovingAverage> _slowIndicators;
        private DateTime _lastTradeDt = DateTime.MinValue;
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
            // CMS DEBUG - change resolution to minute for live trading
            AddEquity(_benchmarkStr, Resolution.Minute, null, true);
            _universe.Keys.ToList().ForEach(x => AddEquity(x, Resolution.Minute, null, true));

            // Add indicators
            _fastIndicators = new Dictionary<string, ExponentialMovingAverage>
            {
                {_benchmarkStr, EMA(_benchmark, FastPeriod, Resolution.Daily, x => ((TradeBar)x).Open) }
            };
            _slowIndicators = new Dictionary<string, ExponentialMovingAverage>
            {
                {_benchmarkStr, EMA(_benchmark, SlowPeriod, Resolution.Daily, x => ((TradeBar)x).Open) }
            };
            foreach(var equity in _universe) 
            {
                _fastIndicators.Add(equity.Key, EMA(equity.Value, FastPeriod, Resolution.Daily, x => ((TradeBar)x).Open));
                _slowIndicators.Add(equity.Key, EMA(equity.Value, SlowPeriod, Resolution.Daily, x => ((TradeBar)x).Open));
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
        /// <param name="slice">TradeBars IDictionary object with your stock data</param>
        public override void OnData(Slice slice)
        {
            if (!_slowIndicators[_benchmarkStr].IsReady) 
                return;

            // only once per week
            if (_lastTradeDt != null && (Time.Date - _lastTradeDt.Date).Days < RebalanceIntervalDays) 
                return;
            
            //if(IsMarketFavorable)
            if(true)
            {
                var momentums = new Dictionary<string, decimal>();
                _universe.Keys.ToList().ForEach(x => momentums[x] = _fastIndicators[x] / _slowIndicators[x]);
                var stocksToBuy = momentums
                    .Where(kvp => kvp.Value > 0.0M) //1.0M)
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(MaxNumPositions)
                    .Select(kvp => kvp.Key)
                    .ToList();
                stocksToBuy.ForEach(x => Rebalance(x, 1.0 / stocksToBuy.Count));
            }
            else
            {
                _universe.Keys.ToList().ForEach(x => Rebalance(x, 0.0));
            }

            _lastTradeDt = Time;
        }

        private void Rebalance(string symbol, double pct)
        {
            if (pct > 0)
            {
                Log($"{Time.Date}: BUY {symbol} @ {Securities[symbol].Price}");
                SetHoldings(_universe[symbol], pct);
            }
            else
            {
                if (Portfolio.ContainsKey(_universe[symbol]))
                {
                    Log($"{Time.Date}: SELL {symbol} @ {Securities[symbol].Price}");
                    Liquidate(_universe[symbol]);
                }
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

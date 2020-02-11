using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Indicators;
using QuantConnect.Data.Market;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash. This is a skeleton
    /// framework you can use for designing an algorithm.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class IbIndicatorIntegrationTest : QCAlgorithm
    {
        //private Symbol _spy = QuantConnect.Symbol.Create("IYM", SecurityType.Equity, Market.USA);
        //private Symbol _spy1 = QuantConnect.Symbol.Create("IYC", SecurityType.Equity, Market.USA);
        private Symbol _spy2 = QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA);
        //private Symbol _spy3 = QuantConnect.Symbol.Create("IYE", SecurityType.Equity, Market.USA);
        //private Symbol _spy4 = QuantConnect.Symbol.Create("IYF", SecurityType.Equity, Market.USA);
        //private Symbol _spy5 = QuantConnect.Symbol.Create("IYH", SecurityType.Equity, Market.USA);
        //private Symbol _spy6 = QuantConnect.Symbol.Create("IYR", SecurityType.Equity, Market.USA);
        //private Symbol _spy7 = QuantConnect.Symbol.Create("IYW", SecurityType.Equity, Market.USA);
        //private Symbol _spy8 = QuantConnect.Symbol.Create("IDU", SecurityType.Equity, Market.USA);

        private int FastPeriod = 12;
        //private int SlowPeriod = 26;
        //private int MaxNumPositions = 6;

        private ExponentialMovingAverage _fast;
        //private ExponentialMovingAverage _slow;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            // Find more symbols here: http://quantconnect.com/data
            // Forex, CFD, Equities Resolutions: Tick, Second, Minute, Hour, Daily.
            // Futures Resolution: Tick, Second, Minute
            // Options Resolution: Minute Only.
            //AddEquity("IYM", Resolution.Minute);
            //AddEquity("IYC", Resolution.Minute);
            AddEquity("SPY", Resolution.Minute, null, true); // IYK
            //AddEquity("IYE", Resolution.Minute);
            //AddEquity("IYF", Resolution.Minute);
            //AddEquity("IYH", Resolution.Minute);
            //AddEquity("IYR", Resolution.Minute);
            //AddEquity("IYW", Resolution.Minute);
            //AddEquity("IDU", Resolution.Minute);

            _fast = EMA(_spy2, 15, Resolution.Daily, x => ((TradeBar)x).Open);
            //_slow = EMA(_spy2, 30, Resolution.Daily);

            var allHistory = History(Securities.Keys, TimeSpan.FromDays(15));
            allHistory.PushThrough(data => _fast.Update(
                new IndicatorDataPoint {
                Value = data.Price,
                DataType = data.DataType,
                Symbol = data.Symbol,
                Time = data.Time,
                EndTime = data.EndTime
            }));

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        //public override void OnTradeBar(Dictionary<string, TradeBar> data)
        {
            if (!_fast.IsReady)
            {
                Log("sma not ready - returning");
                return;
            }
            Log("SMA READY!!! :-)");
            
            if (!Portfolio.Invested)
            {
                //SetHoldings(_spy, 0.08);
                //SetHoldings(_spy1, 0.08);
                SetHoldings(_spy2, 0.08);
                //SetHoldings(_spy3, 0.08);
                //SetHoldings(_spy4, 0.08);
                //SetHoldings(_spy5, 0.08);
                //SetHoldings(_spy6, 0.08);
                //SetHoldings(_spy7, 0.08);
                //SetHoldings(_spy8, 0.08);
                Debug("Purchased Stock");
            }
            Log("FAST: " + _fast.ToDetailedString());
            //Log("SLOW: " + _slow.ToDetailedString());
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };
    }
}


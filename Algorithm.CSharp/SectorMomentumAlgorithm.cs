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
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// First attempt
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class SectorMomentumAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _spy = QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA);
        private Symbol _iym = QuantConnect.Symbol.Create("IYM", SecurityType.Equity, Market.USA);
        private Symbol _iyc = QuantConnect.Symbol.Create("IYC", SecurityType.Equity, Market.USA);
        private Symbol _iyk = QuantConnect.Symbol.Create("IYK", SecurityType.Equity, Market.USA);
        private Symbol _iye = QuantConnect.Symbol.Create("IYE", SecurityType.Equity, Market.USA);
        private Symbol _iyf = QuantConnect.Symbol.Create("IYF", SecurityType.Equity, Market.USA);
        private Symbol _iyh = QuantConnect.Symbol.Create("IYH", SecurityType.Equity, Market.USA);
        private Symbol _iyr = QuantConnect.Symbol.Create("IYR", SecurityType.Equity, Market.USA);
        private Symbol _iyw = QuantConnect.Symbol.Create("IYW", SecurityType.Equity, Market.USA);
        private Symbol _idu = QuantConnect.Symbol.Create("IDU", SecurityType.Equity, Market.USA);


        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);
            SetEndDate(2013, 10, 11);
            SetCash(100000);

            // Find more symbols here: http://quantconnect.com/data
            AddEquity("SPY", Resolution.Hour);
            AddEquity("IYM", Resolution.Hour);
            AddEquity("IYC", Resolution.Hour);
            AddEquity("IYK", Resolution.Hour);
            AddEquity("IYE", Resolution.Hour);
            AddEquity("IYF", Resolution.Hour);
            AddEquity("IYH", Resolution.Hour);
            AddEquity("IYR", Resolution.Hour);
            AddEquity("IYW", Resolution.Hour);
            AddEquity("IDU", Resolution.Hour);
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="slice">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice slice)
        {
            // Rebalance once per week
            if (Time.DayOfWeek != System.DayOfWeek.Monday)
                return;

            // Sell all in unfavorable market (spy sma(50) <= sma(200))
            // Pick top 6 ranked by momentum (SMA(50) / SMA(200) where SMA(50) > SMA(200)
            // Invest 100% of portfolio divided equally between picks

            if (!Portfolio.Invested)
            {
                SetHoldings(_spy, 1);
                Debug("Purchased Stock");
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            // TBD
            {"Total Trades", "1"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "264.583%"},
            {"Drawdown", "2.200%"},
            {"Expectancy", "0"},
            {"Net Profit", "1.668%"},
            {"Sharpe Ratio", "4.41"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0.007"},
            {"Beta", "76.354"},
            {"Annual Standard Deviation", "0.193"},
            {"Annual Variance", "0.037"},
            {"Information Ratio", "4.354"},
            {"Tracking Error", "0.193"},
            {"Treynor Ratio", "0.011"},
            {"Total Fees", "$3.27"}
        };
    }
}

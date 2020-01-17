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

using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Algorithm used for regression tests purposes
    /// </summary>
    /// <meta name="tag" content="regression test" />
    public class RegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);
            SetEndDate(2013, 10, 11);

            SetCash(10000000);

            // Find more symbols here: http://quantconnect.com/data
            AddSecurity(SecurityType.Equity, "SPY", Resolution.Tick);
            AddSecurity(SecurityType.Equity, "BAC", Resolution.Minute);
            AddSecurity(SecurityType.Equity, "AIG", Resolution.Hour);
            AddSecurity(SecurityType.Equity, "IBM", Resolution.Daily);
        }

        private DateTime lastTradeTradeBars;
        private DateTime lastTradeTicks;
        private TimeSpan tradeEvery = TimeSpan.FromMinutes(1);
        public void OnData(Slice data)
        {
            if (Time - lastTradeTradeBars < tradeEvery) return;
            lastTradeTradeBars = Time;

            foreach (var kvp in data.Bars)
            {
                var symbol = kvp.Key;
                var bar = kvp.Value;

                if (bar.Time.RoundDown(bar.Period) != bar.Time)
                {
                    // only trade on new data
                    continue;
                }

                var holdings = Portfolio[symbol];
                if (!holdings.Invested)
                {
                    MarketOrder(symbol, 10);
                }
                else
                {
                    MarketOrder(symbol, -holdings.Quantity);
                }
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "1638"},
            {"Average Win", "0.00%"},
            {"Average Loss", "0.00%"},
            {"Compounding Annual Return", "-3.958%"},
            {"Drawdown", "0.100%"},
            {"Expectancy", "-0.993"},
            {"Net Profit", "-0.054%"},
            {"Sharpe Ratio", "-28.435"},
            {"Probabilistic Sharpe Ratio", "9.727%"},
            {"Loss Rate", "100%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "2.23"},
            {"Alpha", "-0.023"},
            {"Beta", "-0.001"},
            {"Annual Standard Deviation", "0.001"},
            {"Annual Variance", "0"},
            {"Information Ratio", "-4.523"},
            {"Tracking Error", "0.193"},
            {"Treynor Ratio", "39.072"},
            {"Total Fees", "$5433.00"},
            {"Fitness Score", "0"},
            {"Kelly Criterion Estimate", "-2.696"},
            {"Kelly Criterion Probability Value", "0.545"},
            {"Sortino Ratio", "-128.598"},
            {"Return Over Maximum Drawdown", "-71.588"},
            {"Portfolio Turnover", "0.071"},
            {"Total Insights Generated", "5433"},
            {"Total Insights Closed", "5430"},
            {"Total Insights Analysis Completed", "5430"},
            {"Long Insight Count", "2715"},
            {"Short Insight Count", "5"},
            {"Long/Short Ratio", "54300%"},
            {"Estimated Monthly Alpha Value", "$80790.2326"},
            {"Total Accumulated Estimated Alpha Value", "$13913.8734"},
            {"Mean Population Estimated Insight Value", "$2.562408"},
            {"Mean Population Direction", "14.5532%"},
            {"Mean Population Magnitude", "0%"},
            {"Rolling Averaged Population Direction", "6.3635%"},
            {"Rolling Averaged Population Magnitude", "0%"},
            {"OrderListHash", "-2067187803"}
        };
    }
}

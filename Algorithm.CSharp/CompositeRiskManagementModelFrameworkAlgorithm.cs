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
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Execution;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Algorithm.Framework.Risk;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Show cases how to use the <see cref="CompositeRiskManagementModel"/> to define
    /// </summary>
    public class CompositeRiskManagementModelFrameworkAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        public override void Initialize()
        {
            // Set requested data resolution
            UniverseSettings.Resolution = Resolution.Minute;

            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            // set algorithm framework models
            SetUniverseSelection(new ManualUniverseSelectionModel(QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA)));
            SetAlpha(new ConstantAlphaModel(InsightType.Price, InsightDirection.Up, System.TimeSpan.FromMinutes(20), 0.025, null));
            SetPortfolioConstruction(new EqualWeightingPortfolioConstructionModel());
            SetExecution(new ImmediateExecutionModel());

            // define risk management model as a composite of several risk management models
            SetRiskManagement(new CompositeRiskManagementModel(
                new MaximumUnrealizedProfitPercentPerSecurity(0.01m),
                new MaximumDrawdownPercentPerSecurity(0.01m)
            ));
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
            {"Total Trades", "7"},
            {"Average Win", "1.02%"},
            {"Average Loss", "-1.01%"},
            {"Compounding Annual Return", "204.210%"},
            {"Drawdown", "2.200%"},
            {"Expectancy", "0.339"},
            {"Net Profit", "1.433%"},
            {"Sharpe Ratio", "4.318"},
            {"Probabilistic Sharpe Ratio", "64.587%"},
            {"Loss Rate", "33%"},
            {"Win Rate", "67%"},
            {"Profit-Loss Ratio", "1.01"},
            {"Alpha", "-0.108"},
            {"Beta", "0.969"},
            {"Annual Standard Deviation", "0.213"},
            {"Annual Variance", "0.045"},
            {"Information Ratio", "-16.827"},
            {"Tracking Error", "0.008"},
            {"Treynor Ratio", "0.95"},
            {"Total Fees", "$22.77"},
            {"Fitness Score", "0.999"},
            {"Kelly Criterion Estimate", "38.794"},
            {"Kelly Criterion Probability Value", "0.229"},
            {"Sortino Ratio", "79228162514264337593543950335"},
            {"Return Over Maximum Drawdown", "69.143"},
            {"Portfolio Turnover", "1.242"},
            {"Total Insights Generated", "100"},
            {"Total Insights Closed", "99"},
            {"Total Insights Analysis Completed", "99"},
            {"Long Insight Count", "100"},
            {"Short Insight Count", "0"},
            {"Long/Short Ratio", "100%"},
            {"Estimated Monthly Alpha Value", "$246137.8427"},
            {"Total Accumulated Estimated Alpha Value", "$39655.5413"},
            {"Mean Population Estimated Insight Value", "$400.561"},
            {"Mean Population Direction", "53.5354%"},
            {"Mean Population Magnitude", "53.5354%"},
            {"Rolling Averaged Population Direction", "59.0771%"},
            {"Rolling Averaged Population Magnitude", "59.0771%"}
        };
    }
}

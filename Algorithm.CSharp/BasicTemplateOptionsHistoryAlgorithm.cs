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
 *
*/

using System;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Securities.Option;
using QuantConnect.Securities;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This example demonstrates how to get access to options history for a given underlying equity security.
    /// </summary>
    public class BasicTemplateOptionsHistoryAlgorithm : QCAlgorithm
    {
        private const string UnderlyingTicker = "GOOG";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Equity, Market.USA);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.USA);

        public override void Initialize()
        {
            // this test opens position in the first day of trading, lives through stock split (7 for 1), and closes adjusted position on the second day
            SetStartDate(2015, 12, 24);
            SetEndDate(2015, 12, 24);
            SetCash(1000000);

            var equity = AddEquity(UnderlyingTicker);
            var option = AddOption(UnderlyingTicker);

            equity.SetDataNormalizationMode(DataNormalizationMode.Raw);
            option.PriceModel = OptionPriceModels.CrankNicolsonFD();
            //option.EnableGreekApproximation = true;

            option.SetFilter(-2, +2, TimeSpan.FromDays(00), TimeSpan.FromDays(15));

            SetBenchmark(equity.Symbol);
        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            if (!Portfolio.Invested)
            {
                foreach (var chain in slice.OptionChains)
                {
                    var underlying = Securities[chain.Key.Underlying];
                    foreach (var contract in chain.Value)
                    {
                        Log(String.Format(@"{0},Bid={1} Ask={2} Last={3} OI={4} σ={5:0.000} NPV={6:0.000} Δ={7:0.000} Γ={8:0.000} ν={9:0.000} ρ={10:0.00} Θ={11:0.00} IV={12:0.000}",
                             contract.Symbol.Value,
                             contract.BidPrice,
                             contract.AskPrice,
                             contract.LastPrice,
                             contract.OpenInterest,
                             underlying.VolatilityModel.Volatility,
                             contract.TheoreticalPrice,
                             contract.Greeks.Delta,
                             contract.Greeks.Gamma,
                             contract.Greeks.Vega,
                             contract.Greeks.Rho,
                             contract.Greeks.Theta / 365.0m,
                             contract.ImpliedVolatility));
                    }
                }
            }
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log(orderEvent.ToString());
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            foreach (var change in changes.AddedSecurities)
            {
                var history = History(change.Symbol, 10, Resolution.Hour);

                foreach (var data in history.OrderByDescending(x => x.Time).Take(3))
                {
                    Log("History: " + data.Symbol.Value + ": " + data.Time + " > " + data.Close);
                }
            }
        }
    }
}

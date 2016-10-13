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
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This example demonstrates how to add futures for a given underlying.
    /// It also shows how you can prefilter contracts easily based on expirations.
    /// It also shows how you can inspect the futures chain to pick a specific contract to trade.
    /// </summary>
    public class BasicTemplateFuturesAlgorithm : QCAlgorithm
    {
        // Oats futures
        private const string UnderlyingTicker = Futures.Indices.SP500EMini;
        public Symbol FuturesSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Future, Market.USA);

        public override void Initialize()
        {
            SetStartDate(2016, 08, 17);
            SetEndDate(2016, 08, 20);
            SetCash(1000000);

            var future = AddFuture(UnderlyingTicker);

            // set our expiry filter for this futures chain
            future.SetFilter(TimeSpan.Zero, TimeSpan.FromDays(365));

            var benchmark = AddEquity("SPY");
            SetBenchmark(benchmark.Symbol);
        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            if (!Portfolio.Invested)
            {
                FuturesChain chain;
                if (slice.FuturesChains.TryGetValue(FuturesSymbol, out chain))
                {
                    // find the front contract expiring no earlier than in 10 days
                    var contract = (
                        from futuresContract in chain.OrderBy(x => x.Expiry)
                        where futuresContract.Expiry > Time.Date.AddDays(10)
                        select futuresContract
                        ).FirstOrDefault();

                    // if found, trade it
                    if (contract != null)
                    {
                        MarketOrder(contract.Symbol, 1);
                    }
                }
            }
            else
            {
                Liquidate();
            }

            foreach (var kpv in slice.Bars)
            {
                Console.WriteLine("---> OnData: {0}, {1}, {2}", Time, kpv.Key.Value, kpv.Value.Close.ToString("0.0000"));
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
    }
}

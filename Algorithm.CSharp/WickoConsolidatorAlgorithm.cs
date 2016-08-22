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
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;

namespace QuantConnect.Algorithm.Examples
{
    public class WickoConsolidatorAlgorithm : QCAlgorithm
    {
        public override void Initialize()
        {
            SetStartDate(2014, 05, 01);
            SetEndDate(2014, 05, 15);

            AddSecurity(SecurityType.Forex, "EURUSD");

            var wickoClose = new WickoConsolidator(0.0001m);

            wickoClose.DataConsolidated += (sender, consolidated) =>
                HandleWickoClose(consolidated);

            SubscriptionManager.AddConsolidator("EURUSD", wickoClose);
        }

        public void OnData(TradeBars data)
        {
        }

        public void HandleWickoClose(WickoBar data)
        {
            if (!Portfolio.Invested)
                SetHoldings(data.Symbol, 1.0);

            Console.WriteLine("CLOSE - {0} - {1} {2}",
                data.Time.ToString("o"), data.Open, data.Close);
        }
    }
}

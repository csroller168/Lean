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
using NUnit.Framework;
using QuantConnect.Orders;

namespace QuantConnect.Tests.Common.Orders
{
    [TestFixture]
    public class StopMarketOrderTests
    {
        [Test]
        public void GetValuesReturnsQuantityTimesPrice()
        {
            const decimal price = 195m;
            var time = new DateTime(2016, 2, 4, 16, 0, 0).ConvertToUtc(TimeZones.NewYork);
            var order = new StopMarketOrder(Symbols.SPY, 100, price, time);
            var value = order.GetValue(price);
            Assert.AreEqual(price * order.Quantity, value);

            // the value is directional
            order.Quantity = -order.Quantity;
            value = order.GetValue(price);
            Assert.AreEqual(price * order.Quantity, value);
        }

        [Test]
        public void GetValueUsesBetterPrice()
        {
            const decimal price = 195m;
            const decimal delta = 1m;
            var time = new DateTime(2016, 2, 4, 16, 0, 0).ConvertToUtc(TimeZones.NewYork);
            var order = new StopMarketOrder(Symbols.SPY, 100, price - delta, time);
            var value = order.GetValue(price);
            Assert.AreEqual((price - delta) * order.Quantity, value);

            // the value is directional
            order.Quantity = -order.Quantity;
            order.StopPrice = price + delta;
            value = order.GetValue(price);
            Assert.AreEqual((price + delta) * order.Quantity, value);
        }
    }
}
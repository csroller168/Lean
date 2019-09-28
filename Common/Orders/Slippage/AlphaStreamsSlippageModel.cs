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

using QuantConnect.Securities;
using QuantConnect.Data.Market;

namespace QuantConnect.Orders.Slippage
{
    /// <summary>
    /// Represents a slippage model that uses a constant percentage of slip
    /// </summary>
    public class AlphaStreamsSlippageModel : ISlippageModel
    {
        private readonly decimal _slippagePercent = 0.001m;
        /// <summary>
        /// Initializes a new instance of the <see cref="ConstantSlippageModel"/> class
        /// </summary>
        /// <param name="slippagePercent">The slippage percent for each order. Percent is ranged 0 to 1.</param>
        public AlphaStreamsSlippageModel() {}

        /// <summary>
        /// Slippage Model. Return a decimal cash slippage approximation on the order.
        /// </summary>
        public decimal GetSlippageApproximation(Security asset, Order order)
        {
            var lastData = asset.GetLastData();
            if (lastData == null) return 0;

            if (lastData.DataType == MarketDataType.TradeBar)
            {
                return _slippagePercent * ((TradeBar)lastData).Close;
            }
            else
            {
                return 0;
            }
        }
    }
}
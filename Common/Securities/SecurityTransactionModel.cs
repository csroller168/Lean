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

using QuantConnect.Orders;
using QuantConnect.Securities.Interfaces;

namespace QuantConnect.Securities 
{
    /// <summary>
    /// Default security transaction model for user defined securities.
    /// </summary>
    public class SecurityTransactionModel : ISecurityTransactionModel
    {
        private readonly IOrderFillModel _orderFillModel;
        private readonly IOrderFeeModel _orderFeeModel;
        private readonly ISlippageModel _slippageModel;

        /// <summary>
        /// Initializes a new default instance of the <see cref="SecurityTransactionModel"/> class.
        /// This will use default slippage and fill models.
        /// </summary>
        public SecurityTransactionModel()
        {
            _slippageModel = new DefaultSlippageModel();
            _orderFillModel = new DefaultOrderFillModel(this);
            _orderFeeModel = new ConstantOrderFeeModel(0);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityTransactionManager"/> class
        /// </summary>
        /// <param name="orderFillModel">The fill model to use</param>
        /// <param name="orderFeeModel">The order fee model to use</param>
        /// <param name="slippageModel">The slippage model to use</param>
        public SecurityTransactionModel(IOrderFillModel orderFillModel, IOrderFeeModel orderFeeModel, ISlippageModel slippageModel)
        {
            _orderFillModel = orderFillModel;
            _orderFeeModel = orderFeeModel;
            _slippageModel = slippageModel;
        }

        /// <summary>
        /// Default market fill model for the base security class. Fills at the last traded price.
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        /// <seealso cref="StopMarketFill(Security, StopMarketOrder)"/>
        /// <seealso cref="LimitFill(Security, LimitOrder)"/>
        public virtual OrderEvent MarketFill(Security asset, MarketOrder order)
        {
            return _orderFillModel.MarketFill(asset, order);
        }

        /// <summary>
        /// Default stop fill model implementation in base class security. (Stop Market Order Type)
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        /// <seealso cref="MarketFill(Security, MarketOrder)"/>
        /// <seealso cref="LimitFill(Security, LimitOrder)"/>
        public virtual OrderEvent StopMarketFill(Security asset, StopMarketOrder order)
        {
            return _orderFillModel.StopMarketFill(asset, order);
        }

        /// <summary>
        /// Default stop limit fill model implementation in base class security. (Stop Limit Order Type)
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        /// <seealso cref="StopMarketFill(Security, StopMarketOrder)"/>
        /// <seealso cref="LimitFill(Security, LimitOrder)"/>
        /// <remarks>
        ///     There is no good way to model limit orders with OHLC because we never know whether the market has 
        ///     gapped past our fill price. We have to make the assumption of a fluid, high volume market.
        /// 
        ///     Stop limit orders we also can't be sure of the order of the H - L values for the limit fill. The assumption
        ///     was made the limit fill will be done with closing price of the bar after the stop has been triggered..
        /// </remarks>
        public virtual OrderEvent StopLimitFill(Security asset, StopLimitOrder order)
        {
            return _orderFillModel.StopLimitFill(asset, order);
        }

        /// <summary>
        /// Default limit order fill model in the base security class.
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        /// <seealso cref="StopMarketFill(Security, StopMarketOrder)"/>
        /// <seealso cref="MarketFill(Security, MarketOrder)"/>
        public virtual OrderEvent LimitFill(Security asset, LimitOrder order)
        {
            return _orderFillModel.LimitFill(asset, order);
        }

        /// <summary>
        /// Market on Open Fill Model. Return an order event with the fill details
        /// </summary>
        /// <param name="asset">Asset we're trading with this order</param>
        /// <param name="order">Order to be filled</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public OrderEvent MarketOnOpenFill(Security asset, MarketOnOpenOrder order)
        {
            return _orderFillModel.MarketOnOpenFill(asset, order);
        }

        /// <summary>
        /// Market on Close Fill Model. Return an order event with the fill details
        /// </summary>
        /// <param name="asset">Asset we're trading with this order</param>
        /// <param name="order">Order to be filled</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public OrderEvent MarketOnCloseFill(Security asset, MarketOnCloseOrder order)
        {
            return _orderFillModel.MarketOnCloseFill(asset, order);
        }

        /// <summary>
        /// Get the slippage approximation for this order
        /// </summary>
        /// <param name="security">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>decimal approximation for slippage</returns>
        public virtual decimal GetSlippageApproximation(Security security, Order order)
        {
            return _slippageModel.GetSlippageApproximation(security, order);
        }

        /// <summary>
        /// Default implementation returns 0 for fees.
        /// </summary>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to compute fees for</param>
        /// <returns>The cost of the order in units of the account currency</returns>
        public virtual decimal GetOrderFee(Security security, Order order)
        {
            return _orderFeeModel.GetOrderFee(security, order);
        }
    }
}

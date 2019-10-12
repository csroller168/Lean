/*
 * Copyright Chris Short 2019
*/

using System;
using System.Collections.Generic;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Execution;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Algorithm.Framework.Risk;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Orders;
using QuantConnect.Interfaces;

using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System.Linq;
using System.Threading;
using QuantConnect.Orders.Slippage;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template framework algorithm uses framework components to define the algorithm.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class PairsTradingAlgorithm : QCAlgorithm
    {
        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            // Set requested data resolution
            UniverseSettings.Resolution = Resolution.Minute;

            SetStartDate(2018, 10, 08);  //Set Start Date
            SetEndDate(2018, 10, 16);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            var resolution = LiveMode ? Resolution.Minute : Resolution.Daily;
            AddEquity("TLT", resolution, null, true);
        }

        public override void OnData(Slice slice)
        {
            SetHoldings("TLT", 1m, false);
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status.IsFill())
            {
                Debug($"Purchased Stock: {orderEvent.Symbol}");
            }
        }
    }
}

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
        // TODO: test adding SHY to universe (short term bond for inverted yield curve scenario)
        // TODO: find a way to avoid high momentum due to big temporary drop 60 days ago (if that matters)
        // TODO: use trading day's open price as momenum numerator

        private static readonly int slowDays = 60;
        private static readonly decimal flipMargin = 0.035m;
        private string symbolInMarket = string.Empty;
        private readonly ISlippageModel SlippageModel = new ConstantSlippageModel(0.002m);

        public override void Initialize()
        {
            // Set requested data resolution
            UniverseSettings.Resolution = Resolution.Minute;

            SetStartDate(2019, 8, 5);
            SetEndDate(2019, 8, 30);
            SetCash(100000);

            var resolution = LiveMode ? Resolution.Minute : Resolution.Daily;
            var spy = AddEquity("SPY", resolution, null, true);
        }

        public override void OnData(Slice slice)
        {
            SetHoldings("SPY", 1m, false);
        }

        private void PlotPoints(decimal spyMomentum, decimal tltMomentum)
        {
            Plot("momentum", "spyMomentum", (spyMomentum - 1) * 1);
            Plot("momentum", "tltMomentum", (tltMomentum - 1) * 1);

            Plot("price", "SPY", Securities["SPY"].Price);
            Plot("price", "tlt", Securities["TLT"].Price);

            Plot("leverage", "cash", Portfolio.Cash);
            Plot("leverage", "holdings", Portfolio.TotalHoldingsValue);
        }

        private void Rebalance(string buySymbol, string sellSymbol)
        {

            if (buySymbol == symbolInMarket && Portfolio.Cash < Portfolio.TotalHoldingsValue)
                return;

            Liquidate(sellSymbol);
            SetHoldings(buySymbol, 1m, false);
            symbolInMarket = buySymbol;
        }

        private int sharesToBuy(string symbol)
        {
            var value = Portfolio.Cash + Portfolio.TotalHoldingsValue;
            return (int)(value / Securities[symbol].Price);
        }

        private decimal Momentum(string symbol, int days)
        {
            var h = History<TradeBar>(symbol, days);
            //Debug($"{symbol} momentum = {Securities[symbol].Price} / {h.First().Close}");
            return Securities[symbol].Price / h.First().Close;
        }
    }
}

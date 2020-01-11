/*
 * Copyright Chris Short 2019
*/

using System;
using QuantConnect.Orders;
using QuantConnect.Data;
using System.Linq;
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
        //          test ratio of sma or ema from recent days to past days
        // TODO: use trading day's open price as momentum numerator
        // todo: in onData, if not invested, set tolerance to 0
        // todo: get email notification working:  (ERROR:: Messaging.SendNotification(): Send not implemented for notification of type: NotificationEmail)
        // todo: MFA on aws

        private static readonly int slowDays = 60;
        private static readonly int fastDays = 8;
        private static readonly decimal flipMargin = 0.035m;
        private string symbolInMarket = string.Empty;
        private readonly ISlippageModel SlippageModel = new ConstantSlippageModel(0.002m);

        public override void Initialize()
        {
            // Set requested data resolution (NOTE: only needed for IB)
            UniverseSettings.Resolution = Resolution.Minute;

            SetStartDate(2003, 8, 1);
            SetEndDate(2020, 1, 8);
            SetCash(100000);

            var resolution = LiveMode ? Resolution.Minute : Resolution.Daily;
            var spy = AddEquity("SPY", resolution, null, true);
            spy.SetSlippageModel(SlippageModel);
            var tlt = AddEquity("TLT", resolution, null, true);
            tlt.SetSlippageModel(SlippageModel);

            SetSecurityInitializer(x => x.SetDataNormalizationMode(DataNormalizationMode.Raw));
        }

        public override void OnData(Slice slice)
        {
            var spyMomentum = Momentum("SPY", slowDays);
            var tltMomentum = Momentum("TLT", slowDays);

            PlotPoints(spyMomentum, tltMomentum);

            if (spyMomentum > tltMomentum + flipMargin)
            {
                Rebalance("SPY", "TLT");
            }
            else if (tltMomentum > spyMomentum + flipMargin)
            {
                Rebalance("TLT", "SPY");
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if(orderEvent.Status == OrderStatus.Filled
                && orderEvent.Direction == OrderDirection.Buy)
            {
                var address = "chrisshort168@gmail.com";
                var subject = "Trading app notification";
                var body = $"The app is now long {orderEvent.Symbol}";
                Notify.Email(address, subject, body);
            }
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

        private decimal Momentum(string symbol, int days)
        {
            var numerator = Math.Min(Securities[symbol].Open, Securities[symbol].Price);
            var pastPrices = History(symbol, TimeSpan.FromDays(days + fastDays / 2), Resolution.Daily).ToList();
            var denominator = pastPrices.Take(fastDays).Average(x => x.Close);
            return numerator / denominator;
        }
    }
}

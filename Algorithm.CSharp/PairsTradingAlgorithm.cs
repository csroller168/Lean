/*
 * Copyright Chris Short 2019
*/

using System;
using QuantConnect.Orders;
using QuantConnect.Data;
using System.Linq;
using QuantConnect.Orders.Slippage;
using System.Collections.Generic;

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
        // todo: in onData, if not invested, set tolerance to 0
        // todo: get email notification working:  (ERROR:: Messaging.SendNotification(): Send not implemented for notification of type: NotificationEmail)
        // todo: MFA on aws

        private static readonly int slowDays = 60;
        private static readonly int fastDays = 8;
        private static readonly decimal flipMargin = 0.035m;
        private static readonly List<string> equities = new List<string> { "SPY", "TLT" };
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
            equities.ForEach(x =>
            {
                AddEquity(x, resolution, null, true)
                    .SetSlippageModel(SlippageModel);
            });

            SetSecurityInitializer(x => x.SetDataNormalizationMode(DataNormalizationMode.Raw));
        }

        public override void OnData(Slice slice)
        {
            PlotPoints();

            var momentums = equities
                .Select(x => new KeyValuePair<string, decimal>(x, Momentum(x, slowDays)))
                .OrderByDescending(x => x.Value)
                .ToList();
            var momentumInMarket = 0m;
            if (!string.IsNullOrEmpty(symbolInMarket))
            {
                momentumInMarket = momentums.Where(x => x.Key == symbolInMarket).Single().Value;
            }

            if(momentums.First().Value > momentumInMarket + flipMargin)
            {
                Rebalance(momentums[0].Key, symbolInMarket);
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status == OrderStatus.Filled
                && orderEvent.Direction == OrderDirection.Buy)
            {
                var address = "chrisshort168@gmail.com";
                var subject = "Trading app notification";
                var body = $"The app is now long {orderEvent.Symbol}";
                Notify.Email(address, subject, body);
            }
        }

        private void PlotPoints()
        {
            equities.ForEach(x =>
            {
                Plot("momentum", $"{x}Momentum", (Momentum(x, slowDays) - 1));
                Plot("price", x, Securities[x].Price);
            });

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

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
        // TODOS:
        // bugs
            // get email notification working:  (ERROR:: Messaging.SendNotification(): Send not implemented for notification of type: NotificationEmail)
        // optimize
            // test different sets of equities
            // test adding SHY to universe (short term bond for inverted yield curve scenario)
            // re-assess momentum denominator
            // liquidate if all momentums < 0 (and ema not trend up?)?
        // deployment
            // MFA on aws
            // trade with live $

        private static readonly int slowDays = 60;
        private static readonly int fastDays = 8;
        private static readonly decimal flipMargin = 0.035m;
        private static readonly List<string> universe = new List<string> { "SPY", "TLT" };
        private readonly ISlippageModel SlippageModel = new ConstantSlippageModel(0.002m);

        private string SymbolInMarket => Portfolio.OrderByDescending(y => y.Value.HoldingsValue).First().Key;

        public override void Initialize()
        {
            // Set requested data resolution (NOTE: only needed for IB)
            UniverseSettings.Resolution = Resolution.Minute;

            SetStartDate(2003, 8, 1);
            SetEndDate(2020, 1, 8);
            SetCash(100000);

            var resolution = LiveMode ? Resolution.Minute : Resolution.Daily;
            universe.ForEach(x =>
            {
                var equity = AddEquity(x, resolution, null, true);
                equity.SetSlippageModel(SlippageModel);
            });

            SetSecurityInitializer(x => x.SetDataNormalizationMode(DataNormalizationMode.Raw));
        }

        public override void OnData(Slice slice)
        {
            PlotPoints();

            var momentums = universe
                .Select(x => new { Symbol = x, Momentum = Momentum(x, slowDays) })
                .OrderByDescending(x => x.Momentum);
            var selection = momentums.First();

            if (!Portfolio.Invested)
            {
                Rebalance(selection.Symbol);
                return;
            }
                
            if(Portfolio[selection.Symbol].Invested)
                return;

            var momentumInMarket = momentums
                .Single(x => x.Symbol == SymbolInMarket)
                .Momentum;
            if (selection.Momentum > momentumInMarket + flipMargin)
            {
                Rebalance(selection.Symbol);
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
            universe.ForEach(x =>
            {
                Plot("momentum", $"{x}Momentum", Momentum(x, slowDays) - 1);
                Plot("price", x, Securities[x].Price);                
            });

            Plot("leverage", "cash", Portfolio.Cash);
            Plot("leverage", "holdings", Portfolio.TotalHoldingsValue);
        }

        private void Rebalance(string buySymbol)
        {
            Liquidate();
            SetHoldings(buySymbol, 1m, false);
        }

        private decimal Momentum(string symbol, int days)
        {
            var numerator = Math.Min(Securities[symbol].Open, Securities[symbol].Price);
            var denominator = History(symbol, TimeSpan.FromDays(days + fastDays / 2), Resolution.Daily)
                .Take(fastDays)
                .Average(x => x.Close);
            return numerator / denominator;
        }
    }
}

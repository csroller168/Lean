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
        // strategic plans:
            // start building collection of indicator delegates per ticker
            // find a better measure of momentum
                // maybe compare highest and lowest N prices over short and long period?
                // or average of short period / average of past short period
                // maybe measure momentum velocity, not just momentum
                // bollinger bands or one of these: https://www.investopedia.com/ask/answers/05/measuringmomentum.asp
                    // or... maybe compute all, count the signals, and take whomever has most
                    // take top N stocks with decreasing % of portfolio (or base % on num signals)
                    // add my own "signal" of no big drop in last N days
                // note: some of these may perform better in sideways markets vs. rocky ones.  assess, try to identify and switch
            // Test a more diverse group of etfs, including different classes (equities, fixed income, commodities...)
            // try picking multiple etfs
                // possibly separate etfs into classes, then pick top 1 from each class
        // bugs
            // get email notification working:  (ERROR:: Messaging.SendNotification(): Send not implemented for notification of type: NotificationEmail)
        // optimize
            // liquidate if all momentums < 0 (and ema not trend up?)?
            // test closing positions overnight
        // deployment
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
                
            if(Portfolio[selection.Symbol].Invested)
                return;

            var momentumInMarket = momentums
                .Single(x => x.Symbol == SymbolInMarket)
                .Momentum;
            if (selection.Momentum > momentumInMarket + flipMargin
                || !Portfolio.Invested)
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
                Plot("invested", x, Securities[x].Invested ? 1 : 0);
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

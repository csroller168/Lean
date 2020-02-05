/*
 * Copyright Chris Short 2019
*/

using System;
using QuantConnect.Orders;
using QuantConnect.Data;
using System.Linq;
using QuantConnect.Orders.Slippage;
using System.Collections.Generic;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template framework algorithm uses framework components to define the algorithm.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class MultiIndicatorAlgorithm : QCAlgorithm
    {
        // TODOS:
        // strategic plans:
            // start building collection of indicator delegates per ticker
            // find a better measure of momentum
                // macd: https://tradingsim.com/blog/macd/#Chapter_1_What_is_the_MACD/
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


        private static readonly int slowDays = 26;
        private static readonly int fastDays = 12;
        private static readonly List<string> universe = new List<string> { "SPY", "TLT" };
        private readonly ISlippageModel SlippageModel = new ConstantSlippageModel(0.002m);
        private Dictionary<string, MovingAverageConvergenceDivergence> Indicators = new Dictionary<string, MovingAverageConvergenceDivergence>();

        public override void Initialize()
        {
            // Set requested data resolution (NOTE: only needed for IB)
            UniverseSettings.Resolution = Resolution.Minute;

            SetStartDate(2019, 8, 1);
            SetEndDate(2020, 1, 8);
            SetCash(100000);
            EnableAutomaticIndicatorWarmUp = true;

            var resolution = LiveMode ? Resolution.Minute : Resolution.Daily;
            universe.ForEach(x =>
            {
                var equity = AddEquity(x, resolution, null, true);
                equity.SetSlippageModel(SlippageModel);
                Indicators[x] = MACD(x, fastDays, slowDays, 9, MovingAverageType.Exponential, Resolution.Daily);
            });

            SetSecurityInitializer(x => x.SetDataNormalizationMode(DataNormalizationMode.Raw));
        }

        public override void OnData(Slice slice)
        {
            PlotPoints();

            var momentums = universe
                .Select(x => new { Symbol = x, Momentum = Momentum(x) })
                .OrderByDescending(x => x.Momentum);
            var selection = momentums.First();

            if (Portfolio[selection.Symbol].Invested)
                return;

            Rebalance(selection.Symbol);
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
                Plot($"{x}Macd", Indicators[x]);
                Plot($"{x}Signal", Indicators[x].Signal);
                Plot($"{x}Histogram", Indicators[x].Histogram);
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

        private decimal Momentum(string symbol)
        {
            return Indicators[symbol].Histogram > 0
                ? (Indicators[symbol]/Indicators[symbol].Histogram)-1
                : 0m;
        }
    }
}

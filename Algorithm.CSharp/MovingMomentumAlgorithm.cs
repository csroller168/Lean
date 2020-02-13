/*
 * Copyright Chris Short 2019
*/

using QuantConnect.Orders;
using QuantConnect.Data;
using QuantConnect.Orders.Slippage;
using System.Collections.Generic;
using QuantConnect.Indicators;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    public class MovingMomentumAlgorithm : QCAlgorithm
    {
        // TODOS:
        // optimize
            // https://docs.google.com/spreadsheets/d/1i3Mru0C7E7QxuyxgKxuoO1Pa4keSAmlGCehmA2a7g88/edit#gid=138205234
        // bugs
            // algo isn't using all cash
            // get email notification working:  (ERROR:: Messaging.SendNotification(): Send not implemented for notification of type: NotificationEmail)
        // deployment
            // trade with live $

        private static readonly int slowMacdDays = 26;
        private static readonly int fastMacdDays = 12;
        private static readonly int signalMacdDays = 9;
        private static readonly int slowSmaDays = 150;
        private static readonly int fastSmaDays = 20;
        private static readonly int stoPeriod = 20;
        private static readonly List<string> universe = new List<string>
        {   
            "IEF", // treasuries
            "TLT",
            "SHY", 
            "XLB", // etfs
            "XLE",
            "XLF",
            "XLI",
            "XLK",
            "XLP",
            "XLU",
            "XLV",
            "XLY"
        };
        private readonly ISlippageModel SlippageModel = new ConstantSlippageModel(0.002m);
        private Dictionary<string, MovingAverageConvergenceDivergence> Macds = new Dictionary<string, MovingAverageConvergenceDivergence>();
        private Dictionary<string, SimpleMovingAverage> SlowSmas = new Dictionary<string, SimpleMovingAverage>();
        private Dictionary<string, SimpleMovingAverage> FastSmas = new Dictionary<string, SimpleMovingAverage>();
        private Dictionary<string, Stochastic> Stos = new Dictionary<string, Stochastic>();

        public override void Initialize()
        {
            // Set requested data resolution (NOTE: only needed for IB)
            UniverseSettings.Resolution = Resolution.Minute;
            SetBenchmark("SPY");

            SetStartDate(2003, 8, 1);
            SetEndDate(2020, 1, 8);
            SetCash(100000);
            EnableAutomaticIndicatorWarmUp = true;

            var resolution = LiveMode ? Resolution.Minute : Resolution.Daily;
            universe.ForEach(x =>
            {
                var equity = AddEquity(x, resolution, null, true);
                equity.SetSlippageModel(SlippageModel);
                Macds[x] = MACD(x, fastMacdDays, slowMacdDays, signalMacdDays, MovingAverageType.Exponential, Resolution.Daily);
                SlowSmas[x] = SMA(x, slowSmaDays, Resolution.Daily);
                FastSmas[x] = SMA(x, fastSmaDays, Resolution.Daily);
                Stos[x] = STO(x, stoPeriod);
            });

            SetSecurityInitializer(x => x.SetDataNormalizationMode(DataNormalizationMode.Raw));
        }

        public override void OnData(Slice slice)
        {
            PlotPoints();
            var toSell = universe
                .Where(x => SellSignal(x));
            var toBuy = universe
                .Where(x => BuySignal(x));
            var toOwn = toBuy
                .Union(Portfolio.Securities.Select(y => y.ToString()))
                .Except(toSell);
            
            foreach(var symbol in toSell)
            {
                Liquidate(symbol);
            }
            if(toBuy.Any() || toSell.Any())
            {
                foreach (var symbol in toOwn)
                {
                    var pct = 0.98m / toOwn.Count();
                    SetHoldings(symbol, pct);
                }
            }
        }

        private bool BuySignal(string symbol)
        {
            return !Portfolio[symbol].Invested
                && MacdBuySignal(symbol)
                && StoBuySignal(symbol)
                && SmaBuySignal(symbol);
        }

        private bool SellSignal(string symbol)
        {
            return Portfolio[symbol].Invested
                && MacdSellSignal(symbol)
                && StoSellSignal(symbol)
                && SmaSellSignal(symbol);
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
            //universe.ForEach(x =>
            //{
            //    Plot($"{x}-MACD", "macd", Macds[x]);
            //    Plot($"{x}-MACD", "signal", Macds[x].Signal);
            //    Plot($"{x}-MACD", "signal", Macds[x].Histogram);
            //    Plot("price", x, Securities[x].Price);
            //    Plot("invested", x, Securities[x].Invested ? 1 : 0);
            //});

            Plot("leverage", "cash", Portfolio.Cash);
            Plot("leverage", "holdings", Portfolio.TotalHoldingsValue);
        }

        private bool MacdBuySignal(string symbol)
        {
            return Macds[symbol].Histogram > 0;
        }

        private bool SmaBuySignal(string symbol)
        {
            return FastSmas[symbol] > SlowSmas[symbol];
        }

        private bool StoBuySignal(string signal)
        {
            return Stos[signal] < 20;
        }

        private bool MacdSellSignal(string symbol)
        {
            return Macds[symbol].Histogram < 0;
        }

        private bool SmaSellSignal(string symbol)
        {
            return FastSmas[symbol] < SlowSmas[symbol];
        }

        private bool StoSellSignal(string signal)
        {
            return Stos[signal] > 80;
        }
    }
}

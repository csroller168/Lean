/*
 * Copyright Chris Short 2019
*/

using QuantConnect.Orders;
using QuantConnect.Data;
using QuantConnect.Orders.Slippage;
using QuantConnect.Algorithm.Framework.Portfolio;
using System.Collections.Generic;
using QuantConnect.Indicators;
using System.Linq;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    public class MovingMomentumAlgorithm : QCAlgorithm
    {
        // TODOS:
        // optimize
            // https://docs.google.com/spreadsheets/d/1i3Mru0C7E7QxuyxgKxuoO1Pa4keSAmlGCehmA2a7g88/edit#gid=138205234
        // bugs
            // manually manage indicators with history at each OnData call.  the built-in updates don't work
            // use deployed custom emailer
        // deployment
            // trade with live $
            // if I eventually make this into a business, integrate directly with alpaca

        private static readonly int slowMacdDays = 26;
        private static readonly int fastMacdDays = 12;
        private static readonly int signalMacdDays = 9;
        private static readonly int slowSmaDays = 150;
        private static readonly int fastSmaDays = 3; // 20;
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
        private DateTime? lastRun = null;
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

            //SetStartDate(2003, 8, 1);
            SetStartDate(2019, 12, 2);
            SetEndDate(2020, 1, 8);
            SetCash(100000);
            EnableAutomaticIndicatorWarmUp = false;

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
            if (TradedToday())
                return;

            UpdateIndicators();
            PlotPoints();
            var toSell = universe
                .Where(x => Portfolio[x].Invested && SellSignal(x));
            var toBuy = universe
                .Where(x => !Portfolio[x].Invested && BuySignal(x));
            var toOwn = toBuy
                .Union(universe.Where(x => Portfolio[x].Invested))
                .Except(toSell);
            
            if(toBuy.Any() || toSell.Any())
            {
                foreach (var symbol in toSell)
                {
                    Liquidate(symbol);
                }
                var pct = 0.98m / toOwn.Count();
                var targets = toOwn.Select(x => new PortfolioTarget(x, pct));
                SetHoldings(targets.ToList());
            }
        }

        private bool TradedToday()
        {
            if (lastRun?.Day == Time.Day)
                return true;

            lastRun = Time;
            return false;
        }

        private void UpdateIndicators()
        {
            foreach(var symbol in universe)
            {
                Macds[symbol].Reset();
                Stos[symbol].Reset();
                FastSmas[symbol].Reset();
                SlowSmas[symbol].Reset();
            }
            var allHistory = History(slowSmaDays, Resolution.Daily);
            allHistory.PushThrough(data => WarmIndicators(data));
        }

        private void WarmIndicators(BaseData data)
        {
            var dp = new IndicatorDataPoint
            {
                Value = data.Price,
                DataType = data.DataType,
                Symbol = data.Symbol,
                Time = data.Time,
                EndTime = data.EndTime
            };
            Macds[data.Symbol].Update(dp);
            Stos[data.Symbol].Update(data);
            SlowSmas[data.Symbol].Update(dp);
            FastSmas[data.Symbol].Update(dp);
        }

        private bool BuySignal(string symbol)
        {
            return
                MacdBuySignal(symbol)
                && StoBuySignal(symbol)
                && SmaBuySignal(symbol);
        }

        private bool SellSignal(string symbol)
        {
            return
                MacdSellSignal(symbol)
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

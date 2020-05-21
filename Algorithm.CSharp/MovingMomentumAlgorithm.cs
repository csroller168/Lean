/*
 * Copyright Chris Short 2019
*/

using QuantConnect.Data;
using QuantConnect.Data.Custom.CBOE;
using QuantConnect.Orders.Slippage;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Data.Market;
using System.Collections.Generic;
using System.Linq;
using System;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Brokerages;

namespace QuantConnect.Algorithm.CSharp
{
    public class MovingMomentumAlgorithm : QCAlgorithm
    {
        // TODOS:
        // optimize
        //      https://docs.google.com/spreadsheets/d/1i3Mru0C7E7QxuyxgKxuoO1Pa4keSAmlGCehmA2a7g88/edit#gid=138205234
        //      sell on negative macd histogram slope
        // deployment
        //      trade with live $
        //      if I eventually make this into a business, integrate directly with alpaca

        private static readonly int fastMacdDays = 5;
        private static readonly int slowMacdDays = 35;
        private static readonly int signalMacdDays = 5;
        private static readonly int slowSmaDays = 400;
        private static readonly int fastSmaDays = 100;
        private static readonly int stoLookbackPeriod = 20;
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
            "XLY",
            "GLD", // other
            "ICF",
            "IHF",
            "PBJ",
            "VDC"
        };
        private DateTime? lastRun = null;
        private int numAttemptsToTrade = 0;
        private readonly ISlippageModel SlippageModel = new ConstantSlippageModel(0.002m);
        private Dictionary<string, List<BaseData>> histories = new Dictionary<string, List<BaseData>>();
        private Dictionary<string, decimal> macds = new Dictionary<string, decimal>();
        private Dictionary<string, decimal> stos = new Dictionary<string, decimal>();
        //private Dictionary<string, decimal> stdDevs = new Dictionary<string, decimal>();
        private static object mutexLock = new object();
        private Symbol _cboeVix;
        private CBOE _vix = null;

        public override void Initialize()
        {
            // Set requested data resolution (NOTE: only needed for IB)
            UniverseSettings.Resolution = Resolution.Daily;
            UniverseSettings.FillForward = true;
            SetBenchmark("SPY");

            SetStartDate(2003, 8, 1);
            SetEndDate(2003, 10, 27);
            //SetEndDate(2020, 3, 27);
            //SetStartDate(2007, 3, 12);
            //SetEndDate(2012, 10, 8);
            SetCash(100000);

            SetBrokerageModel(BrokerageName.AlphaStreams);

            var resolution = LiveMode ? Resolution.Minute : Resolution.Daily;
            universe.ForEach(x =>
            {
                var equity = AddEquity(x, resolution, null, true);
                equity.SetSlippageModel(SlippageModel);
            });
            _cboeVix = AddData<CBOE>("VIX", Resolution.Daily).Symbol;
        }

        public override void OnData(Slice slice)
        {
            HandleVixData(slice);
            if (!IsAllowedToTrade(slice))
            {
                return;
            }

            try
            {
                UpdateIndicatorData(slice);
                PlotPoints();
                var toSell = universe
                    .Where(x => Portfolio[x].Invested && SellSignal(x));
                var toBuy = universe
                    .Where(x => !Portfolio[x].Invested && BuySignal(x))
                    .ToList();
                var toOwn = toBuy
                    .Union(universe.Where(x => Portfolio[x].Invested))
                    .Except(toSell)
                    .ToList();

                if (NeedToRebalance(toBuy, toSell, toOwn, slice))
                {
                    EmitAllInsights(toBuy, toSell);
                    foreach (var symbol in toSell)
                    {
                        Liquidate(symbol);
                    }
                    var targets = GetPortfolioTargets(toOwn, slice);
                    SetHoldings(targets.ToList());
                }

                var longs = toOwn.Any() ? string.Join(",", toOwn) : "nothing";
                SendEmailNotification($"{longs}");
            }
            catch (Exception e)
            {
                lastRun = null;
                SendEmailNotification($"\"{e.Message}\"");
            }
        }

        private IEnumerable<PortfolioTarget> GetPortfolioTargets(List<string> toOwn, Slice slice)
        {
            var cashPct = GetCashPercent(slice);
            var targets = toOwn.Select(x => new PortfolioTarget(
                x, TargetPctToOwn(x, cashPct, toOwn)));
            return targets;
        }

        private bool NeedToRebalance(
            IEnumerable<string> toBuy,
            IEnumerable<string> toSell,
            List<string> toOwn,
            Slice slice)
        {
            var needToRebal = toBuy.Any() || toSell.Any() || NeedToReactToVix(slice);
            return needToRebal;

            //if (needToRebal || toOwn.Count < 3)
            //    return needToRebal;

            //var currentRanks = Portfolio
            //    .Securities
            //    .Where(x => x.Value.HoldStock)
            //    .Select(x => x.Key)
            //    .OrderByDescending(x => Portfolio[x].AbsoluteHoldingsValue)
            //    .ToList();
            //var targets = GetPortfolioTargets(toOwn, slice)
            //    .OrderByDescending(x => x.Quantity)
            //    .Select(x => x.Symbol)
            //    .ToList();
            //needToRebal = Enumerable.Range(0, Math.Max(targets.Count() - 1, 0))
            //    .Any(i => targets[i].Value != currentRanks[i].Value);

            //return needToRebal;
        }

        private decimal RankScore(List<string> toOwn, string target)
        {
            const double scaleFactor = 0.8;
            var result = toOwn.Count * Math.Pow(scaleFactor, toOwn.IndexOf(target));
            return (decimal)result;
        }

        private decimal TargetPctToOwn(string symbol, decimal cashPct, List<string> toOwn)
        {
            var pct = (1.0m - cashPct) / toOwn.Count();
            //var orderedSymbolsToOwn = toOwn.OrderBy(x => stdDevs[x]).ToList();
            //var stoPct = RankScore(orderedSymbolsToOwn, symbol) / toOwn.Sum(x => RankScore(orderedSymbolsToOwn, x));
            //var pct = (1.0m - cashPct) * stoPct;

            return pct;
        }

        private decimal GetCashPercent(Slice slice)
        {
            var cashPct = (Portfolio.Max(x => x.Value.Price)
                        + Portfolio.Min(x => x.Value.Price)) / Portfolio.TotalPortfolioValue;
            if (_vix?.Price > 40) cashPct = 0.45m;
            return cashPct;
        }

        private void HandleVixData(Slice slice)
        {
            if (slice.ContainsKey(_cboeVix))
            {
                _vix = slice.Get<CBOE>(_cboeVix);
                Plot("VIX", "price", _vix.Price);
                Log($"VIX: {_vix}");
            }
        }

        private bool NeedToReactToVix(Slice currentSlice)
        {
            var desiredCashPct = GetCashPercent(currentSlice);
            var currentCashPct = Portfolio.Cash / Portfolio.TotalPortfolioValue;
            var diff = Math.Abs(desiredCashPct - currentCashPct);
            return diff > .15m;
        }

        private void EmitAllInsights(List<string> toBuy, IEnumerable<string> toSell)
        {
            var insights = toBuy
                .Select(x => Insight.Price(x, Resolution.Daily, 10, InsightDirection.Up))
                .Union(toSell.Select(x => Insight.Price(x, Resolution.Daily, 10, InsightDirection.Down)))
                .ToArray();
            EmitInsights(insights);
        }

        private bool IsAllowedToTrade(Slice slice)
        {
            if(!LiveMode)
            {
                if (lastRun?.Day == Time.Day)
                    return false;
                lastRun = Time;
                return true;
            }

            lock(mutexLock)
            {
                if (lastRun?.Day == Time.Day)
                    return false;

                if(slice.Count < universe.Count
                    && numAttemptsToTrade < universe.Count)
                {
                    numAttemptsToTrade++;
                    return false;
                }

                lastRun = Time;
                return true;
            }
        }

        private void UpdateIndicatorData(Slice currentSlice)
        {
            var localHistories = History(slowSmaDays, Resolution.Daily).ToList();
            foreach (var symbol in universe)
            {
                if (!currentSlice.ContainsKey(symbol))
                    continue;

                histories[symbol] = localHistories
                    .Where(x => x.ContainsKey(symbol))
                    .Select(x => x[symbol] as BaseData)
                    .Union(new[] { currentSlice[symbol] as BaseData })
                    .OrderByDescending(x => x.Time)
                    .ToList();
                macds[symbol] = MacdHistogram(symbol);

                var stoHistories = History<TradeBar>(symbol, stoLookbackPeriod, Resolution.Daily)
                    .Union(new [] { currentSlice[symbol] as TradeBar })
                    .ToList();
                stos[symbol] = Sto(stoHistories);

                //var hourlyHistories = History<TradeBar>(symbol, stoLookbackPeriod * 7, Resolution.Hour)
                //    .Union(new[] { currentSlice[symbol] as TradeBar })
                //    .Select(x => x.Price);
                //stdDevs[symbol] = StdDev(hourlyHistories);
            }
        }

        private decimal StdDev(IEnumerable<decimal> prices)
        {
            var mean = prices.Average();
            //var meansums = prices.Select(x => x < mean ? Math.Pow((double)(x - mean), 2) : 0);
            var meansums = prices.Select(x => Math.Pow((double)(x - mean), 2));
            return (decimal)Math.Sqrt(meansums.Average());
        }

        private decimal Sto(IEnumerable<TradeBar> bars)
        {
            var low = bars.Min(x => x.Low);
            var high = bars.Max(x => x.High);
            return (bars.First().Price - low) / (high - low) * 100;
        }

        private void SendEmailNotification(string msg)
        {
            if (!LiveMode)
                return;
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "mono";
            startInfo.Arguments = $"/home/ubuntu/git/GmailSender/GmailSender/bin/Debug/GmailSender.exe {msg} chrisshort168@gmail.com";
            process.StartInfo = startInfo;
            process.Start();
        }

        private decimal Sma(string symbol, int periods)
        {
            return histories[symbol].Take(periods).Select(x => x.Price).Average();
        }

        private decimal MacdHistogram(string symbol)
        {
            return new MacdData(histories[symbol].Take(slowMacdDays).Select(x => x.Price)).Histogram;
        }

        private bool BuySignal(string symbol)
        {
            return
                macds.ContainsKey(symbol)
                && MacdBuySignal(symbol)
                && SmaBuySignal(symbol)
                && StoBuySignal(symbol);
        }

        private bool SellSignal(string symbol)
        {
            return
                macds.ContainsKey(symbol)
                && SmaSellSignal(symbol)
                && (MacdSellSignal(symbol) || StoSellSignal(symbol));
        }

        private void PlotPoints()
        {
            Plot("leverage", "cash", Portfolio.Cash);
            Plot("leverage", "holdings", Portfolio.TotalHoldingsValue);
        }

        private bool MacdBuySignal(string symbol)
        {
            return macds[symbol] > 0;
        }

        private bool SmaBuySignal(string symbol)
        {
            return Sma(symbol, fastSmaDays) > Sma(symbol, slowSmaDays);
        }

        private bool StoBuySignal(string symbol)
        {
            return stos[symbol] < 20;
        }

        private bool MacdSellSignal(string symbol)
        {
            return macds[symbol] < 0;
        }

        private bool SmaSellSignal(string symbol)
        {
            return Sma(symbol, fastSmaDays) < Sma(symbol, slowSmaDays);
        }

        private bool StoSellSignal(string symbol)
        {
            return stos[symbol] > 80;
        }

        private class MacdData
        {
            public decimal Histogram;
            private decimal SmoothFactor(int periods) => 2.0m / (1 + periods);

            // data is ordered most to least recent
            public MacdData(IEnumerable<decimal> data)
            {
                var fastEma = Ema(data.Skip(data.Count() - fastMacdDays),
                    SmoothFactor(fastMacdDays));
                var slowEma = Ema(data.Skip(data.Count() - slowMacdDays),
                    SmoothFactor(slowMacdDays));
                var macdLine = Enumerable
                    .Range(0, signalMacdDays)
                    .Select(i => fastEma.ElementAt(i) - slowEma.ElementAt(i));
                var signalLine = Ema(macdLine, SmoothFactor(signalMacdDays));
                Histogram = macdLine.First() - signalLine.First();
            }

            private List<decimal> Ema(IEnumerable<decimal> data, decimal smoothFactor)
            {
                var currentPrice = data.First();
                if (data.Count() == 1)
                {
                    return new List<decimal> { currentPrice };
                }

                var emas = Ema(data.Skip(1), smoothFactor);
                var currentEma = currentPrice * smoothFactor
                        + (1 - smoothFactor) * emas.First();
                emas.Insert(0, currentEma);
                return emas;
            }
        }
    }
}

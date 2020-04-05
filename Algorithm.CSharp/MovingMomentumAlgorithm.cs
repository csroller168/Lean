/*
 * Copyright Chris Short 2019
*/

using QuantConnect.Orders;
using QuantConnect.Data;
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
        //      change buy criteria
        //          (macd or sma) && sto
        //              test this with different macd params
        //          sell on negative macd histogram slope
        //      test run hourly (no governor) with trailing stop-loss
        // bugs
        //      remove raw initialization? (default to adjusted price)
        //          splits may trigger stop-loss inadvertently
        //      Fix stop loss orders:
        //          guard against buying when below stop-loss point
        //      use deployed custom emailer
        // deployment
        //      trade with live $
        //      if I eventually make this into a business, integrate directly with alpaca

        private static readonly int slowMacdDays = 26;
        private static readonly int fastMacdDays = 12;
        private static readonly int signalMacdDays = 9;
        private static readonly int slowSmaDays = 150;
        private static readonly int fastSmaDays = 20;
        private static readonly int stoLookbackPeriod = 20;
        private static readonly decimal stopLossPct = 0.15m;
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
        private readonly ISlippageModel SlippageModel = new ConstantSlippageModel(0.002m);
        private Dictionary<string, List<BaseData>> histories = new Dictionary<string, List<BaseData>>();
        private Dictionary<string, decimal> macds = new Dictionary<string, decimal>();
        private Dictionary<string, decimal> stos = new Dictionary<string, decimal>();
        private Dictionary<string, decimal> highs = new Dictionary<string, decimal>();

        public override void Initialize()
        {
            // Set requested data resolution (NOTE: only needed for IB)
            UniverseSettings.Resolution = Resolution.Daily;
            SetBenchmark("SPY");

            SetStartDate(2003, 8, 1);
            //SetStartDate(2019, 12, 2);
            SetEndDate(2020, 3, 27);
            SetCash(100000);
            //SetSecurityInitializer(x => x.SetDataNormalizationMode(DataNormalizationMode.Raw));
            SetBrokerageModel(BrokerageName.AlphaStreams);

            var resolution = LiveMode ? Resolution.Minute : Resolution.Daily;
            universe.ForEach(x =>
            {
                var equity = AddEquity(x, resolution, null, true);
                equity.SetSlippageModel(SlippageModel);
            });
        }

        public override void OnData(Slice slice)
        {
            if (TradedToday())
                return;
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

                if (toBuy.Any() || toSell.Any())
                {
                    EmitAllInsights(toBuy, toSell);
                    foreach (var symbol in toSell)
                    {
                        var stopLossTicket = Transactions
                            .GetOpenOrderTickets(x =>
                                x.OrderType == OrderType.StopMarket
                                && x.Symbol == symbol)
                            .SingleOrDefault();
                        stopLossTicket?.Cancel();
                        Liquidate(symbol);
                    }
                    var pct = 0.98m / toOwn.Count();
                    var targets = toOwn.Select(x => new PortfolioTarget(x, pct)).ToList();
                    SetStopOrders(slice, targets);
                    SetHoldings(targets);
                }
                else
                {
                    var openStopLossTickets = Transactions.GetOpenOrderTickets(x => x.OrderType == OrderType.StopMarket);
                    foreach (var ticket in openStopLossTickets)
                    {
                        ticket.Update(new UpdateOrderFields
                        {
                            StopPrice = GetStopPrice(ticket.Symbol, ticket)
                        });
                    }
                    //Transactions
                    //.GetOpenOrderTickets(x => x.OrderType == OrderType.StopMarket)
                    //.ToList()
                    //.ForEach(x => x.Update(new UpdateOrderFields
                    //{
                    //    StopPrice = GetStopPrice(slice, x.Symbol)
                    //}));
                }
            }
            catch(Exception e)
            {
                // try again in an hour
            }
        }

        private void SetStopOrders(Slice slice, IEnumerable<PortfolioTarget> targets = null)
        {
            foreach(var target in targets)
            {
                var stopLossOrder = Transactions
                    .GetOpenOrderTickets(x =>
                        x.OrderType == OrderType.StopMarket
                        && target.Symbol == x.Symbol)
                    .SingleOrDefault();
                var qty = PortfolioTarget.Percent(this, target.Symbol, target.Quantity).Quantity;
                if (stopLossOrder == null)
                {
                    
                    var id = StopMarketOrder(target.Symbol, -qty, GetStopPrice(target.Symbol));
                }
                else
                {
                    var r = stopLossOrder.Update(new UpdateOrderFields
                    {
                        StopPrice = GetStopPrice(target.Symbol, stopLossOrder),
                        Quantity = -qty
                    });
                }
            }
        }

        private decimal GetStopPrice(Symbol symbol, OrderTicket ticket = null)
        {
            var localHigh = Math.Round((1 - stopLossPct) * highs[symbol], 2);
            var currentStop = ticket?.Get(OrderField.StopPrice) ?? 0m;
            var newStop = Math.Max(localHigh, currentStop);
            return newStop;
        }

        private void EmitAllInsights(List<string> toBuy, IEnumerable<string> toSell)
        {
            var insights = toBuy
                .Select(x => Insight.Price(x, Resolution.Daily, 10, InsightDirection.Up))
                .Union(toSell.Select(x => Insight.Price(x, Resolution.Daily, 10, InsightDirection.Down)))
                .ToArray();
            EmitInsights(insights);
        }

        private bool TradedToday()
        {
            if (lastRun?.Day == Time.Day)
                return true;

            lastRun = Time;
            return false;
        }

        private void UpdateIndicatorData(Slice currentSlice)
        {
            var localHistories = History(slowSmaDays, Resolution.Daily).ToList();
            foreach (var symbol in universe)
            {
                if (!localHistories[0].ContainsKey(symbol))
                    continue;
                histories[symbol] = localHistories
                    .Select(x => x[symbol] as BaseData)
                    .Union(new[] { currentSlice[symbol] as BaseData })
                    .OrderByDescending(x => x.Time)
                    .ToList();
                macds[symbol] = MacdHistogram(symbol);

                var stoHistories = History<TradeBar>(symbol, stoLookbackPeriod, Resolution.Daily)
                    .Union(new [] { currentSlice[symbol] as TradeBar })
                    .OrderByDescending(x => x.Time);
                var low = stoHistories.Min(x => x.Low);
                var high = stoHistories.Max(x => x.High);
                stos[symbol] = (stoHistories.First().Price - low) / (high - low) * 100;
                highs[symbol] = high;
            }
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
                && StoBuySignal(symbol)
                && SmaBuySignal(symbol)
                && Portfolio[symbol].Price > GetStopPrice(symbol);
        }

        private bool SellSignal(string symbol)
        {
            return
                macds.ContainsKey(symbol)
                && SmaSellSignal(symbol)
                && (MacdSellSignal(symbol) || StoSellSignal(symbol));
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log(orderEvent.ToString());
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

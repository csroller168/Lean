/*
 * Copyright Chris Short 2020
*/

using System;
using System.Linq;
using System.Collections.Generic;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Brokerages;
using QuantConnect.Algorithm.Framework.Execution;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Data.Market;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class BigULongShortTrendAlgorithm : QCAlgorithm
    {
        // long-term TODOS:
        // submit alpha when done (https://www.youtube.com/watch?v=f1F4q4KsmAY)

        // short-term TODOs:
        //
        // debug todo:
        //      sto window may not be working
        //      build universe only of positive momentum
        //          for loop that breaks when universe big enough
        //      tweak macd/sto params to not fire/unfire so quickly
        //          maybe just one of them
        // can i get more macds to fire?
        // if I cannot increase universe size, put avg daily volume code back
        // NOTE: getInsights is called multiple times just because it's not finding any. no error
        //
        // Implement moving momentum here
        //      trade daily
        //      consider large universe
        //      definition: https://school.stockcharts.com/doku.php?id=trading_strategies:moving_momentum
        //          buy signal:
        //              sma(20) > sma(150)
        //              sto cross below 20
        //              macd cross above 0 after sto cross
        //          sell signal:
        //              sma(20) < sma(150)
        //              sto cross above 80
        //              macd cross below 0 after sto cross
        //          notes:
        //              pair slow parameters with low volatility & vice versa
        //              consider close all positions at end of day
        //      definition: https://www.investopedia.com/trading/introduction-to-momentum-trading/
        //          notes:
        //              avg daily trade volume >= 5M
        //              have a very short holding period
        //              strategy is most effective in bull market
        // test better combination of indicators or different strategy from school.stockcharts.com
        //      in fact, once I get above done, i could probably rapid test many of these
        // consider ratio of longs/shorts that varies with vix
        // consider universe of high volume, low volatility for better fit with momentum strategy
        //      ... or high volatility for hypothetical mean reversion strategy
        // consider refining long/short universe with dividend yield or age of company

        private static readonly TimeSpan RebalancePeriod = TimeSpan.FromDays(1);
        private static readonly int UniverseSize = 500;
        private static readonly int NumLongShort = 15;
        private static readonly int UniverseSmaDays = 5;
        private static readonly decimal UniverseMinDollarVolume = 5000000m;
        private static readonly int SlowSmaDays = 150;
        private static readonly int FastSmaDays = 20;
        private static readonly int StoDays = 10;
        private static readonly int StoBuyThreshold = 20;
        private static readonly int StoSellThreshold = 80;
        private static readonly int FastMacdDays = 5;
        private static readonly int SlowMacdDays = 30;
        private static readonly int SignalMacdDays = 9;
        private static readonly double CashPct = 0.005;
        private readonly UpdateMeter _rebalanceMeter = new UpdateMeter(RebalancePeriod);
        private readonly Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>();
        private readonly Dictionary<Symbol, RollingWindow<IndicatorDataPoint>> _stos = new Dictionary<Symbol, RollingWindow<IndicatorDataPoint>>();
        private readonly Dictionary<Symbol, RollingWindow<IndicatorDataPoint>> _macdHistograms = new Dictionary<Symbol, RollingWindow<IndicatorDataPoint>>();

        public override void Initialize()
        {
            SetStartDate(2015, 1, 1);
            SetEndDate(2015, 3, 30);
            SetCash(100000);
            UniverseSettings.Resolution = LiveMode
                ? Resolution.Minute
                : Resolution.Hour;
            UniverseSettings.FillForward = true;
            SetBrokerageModel(BrokerageName.AlphaStreams);
            AddUniverseSelection(new CoarseFundamentalUniverseSelectionModel(SelectCoarse));
        }

        public override void OnData(Slice slice)
        {
            try
            {
                if (!_rebalanceMeter.IsDue(Time))
                    return;

                if (slice.Count() == 0)
                    return;

                var insights = GetInsights(slice).ToArray();
                EmitInsights(insights);
                Rebalance(insights);
                _rebalanceMeter.Update(Time);
            }
            catch(Exception e)
            {
                SendEmailNotification(e.Message);
            }
        }

        private void Rebalance(Insight[] insights)
        {
            var targets = new FixedPercentPortfolioConstructionModel(NumLongShort * 2, CashPct)
                .CreateTargets(this, insights);
            new SmartImmediateExecutionModel().Execute(this, targets.ToArray());
            var targetsStr = targets.Any() ? string.Join(",", targets.Select(x => x.Symbol.Value)) : "nothing";
            Log(targetsStr);
            SendEmailNotification(targetsStr);
        }

        private InsightDirection MomentumDirection(Symbol symbol)
        {
            if (!_momentums.ContainsKey(symbol)
                || !_momentums[symbol].IsReady)
                return InsightDirection.Flat;

            return _momentums[symbol] > 0 ? InsightDirection.Up : InsightDirection.Down;
        }

        private SignalStatus StoStatus(Symbol symbol)
        {
            var status = new SignalStatus();

            if (!_stos.ContainsKey(symbol)
                || !_stos[symbol].IsReady)
                return status;

            var stos = _stos[symbol].Select(x => x.Value).ToList();
            //var stos = new List<decimal> { 18m, 19m, 18, 19m, 23m };

            var daysSinceOutBuyRange = stos.FindIndex(x => x > StoBuyThreshold) - 1;
            if (stos[0] <= StoBuyThreshold && daysSinceOutBuyRange > 0)
            {
                status.DaysPastSignal = daysSinceOutBuyRange;
                status.Direction = InsightDirection.Up;
            }

            var daysSinceOutSellRange = stos.FindIndex(x => x < StoSellThreshold) - 1;
            if (stos[0] >= StoSellThreshold && daysSinceOutSellRange > 0)
            {
                status.DaysPastSignal = daysSinceOutSellRange;
                status.Direction = InsightDirection.Down;
            }

            return status;
        }

        private SignalStatus MacdStatus(Symbol symbol)
        {
            var status = new SignalStatus();

            if (!_macdHistograms.ContainsKey(symbol)
                || !_macdHistograms[symbol].IsReady)
                return status;

            var histograms = _macdHistograms[symbol].Select(x => x.Value).ToList();
            //var histograms = new List<decimal> { 1, 1, 1, -0.5m, -1m };

            var daysSinceInSellRange = histograms.FindIndex(x => x < 0);
            if(histograms[0] > 0 && daysSinceInSellRange > 0)
            {
                status.Direction = InsightDirection.Up;
                status.DaysPastSignal = daysSinceInSellRange - 1;
            }

            var daysSinceInBuyRange = histograms.FindIndex(x => x > 0);
            if (histograms[0] < 0 && daysSinceInBuyRange > 0)
            {
                status.Direction = InsightDirection.Down;
                status.DaysPastSignal = daysSinceInBuyRange - 1;
            }

            return status;
        }

        private IEnumerable<Insight> GetInsights(Slice slice)
        {
            try
            {
                var insights = new List<Insight>();

                var stoStatuses = _stos.ToDictionary(
                    x => x.Key,
                    x => StoStatus(x.Key));
                var macdStatuses = _macdHistograms.ToDictionary(
                    x => x.Key,
                    x => MacdStatus(x.Key));

                //*****
                Log(slice.Count());

                //var aSymbol = _stos.Keys.Single(x => x.Value == "AAPL");
                //var sto = _stos[aSymbol];
                //var strElements = Enumerable.Range(0, sto.Count)
                //    .Select(x => $"{x}({sto[x].Value})");
                //var str = string.Join(", ", strElements);
                //Log(str);



                //var momentumCount = ActiveSecurities
                //    .Where(x => x.Value.IsTradable
                //        && slice.ContainsKey(x.Key)
                //        && MomentumDirection(x.Key) == InsightDirection.Up)
                //    .Count();

                //var stoHistogram = ActiveSecurities
                //    .Where(x => x.Value.IsTradable
                //        && slice.ContainsKey(x.Key)
                //        && stoStatuses[x.Key].Direction == InsightDirection.Up)
                //    .Select(x => x.Key)
                //    .GroupBy(x => stoStatuses[x].DaysPastSignal)
                //    .Select(x => new { Days = x.Key, Count = x.Count() })
                //    .OrderBy(x => x.Count)
                //    .Select(x => $"{x.Days}({x.Count})");

                //var macdHistogram = ActiveSecurities
                //    .Where(x => x.Value.IsTradable
                //        && slice.ContainsKey(x.Key)
                //        && macdStatuses[x.Key].Direction == InsightDirection.Up)
                //    .Select(x => x.Key)
                //    .GroupBy(x => macdStatuses[x].DaysPastSignal)
                //    .Select(x => new { Days = x.Key, Count = x.Count() })
                //    .OrderBy(x => x.Count)
                //    .Select(x => $"{x.Days}({x.Count})");

                //Log($"momentumCount={momentumCount}, stos[{string.Join(",", stoHistogram)}], macds[{string.Join(",", macdHistogram)}]");

                //////////

                //var aSto = _stos[sym][0].Value;
                //Log($"{Time}: sto[{sym.Value}]={aSto}");

                //var momentumCount = ActiveSecurities
                //    .Where(x => x.Value.IsTradable
                //        && slice.ContainsKey(x.Key)
                //        && MomentumDirection(x.Key) == InsightDirection.Up)
                //    .Count();
                //var stoCount = ActiveSecurities
                //    .Where(x => x.Value.IsTradable
                //        && slice.ContainsKey(x.Key)
                //        && stoStatuses[x.Key].Direction == InsightDirection.Up)
                //    .Count();
                //var macdCount = ActiveSecurities
                //    .Where(x => x.Value.IsTradable
                //        && slice.ContainsKey(x.Key)
                //        && macdStatuses[x.Key].Direction == InsightDirection.Up)
                //    .Count();
                //Log($"{Time}: momCount={momentumCount}, stoCount={stoCount}, macdCount={macdCount}");

                //*****

                insights.AddRange(ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && MomentumDirection(x.Key) == InsightDirection.Up
                        && stoStatuses[x.Key].Direction == InsightDirection.Up
                        && macdStatuses[x.Key].Direction == InsightDirection.Up
                        && macdStatuses[x.Key].DaysPastSignal < stoStatuses[x.Key].DaysPastSignal)
                    .Take(NumLongShort)
                    .Select(x => new Insight(
                            x.Value.Symbol,
                            RebalancePeriod,
                            InsightType.Price,
                            InsightDirection.Up)));

                insights.AddRange(ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && MomentumDirection(x.Key) == InsightDirection.Down
                        && stoStatuses[x.Key].Direction == InsightDirection.Down
                        && macdStatuses[x.Key].Direction == InsightDirection.Down
                        && macdStatuses[x.Key].DaysPastSignal < stoStatuses[x.Key].DaysPastSignal)
                    .Take(NumLongShort)
                    .Select(x => new Insight(
                            x.Value.Symbol,
                            RebalancePeriod,
                            InsightType.Price,
                            InsightDirection.Down)));

                return insights;
            }
            catch (Exception e)
            {
                Log($"Exception: GetInsights: {e.Message}");
                throw;
            }
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            try
            {
                foreach (var addition in changes.AddedSecurities)
                {
                    // momentum
                    var slowIndicator = SMA(addition.Symbol, SlowSmaDays, Resolution.Daily);
                    var fastIndicator = SMA(addition.Symbol, FastSmaDays, Resolution.Daily);
                    var slope = fastIndicator.Minus(slowIndicator);
                    _momentums[addition.Symbol] = slope;

                    // STO
                    var stoIndicator = STO(addition.Symbol, StoDays, Resolution.Daily);
                    var stoWindow = new RollingWindow<IndicatorDataPoint>(5);
                    stoIndicator.Updated += (sender, dataPoint) => stoWindow.Add(dataPoint);
                    _stos[addition.Symbol] = stoWindow;

                    // MACD
                    var macdIndicator = MACD(addition.Symbol, FastMacdDays, SlowMacdDays, SignalMacdDays, MovingAverageType.Exponential, Resolution.Daily);
                    var macdWindow = new RollingWindow<IndicatorDataPoint>(5);
                    macdIndicator.Histogram.Updated += (sender, dataPoint) => macdWindow.Add(dataPoint);
                    _macdHistograms.Add(addition.Symbol, macdWindow);

                    var history = History(addition.Symbol, SlowSmaDays, Resolution.Daily);
                    foreach (var bar in history)
                    {
                        slowIndicator.Update(bar.EndTime, bar.Close);
                        fastIndicator.Update(bar.EndTime, bar.Close);
                        stoIndicator.Update(bar);
                        macdIndicator.Update(bar.EndTime, bar.Close);
                    }
                }

                foreach (var removedSecurity in changes.RemovedSecurities)
                {
                    _momentums.Remove(removedSecurity.Symbol);
                    _stos.Remove(removedSecurity.Symbol);
                    _macdHistograms.Remove(removedSecurity.Symbol);
                }
            }
            catch (Exception e)
            {
                Log($"Exception: OnSecuritiesChanged: {e.Message}");
            }
        }

        private IEnumerable<Symbol> SelectCoarse(IEnumerable<CoarseFundamental> candidates)
        {
            if (!_rebalanceMeter.IsDue(Time))
                return Universe.Unchanged;

            return candidates.Where(x =>
                x.HasFundamentalData
                && x.DollarVolume > UniverseMinDollarVolume)
                .OrderByDescending(x => x.DollarVolume)
                .Take(UniverseSize)
                .Select(x => x.Symbol);
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

        public override void OnEndOfDay()
        {
            if (Portfolio.Invested)
                Liquidate();
        }
    }

    public class SmartImmediateExecutionModel : ImmediateExecutionModel
    {
        public override void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            var adjustedTargets = targets.ToList();
            adjustedTargets.RemoveAll(x =>
                algorithm.Portfolio.ContainsKey(x.Symbol)
                && algorithm.Portfolio[x.Symbol].Invested);
            adjustedTargets.AddRange(algorithm.ActiveSecurities
                .Where(x =>
                    algorithm.Portfolio.ContainsKey(x.Key)
                    && algorithm.Portfolio[x.Key].Invested
                    && !targets.Any(y => y.Symbol.Equals(x.Key)))
                .Select(x => PortfolioTarget.Percent(algorithm, x.Key, 0)));
            base.Execute(algorithm, adjustedTargets.ToArray());
        }
    }

    public class FixedPercentPortfolioConstructionModel : PortfolioConstructionModel
    {
        private readonly double Percent = 0;
        public FixedPercentPortfolioConstructionModel(
            int maxNumHoldings,
            double cashPct)
        {
            Percent = (1.0 - cashPct) / maxNumHoldings;
        }

        protected override Dictionary<Insight, double> DetermineTargetPercent(List<Insight> activeInsights)
        {
            return activeInsights.ToDictionary(x => x, x => Percent * (double)x.Direction);
        }
    }

    public class UpdateMeter
    {
        private readonly TimeSpan _frequency;
        private DateTime _lastUpdate = DateTime.MinValue;
        public bool IsDue(DateTime now) => _lastUpdate.Add(_frequency) <= now;

        public UpdateMeter(TimeSpan frequency)
        {
            _frequency = frequency;
        }

        public void Update(DateTime now)
        {
            _lastUpdate = now;
        }
    }

    public class SignalStatus
    {
        public InsightDirection Direction = InsightDirection.Flat;
        public int DaysPastSignal = -1;
    }
}
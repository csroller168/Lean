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
using QuantConnect.Util;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.Custom.CBOE;
using QuantConnect.Data.Market;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace QuantConnect.Algorithm.CSharp
{
    public class HiddenGemsAlgorithm : QCAlgorithm
    {
        // fundamentals:  https://www.quantconnect.com/docs/data-library/fundamentals
        //
        // long-term TODOS:
        //      submit alpha when done (https://www.youtube.com/watch?v=f1F4q4KsmAY)
        //      consider submit one for each sector
        // 
        //
        // TODOs:
        //  to speed up, maybe take top/bottom ~100-200 longs shorts ranked on some non-volatile company info metric
        //  consider add consumer defensive sector (205), not consumer cyclical (except maybe for shorts)
        //  restrict universe with more fundamental metrics - target ActiveSecurities <= 200
        //  tune vix params, etc. to reduce drawdown
        //  put more criteria in coarse/fine select now that it's more frequent
        //  reduce drawdown:
        //      test using a weighted average of current price and momentum values instead of just momentum
        //  live:  why no trades?  on next deploy, revise email output
        //  redo comprehensive analysis of features - remove them to get baseline, then put back and tune


        private static readonly string[] ExchangesAllowed = { "NYS", "NAS" };
        private static readonly int[] SectorsAllowed = { 311 };
        private static readonly int SmaLookbackDays = 126;
        private static readonly int SmaWindowDays = 25;
        private static readonly int NumLong = 30;
        private static readonly int NumShort = 0; //5;
        private static readonly int VixLookbackDays = 38;
        private static readonly decimal MinDollarVolume = 1000000m;
        private static readonly decimal MinMarketCap = 2000000000m; // mid-large cap
        private static readonly decimal MaxDrawdown = -0.07m;
        private static readonly decimal MaxShortMomentum = 1m;
        private static readonly decimal MinLongMomentum = 1m;
        private static readonly decimal MinPrice = 5m;
        private static readonly object mutexLock = new object();
        private readonly ConcurrentDictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new ConcurrentDictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>();
        private readonly ConcurrentDictionary<Symbol, Maximum> _maximums = new ConcurrentDictionary<Symbol, Maximum>();
        private readonly Dictionary<Symbol, DateTime> _stopLosses = new Dictionary<Symbol, DateTime>();
        private readonly decimal VixMomentumThreshold = 1.4m;
        private readonly decimal MinOpRevenueGrowth = 0m;
        private static TimeSpan RebuildUniversePeriod;
        private static TimeSpan RebalancePeriod;
        private UpdateMeter _universeMeter;
        private UpdateMeter _rebalanceMeter;
        private List<Symbol> _longCandidates = new List<Symbol>();

        //http://cache.quantconnect.com/alternative/cboe/vix.csv
        private List<TradeBar> _vixHistories = new List<TradeBar>();
        private Symbol _vixSymbol;
        private int _targetLongCount;
        private int _targetShortCount;
        private int numAttemptsToTrade = 0;
        
        public override void Initialize()
        {
            SendEmailNotification("Starting initialization");
            UniverseSettings.Resolution = LiveMode ? Resolution.Minute : Resolution.Hour;
            RebuildUniversePeriod = LiveMode ? TimeSpan.FromSeconds(1) : TimeSpan.FromDays(5);
            RebalancePeriod = LiveMode ? TimeSpan.FromHours(12) : TimeSpan.FromDays(1);
            _universeMeter = new UpdateMeter(RebuildUniversePeriod);
            _rebalanceMeter = new UpdateMeter(RebalancePeriod);

            SetStartDate(2009, 11, 1);
            SetEndDate(2012, 11, 1);
            SetCash(100000);

            UniverseSettings.FillForward = true;
            SetBrokerageModel(BrokerageName.AlphaStreams);
            AddUniverseSelection(new FineFundamentalUniverseSelectionModel(SelectCoarse, SelectFine));

            _vixSymbol = AddData<CBOE>("VIX").Symbol;
            InitializeVixHistories();
            SendEmailNotification("Initialization complete");
        }

        public override void OnData(Slice slice)
        {
            try
            {
                //HandleVixData(slice);
                HandleSplits(slice);

                if (!_rebalanceMeter.IsDue(Time)
                    || slice.Count() == 0
                    || !IsAllowedToTrade(slice))
                    return;

                if (!ActiveSecurities.Any())
                    return;

                SendEmailNotification("Begin OnData()");
                SetTargetCounts();
                var insights = GetInsights(slice).ToArray();
                EmitInsights(insights);
                Rebalance(insights);
                _rebalanceMeter.Update(Time);
                SendEmailNotification("End OnData()");
            }
            catch(Exception e)
            {
                var msg = $"Exception: OnData: {e.Message}, {e.StackTrace}";
                Log(e.Message);
                SendEmailNotification(e.Message);
            }
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                SendEmailNotification("Start OnSecuritiesChanged()");
                numAttemptsToTrade = 0;
                Parallel.ForEach(changes.AddedSecurities, (addition) =>
                {
                    InitIndicators(addition.Symbol);
                });
                Parallel.ForEach(changes.RemovedSecurities, (removal) =>
                {
                    ClearIndicators(removal.Symbol);
                });

                SendEmailNotification("End OnSecuritiesChanged()");

                stopwatch.Stop();
                Log($"Onsecuritieschanged took {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                var msg = $"Exception: OnSecuritiesChanged: {e.Message}, {e.StackTrace}";
                Log(msg);
                SendEmailNotification(msg);
            }
        }

        private void InitializeVixHistories()
        {
            if (!LiveMode)
            {
                _vixHistories = History<CBOE>(_vixSymbol, VixLookbackDays, Resolution.Daily)
                    .Cast<TradeBar>()
                    .ToList();

                return;
            }

            var rawHistories = @"09/16/2020, 25.31, 26.59, 24.84, 26.04
                09/17/2020, 28.22, 28.92, 26.26, 26.46
                09/18/2020, 26.65, 28.10, 25.28, 25.83
                09/21/2020, 28.04, 31.18, 27.39, 27.78
                09/22/2020, 28.61, 28.78, 26.48, 26.86
                09/23/2020, 27.02, 29.73, 25.19, 28.58
                09/24/2020, 29.54, 30.49, 27.94, 28.51
                09/25/2020, 28.17, 30.43, 26.02, 26.38
                09/28/2020, 27.15, 27.19, 24.90, 26.19
                09/29/2020, 26.81, 27.43, 25.98, 26.27
                09/30/2020, 26.69, 27.12, 25.06, 26.37
                10/01/2020, 25.78, 27.11, 25.33, 26.70
                10/02/2020, 28.87, 29.90, 26.93, 27.63
                10/05/2020, 29.52, 29.69, 27.27, 27.96
                10/06/2020, 28.05, 30.00, 26.01, 29.48
                10/07/2020, 29.26, 29.76, 27.94, 28.06
                10/08/2020, 27.65, 27.99, 24.88, 26.36
                10/09/2020, 26.20, 26.22, 24.03, 25.00
                10/12/2020, 25.65, 25.65, 24.14, 25.07
                10/13/2020, 25.67, 26.93, 25.16, 26.07
                10/14/2020, 25.72, 27.23, 25.53, 26.40
                10/15/2020, 27.10, 29.06, 26.82, 26.97
                10/16/2020, 27.16, 27.46, 26.19, 27.41
                10/19/2020, 27.36, 29.69, 27.04, 29.18
                10/20/2020, 28.81, 29.60, 28.29, 29.35
                10/21/2020, 29.12, 30.55, 28.37, 28.65
                10/22/2020, 30.10, 30.12, 27.68, 28.11
                10/23/2020, 28.47, 28.67, 27.26, 27.55
                10/26/2020, 29.38, 33.68, 29.22, 32.46
                10/27/2020, 32.04, 33.77, 31.85, 33.35
                10/28/2020, 34.69, 40.77, 34.68, 40.28
                10/29/2020, 38.80, 41.16, 35.63, 37.59
                10/30/2020, 40.81, 41.09, 36.50, 38.02
                11/02/2020, 38.57, 38.78, 36.13, 37.13
                11/03/2020, 36.44, 36.44, 34.19, 35.55
                11/04/2020, 36.79, 36.85, 28.03, 29.57
                11/05/2020, 27.56, 28.14, 26.04, 27.58
                11/06/2020, 27.87, 29.44, 24.56, 24.86";
            var cboe = new CBOE();
            var config = SubscriptionManager.SubscriptionDataConfigService.GetSubscriptionDataConfigs(_vixSymbol).First();
            rawHistories
                .Split(new[] { '\n' })
                .Select(x => cboe.Reader(config, x.Trim(), DateTime.MinValue, LiveMode))
                .ToList()
                .ForEach(x => _vixHistories.Add(x as TradeBar));
        }

        private void HandleSplits(Slice slice)
        {
            foreach(var split in slice.Splits)
            {
                ClearIndicators(split.Key);
                InitIndicators(split.Key);
            }
        }

        private bool IsAllowedToTrade(Slice slice)
        {
            if (!LiveMode)
                return true;

            if(numAttemptsToTrade == 0)
            {
                SendEmailNotification("IsAllowedToTrade...");
            }

            lock (mutexLock)
            {
                if (slice.Count < ActiveSecurities.Count
                    && numAttemptsToTrade < NumLong / 3)
                {
                    numAttemptsToTrade++;
                    return false;
                }
                return true;
            }
        }

        private void HandleVixData(Slice slice)
        {
            if (!slice.ContainsKey(_vixSymbol))
                return;

            var vix = slice.Get<CBOE>(_vixSymbol);

            _vixHistories.Add(vix);
            if (_vixHistories.Count > VixLookbackDays)
                _vixHistories.Remove(_vixHistories.Single(x => x.Time == _vixHistories.Min(y => y.Time)));

            Plot("vix", "value", vix.Close);
        }

        private decimal VixMomentum(IEnumerable<TradeBar> vixHistories)
        {
            var momentum = vixHistories
            .OrderByDescending(x => x.Time)
            .Take(5)
            .Select(x => x.Price)
            .Average()
            /
            vixHistories
            .OrderBy(x => x.Time)
            .Take(5)
            .Select(x => x.Price)
            .Average();
            Plot("vix", "momentum", momentum);
            return momentum;
        }

        private void SetTargetCounts()
        {
            //if (_vixHistories.Count() >= 8)
            //{
            //    SendEmailNotification("We got vix histories!");
            //    var pastMomentum = VixMomentum(_vixHistories.Take(35));
            //    var currentMomentum = VixMomentum(_vixHistories.Skip(3));
            //    Plot("vix", "momentum", currentMomentum);

            //    if (currentMomentum > VixMomentumThreshold
            //        && currentMomentum > pastMomentum)
            //    {
            //        _targetLongCount = NumShort;
            //        _targetShortCount = NumLong;
            //        return;
            //    }

            //    if (currentMomentum < VixMomentumThreshold
            //        && currentMomentum < pastMomentum)
            //    {
            //        _targetLongCount = NumLong;
            //        _targetShortCount = 0;
            //        return;
            //    }
            //}

            _targetLongCount = NumLong;
            _targetShortCount = NumShort;
        }

        private void Rebalance(Insight[] insights)
        {
            var shortCount = insights.Count(x => x.Direction == InsightDirection.Down);
            var longTargets = insights
                .Where(x => x.Direction == InsightDirection.Up)
                .Select(x => PortfolioTarget.Percent(this, x.Symbol, 1.0m / (_targetLongCount + shortCount)));
            var shortTargets = insights
                .Where(x => x.Direction == InsightDirection.Down)
                .Select(x => PortfolioTarget.Percent(this, x.Symbol, -1.0m / (NumLong + NumShort)));
            var targets = longTargets.Union(shortTargets);

            Plot("targetCounts", "longs", longTargets.Count());
            Plot("targetCounts", "shorts", shortTargets.Count());
            Plot("targetCounts", "active", ActiveSecurities.Count());

            new SmartImmediateExecutionModel().Execute(this, targets.ToArray());
            var targetsStr = targets.Any() ? string.Join(",", targets.Select(x => x.Symbol.Value)) : "nothing";
            Log(targetsStr);
            SendEmailNotification($"We have positions in: {targetsStr}");
        }

        private bool StopLossTriggered(Slice slice, Symbol symbol)
        {
            return false;
            if (_stopLosses.ContainsKey(symbol)
                && (Time - _stopLosses[symbol]).Days < SmaWindowDays)
                return true;

            var max = _maximums[symbol].Current;
            var price = (slice[symbol] as BaseData).Price;
            var drawdown = (price - max) / max;

            if(drawdown < MaxDrawdown) // e.g. -0.3 < -0.1
            {
                _stopLosses[symbol] = Time;
                return true;
            }
            return false;
        }

        private IEnumerable<Insight> GetInsights(Slice slice)
        {
            try
            {
                var insights = new List<Insight>();
                insights.AddRange(ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && _longCandidates.Contains(x.Key)
                        && _momentums.ContainsKey(x.Key)
                        && _momentums[x.Key].IsReady
                        && _momentums[x.Key].Current > MinLongMomentum
                        && (slice[x.Key] as BaseData).Price >= MinPrice
                        && !StopLossTriggered(slice, x.Key)
                        )
                    .OrderByDescending(x => _momentums[x.Key].Current)
                    .Take(_targetLongCount)
                    .Select(x => new Insight(
                            x.Value.Symbol,
                            RebalancePeriod,
                            InsightType.Price,
                            InsightDirection.Up))); ;

                insights.AddRange(ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && _momentums.ContainsKey(x.Key)
                        && _momentums[x.Key].IsReady
                        && _momentums[x.Key].Current < MaxShortMomentum
                        && (slice[x.Key] as BaseData).Price >= MinPrice
                        )
                    .OrderBy(x => _momentums[x.Key].Current)
                    .Take(_targetShortCount)
                    .Select(x => new Insight(
                            x.Value.Symbol,
                            RebalancePeriod,
                            InsightType.Price,
                            InsightDirection.Down)));

                return insights;
            }
            catch (Exception e)
            {
                var msg = $"Exception: GetInsights: {e.Message}, {e.StackTrace}";
                Log(msg);
                SendEmailNotification(msg);
                throw;
            }
        }

        private void InitIndicators(Symbol symbol)
        {
            var currentSma = SMA(symbol, SmaWindowDays, Resolution.Daily);
            var pastSma = new Delay(SmaLookbackDays - SmaWindowDays).Of(currentSma);
            var momentum = currentSma.Over(pastSma);
            _momentums[symbol] = momentum;
            //_maximums[symbol] = MAX(symbol, SmaWindowDays, Resolution.Daily);

            var history = History(symbol, SmaLookbackDays, Resolution.Daily);
            foreach (var bar in history)
            {
                currentSma.Update(bar.EndTime, bar.Close);
                //_maximums[symbol].Update(bar.EndTime, bar.Close);
            }
        }

        private void ClearIndicators(Symbol symbol)
        {
            CompositeIndicator<IndicatorDataPoint> unused;
            Maximum unused2;
            _momentums.TryRemove(symbol, out unused);
            _maximums.TryRemove(symbol, out unused2);
        }

        private IEnumerable<Symbol> SelectCoarse(IEnumerable<CoarseFundamental> candidates)
        {
            try
            {
                SendEmailNotification("Start SelectCoarse()");

                if (!_universeMeter.IsDue(Time))
                    return Universe.Unchanged;

                var eligibleCandidates = candidates
                    .Where(x => x.HasFundamentalData
                        && x.DollarVolume > MinDollarVolume
                        )
                    .Select(x => x.Symbol)
                    .ToList();
                SendEmailNotification("End SelectCoarse()");
                return eligibleCandidates;
                
            }
            catch(Exception e)
            {
                var msg = $"Exception: SelectCoarse: {e.Message}, {e.StackTrace}";
                Log(msg);
                SendEmailNotification(msg);
                return Universe.Unchanged;
            }
        }

        private IEnumerable<Symbol> SelectFine(IEnumerable<FineFundamental> candidates)
        {
            try
            {
                SendEmailNotification("Start SelectFine()");

                if (!_universeMeter.IsDue(Time))
                    return Universe.Unchanged;

                _longCandidates = candidates
                    .Where(
                        x => SectorsAllowed.Contains(x.AssetClassification.MorningstarSectorCode)
                        && ExchangesAllowed.Contains(x.SecurityReference.ExchangeId)
                        //&& x.MarketCap > MinMarketCap
                        //&& x.OperationRatios.OperationRevenueGrowth3MonthAvg.Value > MinOpRevenueGrowth
                        )
                    .Select(x => x.Symbol)
                    .ToList();

                var shorts = candidates
                    .Where(
                        x => !_longCandidates.Contains(x.Symbol)
                        && SectorsAllowed.Contains(x.AssetClassification.MorningstarSectorCode)
                        && ExchangesAllowed.Contains(x.SecurityReference.ExchangeId)
                        //&& x.MarketCap > MinMarketCap
                        //&& x.OperationRatios.OperationRevenueGrowth3MonthAvg.Value < MinOpRevenueGrowth
                        )
                    .Select(x => x.Symbol);
                _universeMeter.Update(Time);
                SendEmailNotification("End SelectFine()");

                return _longCandidates.Union(shorts);                
            }
            catch (Exception e)
            {
                var msg = $"Exception: SelectFine: {e.Message}, {e.StackTrace}";
                Log(msg);
                SendEmailNotification(msg);
                return Universe.Unchanged;
            }
        }

        private void SendEmailNotification(string msg)
        {
            if (!LiveMode)
                return;

            Notify.Email("chrisshort168@gmail.com", "Trading app notification", msg);
        }

        private class UpdateMeter
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

        private class SignalStatus
        {
            public InsightDirection Direction = InsightDirection.Flat;
            public int DaysPastSignal = -1;
        }

        private class SmartImmediateExecutionModel : ImmediateExecutionModel
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
    }
}
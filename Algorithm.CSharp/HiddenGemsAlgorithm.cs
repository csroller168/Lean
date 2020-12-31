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
using QuantConnect.Data.Market;
using System.Threading.Tasks;
using System.Collections.Concurrent;

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
        private static readonly string[] ExchangesAllowed = { "NYS", "NAS" };
        private static readonly int[] SectorsAllowed = { 311 }; // { 102, 311 };
        private static readonly int SmaLookbackDays = 126;
        private static readonly int SmaRecentWindowDays = 5;
        private static readonly int SmaDistantWindowDays = 50;
        private static readonly int SmaExclusionDays = 100;
        private static readonly int NumLong = 30;
        private static readonly int NumShort = 0;
        private static readonly decimal MinDollarVolume = 500000m;
        private static readonly decimal MinMarketCap = 2000000000m; // mid-large cap
        private static readonly decimal MaxDrawdown = -0.07m;
        private static readonly decimal MaxShortMomentum = 1m;
        private static readonly decimal MinLongMomentum = 1m;
        private static readonly decimal MinPrice = 5m;
        private static readonly object mutexLock = new object();
        private readonly ConcurrentDictionary<Symbol, GroupIndicator> _indicators = new ConcurrentDictionary<Symbol, GroupIndicator>();
        private readonly Dictionary<Symbol, DateTime> _stopLosses = new Dictionary<Symbol, DateTime>();
        private readonly decimal MinOpRevenueGrowth = 0m;
        private static TimeSpan RebuildUniversePeriod;
        private static TimeSpan RebalancePeriod;
        private UpdateMeter _universeMeter;
        private UpdateMeter _rebalanceMeter;
        private List<Symbol> _longCandidates = new List<Symbol>();

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
            _rebalanceMeter = new UpdateMeter(RebalancePeriod, LiveMode, 9, 31, 16, 29);

            SetStartDate(2011, 1, 1);
            SetEndDate(2013, 1, 1);
            SetCash(100000);

            UniverseSettings.FillForward = true;
            SetBrokerageModel(BrokerageName.AlphaStreams);
            AddUniverseSelection(new FineFundamentalUniverseSelectionModel(SelectCoarse, SelectFine));
            SendEmailNotification("Initialization complete");
        }

        public override void OnData(Slice slice)
        {
            try
            {
                HandleSplits(slice);

                if (!_rebalanceMeter.IsDue(Time)
                    || slice.Count() == 0
                    || !IsAllowedToTrade(slice))
                    return;

                if (!ActiveSecurities.Any())
                    return;

                SendEmailNotification("Begin OnData()");
                UpdateIndicatorOpen(slice);
                SetTargetCounts();
                var insights = GetInsights(slice).ToArray();
                EmitInsights(insights);
                Rebalance(insights);
                _rebalanceMeter.Update(Time);
                SendEmailNotification("End OnData()");
            }
            catch (Exception e)
            {
                var msg = $"Exception: OnData: {e.Message}, {e.StackTrace}";
                Log(msg);
                SendEmailNotification(msg);
            }
        }

        private void UpdateIndicatorOpen(Slice slice)
        {
            Parallel.ForEach(_indicators, (indicator) =>
            {
                try
                {
                    indicator.Value.UpdateOpen(slice[indicator.Key]);
                }
                catch(Exception e)
                {
                    var msg = $"Exception: UpdateIndicatorOpen: {e.Message}, {e.StackTrace}";
                    Log(msg);
                    SendEmailNotification(msg);
                }
            });
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            try
            {
                SendEmailNotification("Start OnSecuritiesChanged()");
                numAttemptsToTrade = 0;
                Parallel.ForEach(changes.RemovedSecurities, (removal) =>
                {
                    try
                    {
                        ClearIndicators(removal.Symbol);
                    }
                    catch(Exception e)
                    {
                        var msg = $"Exception: OnSecuritiesChanged(removed): {e.Message}, {e.StackTrace}";
                        Log(msg);
                        SendEmailNotification(msg);
                    }
                });
                Parallel.ForEach(changes.AddedSecurities, (addition) =>
                {
                    try
                    {
                        InitIndicators(addition.Symbol);
                    }
                    catch(Exception e)
                    {
                        var msg = $"Exception: OnSecuritiesChanged(added): {e.Message}, {e.StackTrace}";
                        Log(msg);
                        SendEmailNotification(msg);
                    }
                });
                
                SendEmailNotification("End OnSecuritiesChanged()");
            }
            catch (Exception e)
            {
                var msg = $"Exception: OnSecuritiesChanged: {e.Message}, {e.StackTrace}";
                Log(msg);
                SendEmailNotification(msg);
            }
        }

        private void HandleSplits(Slice slice)
        {
            foreach (var split in slice.Splits)
            {
                InitIndicators(split.Key);
            }
        }

        private bool IsAllowedToTrade(Slice slice)
        {
            if (!LiveMode)
                return true;

            if (numAttemptsToTrade == 0)
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

        private void SetTargetCounts()
        {
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

        private IEnumerable<Insight> GetInsights(Slice slice)
        {
            try
            {
                var insights = new List<Insight>();
                insights.AddRange(ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && _longCandidates.Contains(x.Key)
                        && _indicators.ContainsKey(x.Key)
                        && _indicators[x.Key].IsReady
                        && _indicators[x.Key].Momentum > MinLongMomentum
                        && !_indicators[x.Key].ExcludedBySma
                        && (slice[x.Key] as BaseData).Price >= MinPrice
                        )
                    .OrderByDescending(x => _indicators[x.Key].Momentum)
                    .Take(_targetLongCount)
                    .Select(x => new Insight(
                            x.Value.Symbol,
                            RebalancePeriod,
                            InsightType.Price,
                            InsightDirection.Up)));


                insights.AddRange(ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && !_longCandidates.Contains(x.Key)
                        && _indicators.ContainsKey(x.Key)
                        && _indicators[x.Key].IsReady
                        && _indicators[x.Key].Momentum < MaxShortMomentum
                        && (slice[x.Key] as BaseData).Price >= MinPrice
                        )
                    .OrderBy(x => _indicators[x.Key].Momentum)
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
            var groupIndicator = GetIndicator(symbol, SmaLookbackDays, Resolution.Daily);
            _indicators[symbol] = groupIndicator;

            var history = History(symbol, SmaLookbackDays, Resolution.Daily);
            foreach(var bar in history)
            {
                groupIndicator.Update(bar);
            }
        }

        private void ClearIndicators(Symbol symbol)
        {
            GroupIndicator unused;
            _indicators.TryRemove(symbol, out unused);
        }

        private GroupIndicator GetIndicator(Symbol symbol, int period, Resolution? resolution = null, Func<IBaseData, IBaseDataBar> selector = null)
        {
            var name = CreateIndicatorName(symbol, $"Group({period})", resolution);
            var indicator = new GroupIndicator(name);
            RegisterIndicator(symbol, indicator, resolution, selector);

            return indicator;
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
            catch (Exception e)
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
                        && x.MarketCap > MinMarketCap
                        && x.OperationRatios.OperationRevenueGrowth3MonthAvg.Value > MinOpRevenueGrowth
                        )
                    .Select(x => x.Symbol)
                    .ToList();

                var shorts = candidates
                    .Where(
                        x => !_longCandidates.Contains(x.Symbol)
                        && SectorsAllowed.Contains(x.AssetClassification.MorningstarSectorCode)
                        && ExchangesAllowed.Contains(x.SecurityReference.ExchangeId)
                        && x.MarketCap < MinMarketCap
                        && x.OperationRatios.OperationRevenueGrowth3MonthAvg.Value < MinOpRevenueGrowth
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
            private readonly bool _isRangeBound;
            private readonly int _minHour;
            private readonly int _minMinute;
            private readonly int _maxHour;
            private readonly int _maxMinute;

            public UpdateMeter(
                TimeSpan frequency,
                bool isRangeBound = false,
                int minHour = 0,
                int minMinute = 0,
                int maxHour = 23,
                int maxMinute = 59)
            {
                _isRangeBound = isRangeBound;
                _minHour = minHour;
                _minMinute = minMinute;
                _maxHour = maxHour;
                _maxMinute = maxMinute;
                _frequency = frequency;
            }

            public void Update(DateTime now)
            {
                _lastUpdate = now;
            }

            public bool IsDue(DateTime now)
            {
                var inRange = true;
                if (_isRangeBound)
                {
                    var minAllowedTime = new DateTime(now.Year, now.Month, now.Day, _minHour, _minMinute, 0);
                    var maxAllowedTime = new DateTime(now.Year, now.Month, now.Day, _maxHour, _maxMinute, 0);

                    inRange = now > minAllowedTime
                        && now < maxAllowedTime;
                }
                return inRange && _lastUpdate.Add(_frequency) <= now;
            }
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

        public class GroupIndicator : BarIndicator
        {
            private List<IBaseDataBar> _bars = new List<IBaseDataBar>(SmaLookbackDays);
            private decimal _recentSma = 1m;
            private decimal _distantSma = 1m;
            private decimal _exclusionSma = 1m;
            private decimal _openValue = 1m;

            public override bool IsReady => _bars.Count == SmaLookbackDays;

            public decimal Momentum => AdjustedAverage(_recentSma, SmaRecentWindowDays, 0m, _openValue) / _distantSma;
            public bool ExcludedBySma => _openValue < _exclusionSma;

            public GroupIndicator(string name)
                : base(name) { }

            protected override decimal ComputeNextValue(IBaseDataBar input)
            {
                IBaseDataBar lastRemovedBar = null;
                if (IsReady)
                {
                    lastRemovedBar = _bars[0];
                    _bars.RemoveAt(0);
                }
                _bars.Add(input);

                if (IsReady)
                {
                    if (lastRemovedBar == null)
                    {
                        _recentSma = _bars.Skip(SmaLookbackDays - SmaRecentWindowDays).Average(x => Value(x));
                        _distantSma = _bars.Take(SmaDistantWindowDays).Average(x => Value(x));
                        _exclusionSma = _bars.Skip(SmaLookbackDays - SmaExclusionDays).Average(x => Value(x));
                    }
                    else
                    {
                        _recentSma = AdjustedAverage(_recentSma, SmaRecentWindowDays, _bars[SmaLookbackDays - SmaRecentWindowDays - 1], input);
                        _distantSma = AdjustedAverage(_distantSma, SmaDistantWindowDays, lastRemovedBar, _bars[SmaDistantWindowDays - 1]);
                        _exclusionSma = AdjustedAverage(_exclusionSma, SmaExclusionDays, _bars[SmaLookbackDays - SmaExclusionDays - 1], input);
                    }
                    return Momentum;
                }

                return 1m;
            }

            internal void UpdateOpen(IBaseDataBar bar)
            {
                _openValue = Value(bar);
            }

            private decimal Value(IBaseDataBar bar)
            {
                return bar.High;
            }

            private decimal AdjustedAverage(
                decimal currentAvg,
                int numPeriods,
                IBaseDataBar oldBar,
                IBaseDataBar newBar)
            {
                return AdjustedAverage(currentAvg, numPeriods, Value(oldBar), Value(newBar));
            }

            private decimal AdjustedAverage(
                decimal currentAvg,
                int numPeriods,
                decimal oldVal,
                decimal newVal)
            {
                return ((currentAvg * numPeriods)
                    - oldVal
                    + newVal)
                    / numPeriods;
            }
        }
    }
}
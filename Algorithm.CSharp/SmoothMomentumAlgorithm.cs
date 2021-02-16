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
    public class SmoothMomentumAlgorithm : QCAlgorithm
    {
        // fundamentals:  https://www.quantconnect.com/docs/data-library/fundamentals
        //
        // long-term TODOS:
        //      submit alpha when done (https://www.youtube.com/watch?v=f1F4q4KsmAY)
        // 
        private static readonly string[] ExchangesAllowed = { "NYS", "NAS" };
        private static readonly string AllowedCountry = "USA";
        private static readonly int SmaLookbackDays = 126;
        private static readonly int SmaRecentWindowDays = 5;
        private static readonly int SmaDistantWindowDays = 50;
        private static readonly int SmaExclusionDays = 100;
        private static readonly int MaxSpreadLookbackDays = 90;
        private static readonly decimal MaxDailySpread = 999m;
        private static readonly int NumLong = 30;
        private static readonly int NumShort = 0;
        private static readonly decimal MinDollarVolume = 500000m;
        private static readonly decimal MinMarketCap = 2000000000m; // mid-large cap
        private static readonly decimal MaxDrawdown = -0.07m;
        private static readonly decimal MaxShortMomentum = 1m;
        private static readonly decimal MinLongMomentum = 1m;
        private static readonly decimal MinPrice = 5m;
        private static readonly object mutexLock = new object();
        private readonly ConcurrentDictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new ConcurrentDictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>();
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

            SetStartDate(2008, 1, 1);
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
            var targetsStr = targets.Any() ? string.Join(",", targets.Select(x => x.Symbol.Value).OrderBy(x => x)) : "nothing";
            Log(targetsStr);
            SendEmailNotification($"We have positions in: {targetsStr}");
        }

        //private IEnumerable<Symbol> RankSymbols(IEnumerable<Symbol> symbols)
        //{
        //    var indicators = _indicators.Where(x => symbols.Contains(x.Key)).ToList();
        //    var momentumOrder = indicators.OrderByDescending(x => x.Value.Momentum).ToList();
        //    var spreadOrder = indicators.OrderBy(x => x.Value.MaxDailySpread).ToList();
        //    var ranks = indicators.ToDictionary(
        //        x => x.Key,
        //        x =>
        //            0.55 * momentumOrder.FindIndex(y => x.Key == y.Key)
        //            + 0.45 * spreadOrder.FindIndex(y => x.Key == y.Key));
        //    return symbols.OrderBy(x => ranks[x]);
        //}

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
                        && (slice[x.Key] as BaseData).Price >= MinPrice)
                    .OrderByDescending(x => _momentums[x.Key].Current)
                    .Take(_targetLongCount)
                        .Select(x => new Insight(
                                x.Key,
                                RebalancePeriod,
                                InsightType.Price,
                                InsightDirection.Up)));
                return insights;




                //var candidateLongs = RankSymbols(ActiveSecurities
                //    .Where(x => x.Value.IsTradable
                //        && slice.ContainsKey(x.Key)
                //        && _longCandidates.Contains(x.Key)
                //        && _momentums.ContainsKey(x.Key)
                //        && _indicators[x.Key].IsReady
                //        && _indicators[x.Key].Momentum > MinLongMomentum
                //        && !_indicators[x.Key].ExcludedBySma
                //        && !_indicators[x.Key].ExcludedBySpread
                //        && (slice[x.Key] as BaseData).Price >= MinPrice)
                //    .Select(x => x.Key))
                //    .ToList();

                //var holdingRanks = Portfolio
                //    .Where(x => x.Value.Invested)
                //    .ToDictionary(
                //        x => x.Key,
                //        x => candidateLongs.FindIndex(y => y == x.Key));
                //var rankThreshold = 2.5 * NumLong;
                //var toSell = holdingRanks
                //    .Where(x => x.Value < 0 || x.Value > rankThreshold)
                //    .Select(x => x.Key);

                //var toHold = holdingRanks.Keys.Except(toSell);
                //var toBuy = candidateLongs
                //    .Where(x => !toHold.Contains(x))
                //    .OrderByDescending(x => _indicators[x].Momentum)
                //    .Take(NumLong - toHold.Count());

                //var toOwn = toBuy
                //    .Union(toHold)
                //    .Select(x => new Insight(
                //            x,
                //            RebalancePeriod,
                //            InsightType.Price,
                //            InsightDirection.Up));

                //return toOwn;
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
            var currentSma = SMA(symbol, SmaRecentWindowDays, Resolution.Daily);
            var distantSma = SMA(symbol, SmaDistantWindowDays, Resolution.Daily);
            var pastSma = new Delay(SmaLookbackDays - SmaDistantWindowDays).Of(distantSma);
            var momentum = currentSma.Over(pastSma);
            _momentums[symbol] = momentum;

            var history = History(symbol, SmaLookbackDays, Resolution.Daily);
            foreach(var bar in history)
            {
                currentSma.Update(bar.EndTime, bar.Close);
                distantSma.Update(bar.EndTime, bar.Close);
            }
        }

        private void ClearIndicators(Symbol symbol)
        {
            CompositeIndicator<IndicatorDataPoint> unused;
            _momentums.TryRemove(symbol, out unused);
        }

        private IEnumerable<Symbol> SelectCoarse(IEnumerable<CoarseFundamental> candidates)
        {
            try
            {
                SendEmailNotification("Start SelectCoarse()");

                if (!_universeMeter.IsDue(Time))
                    return Universe.Unchanged;

                var eligibleCandidates = candidates
                    .Where(x => x.HasFundamentalData)
                    .OrderByDescending(x => x.Volume)
                    .Take(300)
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
                        x =>
                        x.MarketCap > MinMarketCap
                        && x.CompanyReference.CountryId == AllowedCountry
                        && x.OperationRatios.OperationRevenueGrowth3MonthAvg.Value > MinOpRevenueGrowth
                        )
                    .Select(x => x.Symbol)
                    .ToList();

                var shorts = Enumerable.Empty<Symbol>();
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
    }
}
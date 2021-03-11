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
        // short-term TODOs:
        //      evaluate efficacy of missing data bug fix
        //      evaluate under-performing years (details)
        //          these have very low leverage... consider other hedge strategies
        //      test different universe filters(market cap, dollar volume, volatility, etc.)
        //          especially sma parameters
        //      open only when vix momentum < 2
        //      shorts with strict criteria (mom < 0.9 and low op rev growth, low mkt cap)
        //      stop loss
        //      test hedge with vix momentum

        private static readonly string[] ExchangesAllowed = { "NYS", "NAS" };
        private static readonly string AllowedCountry = "USA";
        private static readonly int SmaLookbackDays = 126;
        private static readonly int SmaRecentWindowDays = 5;
        private static readonly int SmaDistantWindowDays = 50;
        private static readonly int SmaExclusionDays = 100;
        private static readonly int MaxSpreadLookbackDays = 90;
        private static readonly decimal MaxDailySpread = 999m;
        private static readonly int NumLong = 12;
        private static readonly int NumShort = 0;
        private static readonly decimal MinDollarVolume = 500000m;
        private static readonly decimal MinMarketCap = 2000000000m; // mid-large cap
        private static readonly decimal MaxDrawdown = -0.07m;
        private static readonly decimal MaxShortMomentum = 1m;
        private static readonly decimal MinLongMomentum = 1m;
        private static readonly decimal MinPrice = 5m;
        private static readonly decimal RankSellMargin = 1.5m;
        private static readonly object mutexLock = new object();
        private readonly ConcurrentDictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new ConcurrentDictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>();
        private readonly decimal MinOpRevenueGrowth = 0m;
        private static TimeSpan RebuildUniversePeriod;
        private static TimeSpan RebalancePeriod;
        private UpdateMeter _universeMeter;
        private UpdateMeter _rebalanceMeter;
        private UpdateMeter _onDataMeter;
        private List<Symbol> _longCandidates = new List<Symbol>();
        private SimpleMovingAverage _spyMomentum;
        private Symbol _spySymbol = QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA);

        private int _targetLongCount;
        private int _targetShortCount;
        private int _numAttemptsToTrade = 0;
        private decimal _leverage = 1m;

        public override void Initialize()
        {
            UniverseSettings.Resolution = LiveMode ? Resolution.Minute : Resolution.Hour;
            RebuildUniversePeriod = LiveMode ? TimeSpan.FromSeconds(1) : TimeSpan.FromDays(5);
            RebalancePeriod = LiveMode ? TimeSpan.FromHours(12) : TimeSpan.FromDays(1);
            _universeMeter = new UpdateMeter(RebuildUniversePeriod);
            _rebalanceMeter = new UpdateMeter(RebalancePeriod, LiveMode, 9, 31, 16, 29);
            _onDataMeter = new UpdateMeter(TimeSpan.FromMinutes(2));

            AddSecurity(_spySymbol, Resolution.Hour);
            _spyMomentum = SMA(_spySymbol, 100, Resolution.Daily);

            SetStartDate(2008, 1, 1);
            SetEndDate(2013, 1, 1);
            SetCash(100000);

            UniverseSettings.FillForward = true;
            SetBrokerageModel(BrokerageName.AlphaStreams);
            AddUniverseSelection(new FineFundamentalUniverseSelectionModel(SelectCoarse, SelectFine));
        }

        public override void OnData(Slice slice)
        {
            try
            {
                HandleSplits(slice);

                if (!_rebalanceMeter.IsDue(Time)
                    || !_onDataMeter.IsDue(Time)
                    || slice.Count() == 0
                    || !IsAllowedToTrade(slice))
                    return;

                if (!ActiveSecurities.Any())
                    return;

                SetTargetCounts();
                var insights = GetInsights(slice).ToArray();
                EmitInsights(insights);
                Rebalance(insights);
                _rebalanceMeter.Update(Time);
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
                _numAttemptsToTrade = 0;
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

            lock (mutexLock)
            {
                _onDataMeter.Update(Time);
                if ((slice.Count < ActiveSecurities.Count
                    || ActiveSecurities
                        .Where(x=> x.Value.Invested)
                        .Any(x => !slice.ContainsKey(x.Key)))
                    && _numAttemptsToTrade < NumLong)
                {
                    _numAttemptsToTrade++;
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

        private bool SetLeverage()
        {
            var oldLeverage = _leverage;
            var medianMomentum = _momentums.Values.Median(x => x.Current);
            if (medianMomentum < 0.795m) _leverage = 0.2m;
            else if (medianMomentum > 0.805m && medianMomentum < 0.995m) _leverage = 0.6m;
            else if (medianMomentum > 1.05m) _leverage = 1m;
            return oldLeverage != _leverage;
        }

        private void Rebalance(Insight[] insights)
        {
            var leverageChanged = SetLeverage();
            var shortCount = insights.Count(x => x.Direction == InsightDirection.Down);
            var longTargets = insights
                .Where(x => x.Direction == InsightDirection.Up)
                .Select(x => PortfolioTarget.Percent(this, x.Symbol, _leverage / (_targetLongCount + shortCount)));
            var shortTargets = insights
                .Where(x => x.Direction == InsightDirection.Down)
                .Select(x => PortfolioTarget.Percent(this, x.Symbol, -_leverage / (NumLong + NumShort)));
            var targets = longTargets.Union(shortTargets);

            Plot("targetCounts", "longs", longTargets.Count());
            Plot("targetCounts", "shorts", shortTargets.Count());
            Plot("targetCounts", "active", ActiveSecurities.Count());
            Plot("momentum", "median", _momentums.Values.Median(x => x.Current));
            Plot("momentum", "leverage", _leverage);

            new SmartImmediateExecutionModel().Execute(this, targets.ToArray(), leverageChanged);
            var longs = targets.Any() ? string.Join(",",
                targets
                .Where(x => x.Quantity > 0)
                .Select(x => x.Symbol.Value)
                .OrderBy(x => x)) : "nothing";
            var shorts = targets.Any() ? string.Join(",",
                targets
                .Where(x => x.Quantity < 0)
                .Select(x => x.Symbol.Value)
                .OrderBy(x => x)) : "nothing";
            var longsStr = $"[CMS] LONGS: {longs}";
            var shortsStr = $"[CMS] SHORTS: {shorts}";
            Log(longsStr);
            Log(shortsStr);
            SendEmailNotification(longsStr + "\n" + shortsStr);
        }

        private IEnumerable<Insight> GetInsights(Slice slice)
        {
            try
            {
                var candidateLongs = ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && _longCandidates.Contains(x.Key)
                        && _momentums.ContainsKey(x.Key)
                        && _momentums[x.Key].IsReady
                        && _momentums[x.Key].Current > MinLongMomentum
                        && (slice[x.Key] as BaseData).Price >= MinPrice)
                    .OrderByDescending(x => _momentums[x.Key].Current)
                    .ToList();

                var holdingRanks = Portfolio
                    .Where(x => x.Value.Invested)
                    .ToDictionary(
                        x => x.Key,
                        x => candidateLongs.FindIndex(y => y.Key == x.Key));
                var rankThreshold = RankSellMargin * NumLong;
                var toSell = holdingRanks
                    .Where(x => x.Value < 0 || x.Value > rankThreshold)
                    .Select(x => x.Key);
                var toHold = holdingRanks.Keys.Except(toSell);
                var toBuy = candidateLongs
                    .Where(x => !toHold.Contains(x.Key))
                    .OrderByDescending(x => _momentums[x.Key].Current)
                    .Take(NumLong - toHold.Count())
                    .Select(x => x.Key);

                if (slice.ContainsKey(_spySymbol) &&
                    (slice[_spySymbol] as BaseData).Price < _spyMomentum.Current)
                    toBuy = Enumerable.Empty<Symbol>();

                var insights = toBuy
                    .Union(toHold)
                    .Select(x => new Insight(
                            x,
                            RebalancePeriod,
                            InsightType.Price,
                            InsightDirection.Up));

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
                if (!_universeMeter.IsDue(Time))
                    return Universe.Unchanged;

                var eligibleCandidates = candidates
                    .Where(x => x.HasFundamentalData)
                    .OrderByDescending(x => x.Volume)
                    .Take(300)
                    .Select(x => x.Symbol)
                    .ToList();
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
            public void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets, bool forceRebalance)
            {
                var adjustedTargets = targets.ToList();
                if (!forceRebalance)
                {
                    adjustedTargets.RemoveAll(x =>
                        algorithm.Portfolio.ContainsKey(x.Symbol)
                        && algorithm.Portfolio[x.Symbol].Invested);
                }
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
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

namespace QuantConnect.Algorithm.CSharp
{
    public partial class HiddenGemsAlgorithm : QCAlgorithm
    {
        // fundamentals:  https://www.quantconnect.com/docs/data-library/fundamentals
        //
        // long-term TODOS:
        //      submit alpha when done (https://www.youtube.com/watch?v=f1F4q4KsmAY)
        //      consider submit one for each sector
        //
        // live integration
        //  merge from master?  MovingMomentum not working locally now.
        //
        // TODOs:
        //  temporary:  send debug output in emails
        //  cronjob to backup/delete output
        //      or... make the app stop between 5 pm and 11 PM
        //  make a better trading hours guard fix
        //  resolve the slump late 2015-mid 2017
        //  from docs: If an algorithm is indicator-heavy and a split occurs, the algorithm will have to reset and refresh the indicators using historical data. We can monitor for split events in the slice.Splits[] collection.
        //  to speed up, maybe take top/bottom ~100-200 longs shorts ranked on some non-volatile company info metric
        //  consider add consumer defensive sector (205), not consumer cyclical (except maybe for shorts)
        //  restrict universe with more fundamental metrics - target ActiveSecurities <= 200
        //  set min company age for shorts and max age for longs
        //
        // bugs:
        //   commit 377f41b made too few longs, but I want to see if doesn't crash live


        private static readonly string[] ExchangesAllowed = { "NYS", "NAS" };
        private static readonly int[] SectorsAllowed = { 311 };
        private static readonly int SmaLookbackDays = 126;
        private static readonly int SmaWindowDays = 25;
        private static readonly int NumLong = 30;
        private static readonly int NumShort = 5;
        private static readonly decimal MinDollarVolume = 1000000m;
        private static readonly decimal MinMarketCap = 2000000000m; // mid-large cap
        private static readonly decimal MaxDrawdown = 0.4m;
        private static readonly decimal MaxShortMomentum = 1m;
        private static readonly decimal MinLongMomentum = 1m;
        private static readonly decimal MinPrice = 5m;
        private static readonly object mutexLock = new object();
        private readonly Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>();
        private readonly Dictionary<Symbol, Maximum> _maximums = new Dictionary<Symbol, Maximum>();
        private readonly Dictionary<Symbol, DateTime> _stopLosses = new Dictionary<Symbol, DateTime>();
        private readonly decimal VixMomentumThreshold = 1.4m;
        private readonly decimal MinOpRevenueGrowth = 0m;
        private static TimeSpan RebuildUniversePeriod;
        private static TimeSpan RebalancePeriod;
        private UpdateMeter _universeMeter;
        private UpdateMeter _rebalanceMeter;
        private List<Symbol> _longCandidates = new List<Symbol>();
        private Symbol _vixSymbol;
        private int _targetLongCount;
        private int _targetShortCount;
        private int numAttemptsToTrade = 0;
        private readonly Dictionary<Symbol, decimal> _dollarVolumes = new Dictionary<Symbol, decimal>();
        private readonly Dictionary<Symbol, long> _marketCaps = new Dictionary<Symbol, long>();
        private readonly Dictionary<Symbol, decimal> _opRevenueGrowth = new Dictionary<Symbol, decimal>();
        

        public override void Initialize()
        {
            SendEmailNotification("Starting initialization");
            UniverseSettings.Resolution = LiveMode ? Resolution.Minute : Resolution.Hour;
            RebuildUniversePeriod = LiveMode ? TimeSpan.FromSeconds(1) : TimeSpan.FromDays(60);
            RebalancePeriod = LiveMode ? TimeSpan.FromHours(12) : TimeSpan.FromDays(1);
            _universeMeter = new UpdateMeter(RebuildUniversePeriod);
            _rebalanceMeter = new UpdateMeter(RebalancePeriod);

            SetStartDate(2010, 1, 1);
            SetEndDate(2010, 2, 1);
            SetCash(100000);

            UniverseSettings.FillForward = true;
            SetBrokerageModel(BrokerageName.AlphaStreams);
            AddUniverseSelection(new FineFundamentalUniverseSelectionModel(SelectCoarse, SelectFine));

            _vixSymbol = AddData<CBOE>("VIX").Symbol;
            SendEmailNotification("Initialization complete");
        }

        public override void OnData(Slice slice)
        {
            try
            {
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
                Log(e.Message);
                SendEmailNotification(e.Message);
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
            var vixHistories = History<CBOE>(_vixSymbol, 38, Resolution.Daily).Cast<TradeBar>();
            if (vixHistories.Any())
            {
                SendEmailNotification("We got vix histories!");
                var pastMomentum = VixMomentum(vixHistories.Take(35));
                var currentMomentum = VixMomentum(vixHistories.Skip(3));
                Plot("vix", "momentum", currentMomentum);

                if (currentMomentum > VixMomentumThreshold
                    && currentMomentum > pastMomentum)
                {
                    _targetLongCount = NumShort;
                    _targetShortCount = NumLong;
                    return;
                }

                if (currentMomentum < VixMomentumThreshold
                    && currentMomentum < pastMomentum)
                {
                    _targetLongCount = NumLong;
                    _targetShortCount = 0;
                    return;
                }
            }

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
            if (_stopLosses.ContainsKey(symbol)
                && (Time - _stopLosses[symbol]).Days < SmaWindowDays)
                return true;

            var max = _maximums[symbol].Current;
            var price = (slice[symbol] as BaseData).Price;
            var drawdown = (max - price) / max;

            if(drawdown >= MaxDrawdown)
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
                        //&& _dollarVolumes.ContainsKey(x.Key)
                        //&& _dollarVolumes[x.Key] > MinDollarVolume
                        //&& _marketCaps.ContainsKey(x.Key)
                        //&& _marketCaps[x.Key] > MinMarketCap
                        //&& _opRevenueGrowth.ContainsKey(x.Key)
                        //&& _opRevenueGrowth[x.Key] > MinOpRevenueGrowth
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
                        //&& _dollarVolumes.ContainsKey(x.Key)
                        //&& _dollarVolumes[x.Key] > MinDollarVolume
                        //&& _marketCaps.ContainsKey(x.Key)
                        //&& _marketCaps[x.Key] > MinMarketCap
                        //&& _opRevenueGrowth.ContainsKey(x.Key)
                        //&& _opRevenueGrowth[x.Key] < MinOpRevenueGrowth
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
                var msg = $"Exception: GetInsights: {e.Message}";
                Log(msg);
                SendEmailNotification(msg);
                throw;
            }
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            try
            {
                SendEmailNotification("Start OnSecuritiesChanged()");
                numAttemptsToTrade = 0;
                foreach (var addition in changes.AddedSecurities)
                {
                    var currentSma = SMA(addition.Symbol, SmaWindowDays, Resolution.Daily);
                    var pastSma = new Delay(SmaLookbackDays - SmaWindowDays).Of(currentSma);
                    var momentum = currentSma.Over(pastSma);
                    _momentums[addition.Symbol] = momentum;
                    _maximums[addition.Symbol] = MAX(addition.Symbol, SmaWindowDays, Resolution.Daily);

                    var history = History(addition.Symbol, SmaLookbackDays, Resolution.Daily);
                    foreach(var bar in history)
                    {
                        currentSma.Update(bar.EndTime, bar.Close);
                        _maximums[addition.Symbol].Update(bar.EndTime, bar.Close);
                    }
                }
                foreach(var removal in changes.RemovedSecurities)
                {
                    _momentums.Remove(removal.Symbol);
                    _maximums.Remove(removal.Symbol);
                }

                SendEmailNotification("End OnSecuritiesChanged()");
            }
            catch (Exception e)
            {
                var msg = $"Exception: OnSecuritiesChanged: {e.Message}";
                Log(msg);
                SendEmailNotification(msg);
            }
        }

        private IEnumerable<Symbol> SelectCoarse(IEnumerable<CoarseFundamental> candidates)
        {
            try
            {
                SendEmailNotification("Start SelectCoarse()");
                //_dollarVolumes.Clear();
                //foreach (var candidate in candidates)
                //{
                //    _dollarVolumes[candidate.Symbol] = candidate.DollarVolume;
                //}

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
                var msg = $"Exception: SelectCoarse: {e.Message}";
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
                //_marketCaps.Clear();
                //_opRevenueGrowth.Clear();
                //foreach (var candidate in candidates)
                //{
                //    _marketCaps[candidate.Symbol] = candidate.MarketCap;
                //    _opRevenueGrowth[candidate.Symbol] = candidate.OperationRatios.OperationRevenueGrowth3MonthAvg.Value;
                //}

                if (!_universeMeter.IsDue(Time))
                    return Universe.Unchanged;

                var _longCandidates = candidates
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
                        && x.MarketCap > MinMarketCap
                        && x.OperationRatios.OperationRevenueGrowth3MonthAvg.Value < MinOpRevenueGrowth
                        )
                    .Select(x => x.Symbol);
                _universeMeter.Update(Time);
                SendEmailNotification("End SelectFine()");

                return _longCandidates.Union(shorts);                
            }
            catch (Exception e)
            {
                var msg = $"Exception: SelectFine: {e.Message}";
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
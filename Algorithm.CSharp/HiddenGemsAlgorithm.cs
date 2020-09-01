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

namespace QuantConnect.Algorithm.CSharp
{
    public class HiddenGemsAlgorithm : QCAlgorithm
    {
        // long-term TODOS:
        // submit alpha when done (https://www.youtube.com/watch?v=f1F4q4KsmAY)

        // optimize todos:
        //      risk control
        //          short criteria
        //              maybe high debt, high p/e, or low momentum (or different exchange)
        //              first, separate collections of longs/shorts
        //      some criteria to find early risers... low mkt cap high $volume?  low $volume?
        //          1yr growth metrics
        //      screen for debt (https://www.quantconnect.com/docs/data-library/fundamentals)
        //          no/low debt for longs
        //      cap or floor market cap x.companyProfile.MarketCap
        //      increase NumLong, NumShort
        //      don't limit to USA

        private static readonly TimeSpan RebalancePeriod = TimeSpan.FromDays(1);
        private static readonly TimeSpan RebuildUniversePeriod = TimeSpan.FromDays(60);
        private static readonly int YearEstablishedLookback = 10;
        private static readonly int TechSectorCode = 311;
        private static readonly string CountryCode = "USA";
        private static readonly string[] ExchangesAllowed = { "NYS", "NAS" };
        private static readonly int SmaLookbackDays = 126; // ~ 6 mo.
        private static readonly int SmaWindowDays = 14;
        private static readonly int NumLong = 20;
        private static readonly int NumShort = 0;
        private static readonly decimal MaxDrawdown = 0.2m;
        private static readonly decimal MinOpGrowth = 0m;
        private static readonly decimal UniverseMinDollarVolume = 5000000m;
        private readonly UpdateMeter _rebalanceMeter = new UpdateMeter(RebalancePeriod);
        private readonly UpdateMeter _universeMeter = new UpdateMeter(RebuildUniversePeriod);
        private readonly Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>();
        private readonly Dictionary<Symbol, Maximum> _maximums = new Dictionary<Symbol, Maximum>();
        private List<Symbol> _longCandidates = new List<Symbol>();
        private readonly Dictionary<Symbol, DateTime> _stopLosses = new Dictionary<Symbol, DateTime>();

        public override void Initialize()
        {
            SetStartDate(2006, 1, 1);
            SetEndDate(2020, 8, 1);
            SetCash(100000);
            UniverseSettings.Resolution = LiveMode
                ? Resolution.Minute
                : Resolution.Hour;
            UniverseSettings.FillForward = true;
            SetBrokerageModel(BrokerageName.AlphaStreams);
            AddUniverseSelection(new FineFundamentalUniverseSelectionModel(SelectCoarse, SelectFine));
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
            var shortCount = insights.Count(x => x.Direction == InsightDirection.Down);
            var longTargets = insights
                .Where(x => x.Direction == InsightDirection.Up)
                .Select(x => PortfolioTarget.Percent(this, x.Symbol, 1.0m / (NumLong + shortCount)));
            var shortTargets = insights
                .Where(x => x.Direction == InsightDirection.Down)
                .Select(x => PortfolioTarget.Percent(this, x.Symbol, -1.0m / (NumLong + NumShort)));
            var targets = longTargets.Union(shortTargets);

            new SmartImmediateExecutionModel().Execute(this, targets.ToArray());
            var targetsStr = targets.Any() ? string.Join(",", targets.Select(x => x.Symbol.Value)) : "nothing";
            Log(targetsStr);
            SendEmailNotification(targetsStr);
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
                        && _momentums[x.Key].Current > 1
                        && !StopLossTriggered(slice, x.Key)
                        )
                    .OrderByDescending(x => _momentums[x.Key].Current)
                    .Take(NumLong)
                    .Select(x => new Insight(
                            x.Value.Symbol,
                            RebalancePeriod,
                            InsightType.Price,
                            InsightDirection.Up)));

                insights.AddRange(ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && !_longCandidates.Contains(x.Key)
                        && _momentums.ContainsKey(x.Key)
                        && _momentums[x.Key].IsReady
                        && _momentums[x.Key].Current < 1)
                    .OrderBy(x => _momentums[x.Key].Current)
                    .Take(NumShort)
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
                foreach(var addition in changes.AddedSecurities)
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
            }
            catch (Exception e)
            {
                Log($"Exception: OnSecuritiesChanged: {e.Message}");
            }
        }

        private IEnumerable<Symbol> SelectCoarse(IEnumerable<CoarseFundamental> candidates)
        {
            if (!_universeMeter.IsDue(Time))
                return Universe.Unchanged;

            _longCandidates = candidates
                .Where(x => x.HasFundamentalData
                    && x.DollarVolume > UniverseMinDollarVolume)
                .Select(x => x.Symbol)
                .ToList();

            var shorts = Enumerable.Empty<Symbol>();

            return _longCandidates.Union(shorts);
        }

        private IEnumerable<Symbol> SelectFine(IEnumerable<FineFundamental> candidates)
        {
            if (!_universeMeter.IsDue(Time))
                return Universe.Unchanged;

            var longs = candidates
                .Where(
                	x => _longCandidates.Contains(x.Symbol)
                	&& x.AssetClassification.MorningstarSectorCode == TechSectorCode
                    && IsRecent(x.CompanyReference.YearofEstablishment)
                    && x.CompanyReference.CountryId == CountryCode
                    && ExchangesAllowed.Contains(x.SecurityReference.ExchangeId)
                    && x.OperationRatios.OperationRevenueGrowth3MonthAvg.HasValue
                    && x.OperationRatios.OperationRevenueGrowth3MonthAvg.Value > MinOpGrowth)
                .Select(x => x.Symbol)
                .ToList();
            _longCandidates = longs;

            var shorts = candidates
                .Where(
                    x => !_longCandidates.Contains(x.Symbol)
                    && x.AssetClassification.MorningstarSectorCode == TechSectorCode
                    && !IsRecent(x.CompanyReference.YearofEstablishment)
                    && x.CompanyReference.CountryId == CountryCode
                    && ExchangesAllowed.Contains(x.SecurityReference.ExchangeId)
                    && x.OperationRatios.OperationRevenueGrowth3MonthAvg.HasValue
                    && x.OperationRatios.OperationRevenueGrowth3MonthAvg.Value < 0)
                .Select(x => x.Symbol);

            _universeMeter.Update(Time);

            return _longCandidates.Union(shorts);
        }

        private bool IsRecent(string strYearEstablished)
        {
            int yearEstablished;
            if (!int.TryParse(strYearEstablished, out yearEstablished))
                return false;
            return yearEstablished + YearEstablishedLookback >= Time.Year;
        }

        private void SendEmailNotification(string msg)
        {
            if (!LiveMode)
                return;
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                FileName = "mono",
                Arguments = $"/home/ubuntu/git/GmailSender/GmailSender/bin/Debug/GmailSender.exe {msg} chrisshort168@gmail.com"
            };
            process.StartInfo = startInfo;
            process.Start();
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
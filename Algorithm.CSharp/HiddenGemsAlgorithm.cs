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

        // debug todos:
        //      go faster!

        // optimize todos:
        //      ** retry all prior things now that rebal bug fixed
        //      risk control**
        //          short criteria
        //              maybe high debt, high p/e, or low momentum (or different exchange)
        //              first, separate collections of longs/shorts
        //      screen for debt (https://www.quantconnect.com/docs/data-library/fundamentals)
        //      adjust MinYearEstablished
        //      cap or floor market cap x.companyProfile.MarketCap
        //      increase NumLong, NumShort
        //      adjust universe size
        //      some criteria to find early risers... low mkt cap high $volume?  low $volume?
        //          1yr growth metrics

        private static readonly TimeSpan RebalancePeriod = TimeSpan.FromHours(12);
        private static readonly int CoarseUniverseSize = 400;
        private static readonly int FineUniverseSize = 50;
        private static readonly int MinYearEstablished = 1992;
        private static readonly int TechSectorCode = 311;
        private static readonly string CountryCode = "USA";
        private static readonly string[] ExchangesAllowed = { "NYS", "NAS" };
        private static readonly int SmaLookbackDays = 126; // ~ 6 mo.
        private static readonly int SmaWindowDays = 14;
        private static readonly int NumLong = 15;
        private static readonly int NumShort = 0;
        private static readonly decimal UniverseMinDollarVolume = 5000000m;
        private readonly UpdateMeter _rebalanceMeter = new UpdateMeter(RebalancePeriod);
        private readonly Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>();
        private List<Symbol> _longCandidates = new List<Symbol>();

        public override void Initialize()
        {
            SetStartDate(2006, 1, 1);
            SetEndDate(2008, 1, 1);
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
            var targets = new EqualWeightingPortfolioConstructionModel()
                .CreateTargets(this, insights);
            new SmartImmediateExecutionModel().Execute(this, targets.ToArray());
            var targetsStr = targets.Any() ? string.Join(",", targets.Select(x => x.Symbol.Value)) : "nothing";
            Log(targetsStr);
            SendEmailNotification(targetsStr);
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
                        && _momentums[x.Key].IsReady)
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
                        && _momentums[x.Key].IsReady)
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

                    var history = History(addition.Symbol, SmaLookbackDays, Resolution.Daily);
                    foreach(var bar in history)
                    {
                        currentSma.Update(bar.EndTime, bar.Close);
                    }
                }
                foreach(var removal in changes.RemovedSecurities)
                {
                    _momentums.Remove(removal.Symbol);
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

            _longCandidates = candidates
                .Where(x => x.HasFundamentalData
                    && x.DollarVolume > UniverseMinDollarVolume)
                .OrderByDescending(x => x.DollarVolume)
                .Take(CoarseUniverseSize)
                .Select(x => x.Symbol)
                .ToList();

            var shorts = Enumerable.Empty<Symbol>();

            return _longCandidates.Union(shorts);
        }

        private IEnumerable<Symbol> SelectFine(IEnumerable<FineFundamental> candidates)
        {
            if (!_rebalanceMeter.IsDue(Time))
                return Universe.Unchanged;

            var longs = candidates
                .Where(
                	x => _longCandidates.Contains(x.Symbol)
                	&& x.AssetClassification.MorningstarSectorCode == TechSectorCode
                    && StrToInt(x.CompanyReference.YearofEstablishment) >= MinYearEstablished
                    && x.CompanyReference.CountryId == CountryCode
                    && ExchangesAllowed.Contains(x.SecurityReference.ExchangeId))
                .Take(FineUniverseSize)
                .Select(x => x.Symbol)
                .ToList();
            _longCandidates = longs;

            var shorts = Enumerable.Empty<Symbol>();

            return _longCandidates.Union(shorts);
        }

        private int StrToInt(string str)
        {
            int result;
            if(!int.TryParse(str, out result)) return int.MinValue;
            return result;
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
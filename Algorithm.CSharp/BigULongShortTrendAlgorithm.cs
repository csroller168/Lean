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
        // DateMeter appears to not work, likely because of exception thrown during rebal
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
        private static readonly int UniverseSize = 150;
        private static readonly int NumLongShort = 6;
        private static readonly int UniverseSmaDays = 5;
        private static readonly int SlowSmaDays = 150;
        private static readonly int FastSmaDays = 20;
        private static readonly double CashPct = 0.005;
        private readonly UpdateMeter _rebalanceMeter = new UpdateMeter(RebalancePeriod);
        private readonly Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>(); 
        //private readonly Dictionary<Symbol, RollingWindow<CompositeIndicator<IndicatorDataPoint>>> _smaIndicator = new Dictionary<Symbol, RollingWindow<CompositeIndicator<IndicatorDataPoint>>>();

        public override void Initialize()
        {
            SetStartDate(2015, 1, 1);
            SetEndDate(2016, 6, 30);
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

                var insights = GetInsights(slice).ToArray();
                if(insights.Count() >= NumLongShort * 2)
                {
                    EmitInsights(insights);
                    Rebalance(insights);
                    _rebalanceMeter.Update(Time);
                }
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
            SendEmailNotification(targetsStr);
        }

        private IEnumerable<Insight> GetInsights(Slice slice)
        {
            try
            {
                var insights = new List<Insight>();

                var momentums = ActiveSecurities
                    .Where(x => x.Value.IsTradable
                        && slice.ContainsKey(x.Key)
                        && _momentums.ContainsKey(x.Key)
                        && _momentums[x.Key].IsReady)
                    .OrderByDescending(x => _momentums[x.Key])
                    .ToList();

                insights.AddRange(momentums
                    .Take(NumLongShort)
                    .Select(x => new Insight(
                            x.Value.Symbol,
                            RebalancePeriod,
                            InsightType.Price,
                            InsightDirection.Up)));
                momentums.Reverse();

                insights.AddRange(momentums
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
                return Enumerable.Empty<Insight>();
            }
        }

        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            try
            {
                foreach (var addition in changes.AddedSecurities)
                {
                    var slowIndicator = SMA(addition.Symbol, SlowSmaDays, Resolution.Daily);
                    var fastIndicator = SMA(addition.Symbol, FastSmaDays, Resolution.Daily);
                    var slope = fastIndicator.Minus(slowIndicator);
                    //var window = new RollingWindow<CompositeIndicator<IndicatorDataPoint>>(2);
                    //slope.Updated += (sender, updated) => window.Add(sender as CompositeIndicator<IndicatorDataPoint>);
                    _momentums[addition.Symbol] = slope;

                    var history = History(addition.Symbol, SlowSmaDays, Resolution.Daily);
                    foreach (var bar in history)
                    {
                        slowIndicator.Update(bar.EndTime, bar.Close);
                        fastIndicator.Update(bar.EndTime, bar.Close);
                    }
                }

                foreach (var removedSecurity in changes.RemovedSecurities)
                {
                    _momentums.Remove(removedSecurity.Symbol);
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

            var avgDollarVolumes = new Dictionary<CoarseFundamental, decimal>();

            foreach (var candidate in candidates.OrderByDescending(x => x.DollarVolume).Take(UniverseSize * 3))
            {
                try
                {
                    var history = History<TradeBar>(candidate.Symbol, UniverseSmaDays, Resolution.Daily);
                    if(history.Any())
                        avgDollarVolumes[candidate] = history.Average(x => x.Volume * x.Price);
                }
                catch (Exception e)
                {
                    Log($"Exception getting avg dollar volume for {candidate.Symbol.Value}: {e.Message}");
                }
            }

            return candidates
                .Where(x => x.HasFundamentalData
                    && avgDollarVolumes.ContainsKey(x))
                .OrderByDescending(x => avgDollarVolumes[x])
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
}
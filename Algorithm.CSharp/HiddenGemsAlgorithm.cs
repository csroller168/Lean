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
    public class HiddenGemsAlgorithm : QCAlgorithm
    {
        // fundamentals:  https://www.quantconnect.com/docs/data-library/fundamentals
        //
        // live issues:
        //      need fundamental data
        //      not getting vix history (maybe just need to wait until it's in the slice, or get externally)
        // long-term TODOS:
        //      submit alpha when done (https://www.youtube.com/watch?v=f1F4q4KsmAY)
        //      consider submit one for each sector
        //
        // TODOs:
        //  resolve the slump late 2015-mid 2017
        //  to speed up, maybe take top/bottom ~100-200 longs shorts ranked on some non-volatile company info metric
        //  consider add consumer defensive sector (205), not consumer cyclical (except maybe for shorts)
        //  restrict universe with more fundamental metrics - target ActiveSecurities <= 200
        //  set min company age for shorts and max age for longs

        private static readonly TimeSpan RebalancePeriod = TimeSpan.FromDays(1);
        private static readonly TimeSpan RebuildUniversePeriod = TimeSpan.FromDays(60);
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
        private readonly UpdateMeter _rebalanceMeter = new UpdateMeter(RebalancePeriod);
        private readonly UpdateMeter _universeMeter = new UpdateMeter(RebuildUniversePeriod);
        private readonly Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>> _momentums = new Dictionary<Symbol, CompositeIndicator<IndicatorDataPoint>>();
        private readonly Dictionary<Symbol, Maximum> _maximums = new Dictionary<Symbol, Maximum>();
        private readonly Dictionary<Symbol, DateTime> _stopLosses = new Dictionary<Symbol, DateTime>();
        private readonly decimal VixMomentumThreshold = 1.4m;
        private readonly decimal MinOpRevenueGrowth = 0m;
        private List<Symbol> _longCandidates = new List<Symbol>();
        private Symbol _vixSymbol;
        private int _targetLongCount;
        private int _targetShortCount;
        private int numAttemptsToTrade = 0;
        private readonly Dictionary<Symbol, decimal> _dollarVolumes = new Dictionary<Symbol, decimal>();
        private readonly Dictionary<Symbol, long> _marketCaps = new Dictionary<Symbol, long>();
        private readonly Dictionary<Symbol, decimal> _opRevenueGrowth = new Dictionary<Symbol, decimal>();
        private readonly List<string> LiveSymbols = new List<string> { "PLAB", "FICO", "MIND", "ZUO", "AIRG", "WDAY", "SNE", "CAMT", "NTNX", "CSGS", "GIB", "WATT", "AVCT", "RST", "FOUR", "RMNI", "AUDC", "SITM", "PBTS", "MPWR", "SABR", "BHE", "TDC", "SNX", "EXTR", "VMW", "ITRI", "QTWO", "PRFT", "EBIX", "JNPR", "SCKT", "QUMU", "SQ", "ST", "IMTE", "MYSZ", "QMCO", "ATEN", "CGNX", "CLSK", "HBB", "PRTH", "SANM", "ADSK", "WSTG", "OSS", "IMMR", "KOSS", "WIX", "JCOM", "VIAV", "AGMH", "LYTS", "SWKS", "HPQ", "DT", "VVPR", "UTSI", "AVLR", "FORM", "VSAT", "MVIS", "VRTU", "AMBA", "PLXS", "MICT", "RXT", "TYL", "ZIXI", "EPAY", "LSCC", "VPG", "YEXT", "PXLW", "AEHR", "SEDG", "SSNT", "NEWR", "UCTT", "EEFT", "CRWD", "MXL", "WDC", "MNDO", "APPS", "DAKT", "GNSS", "RP", "NSYS", "LEDS", "RMBS", "RPD", "TAIT", "NET", "LYFT", "ENPH", "PAR", "JCS", "MWK", "DSWL", "MTC", "IIIV", "CTG", "TTEC", "SWIR", "MAXR", "STNE", "STMP", "BLIN", "ZEN", "EVSI", "SMTC", "IMXI", "FEYE", "EVBG", "RDVT", "NLOK", "ANSS", "OLED", "PWFL", "FTNT", "DUOT", "APH", "ALOT", "NSIT", "INTU", "VRNT", "ICHR", "MITK", "TNAV", "ALYA", "FN", "UEIC", "NATI", "REFR", "BLKB", "KVHI", "FLIR", "PAGS", "USIO", "LFUS", "TDY", "AKAM", "ALLT", "FIS", "PLAN", "IRBT", "MTSI", "ADBE", "AVT", "QRVO", "MFGP", "PFPT", "SILC", "LPL", "SMAR", "AEY", "SPLK", "OSPN", "ISNS", "NETE", "WORK", "PTC", "ITRN", "TSM", "MKSI", "NTGR", "SCSC", "CMBM", "WISA", "BRQS", "ERIC", "DMRC", "ZI", "DSPG", "APPN", "ELTK", "WKEY", "TTD", "PRO", "GDDY", "SFET", "PING", "OLB", "KLIC", "ASX", "CRNT", "SVMK", "PDFS", "PRGS", "NTAP", "POWI", "RDWR", "CALX", "NVEC", "CEVA", "INOD", "AVNW", "SMTX", "SCON", "ASYS", "XLNX", "DOCU", "BOX", "ACLS", "TCCO", "IBM", "RPAY", "RNG", "SHOP", "NOVT", "TEL", "NICE", "CDW", "MIXT", "BCOV", "CHKP", "CACI", "CLFD", "LTRX", "SGH", "CYBR", "EVTC", "VECO", "MANH", "HPE", "BELFA", "SWCH", "COHU", "HEAR", "BKI", "UBER", "SUNW", "NPTN", "DNB", "COHR", "CASA", "IDEX", "AWRE", "CREE", "FSLY", "CTSH", "INSG", "CLS", "MDLA", "PAYC", "CETXP", "ZS", "TENB", "AZPN", "MSTR", "QLYS", "RSSS", "ON", "PCTI", "GLW", "RIOT", "CLRO", "CPAH", "WTRH", "CSCO", "EXFO", "MIME", "AMKR", "MOGO", "CCMP", "EPAM", "ANY", "MINDP", "TXN", "SEAC", "JKHY", "IPHI", "IDN", "MOSY", "PEGA", "PSTG", "PRSP", "ORCL", "PHUN", "CDNS", "PCTY", "UPLD", "EVOP", "NTCT", "STM", "SMCI", "IIVIP", "NXPI", "AOSL", "NOVA", "SWI", "CSIQ", "PLT", "CAJ", "RELL", "IMOS", "SGMA", "SONM", "PLUS", "SPSC", "NVMI", "SPRT", "PRCP", "TTMI", "TACT", "ENTG", "SSNC", "FLEX", "SSTI", "MDB", "GSKY", "COMM", "QUIK", "SMSI", "KTCC", "LPTH", "IIVI", "SGLB", "LPSN", "DIOD", "SPNS", "GILT", "ZBRA", "TCX", "LLNW", "SPT", "DBX", "BIGC", "CCRC", "KBNT", "SAP", "XELA", "VICR", "MLAB", "INPX", "BNFT", "AAPL", "EBON", "PECK", "FFIV", "DOX", "FARO", "SONO", "SPWR", "DDD", "ASML", "TESS", "CIEN", "ESTC", "AYX", "CTXS", "AXTI", "GSIT", "OTEX", "LITE", "WIT", "VSLR", "UIS", "QADA", "SPI", "KEYS", "DTSS", "FORTY", "BTBT", "SNPS", "EB", "CLDR", "ADI", "DZSI", "GLOB", "IEC", "INFY", "MOBL", "DAIO", "ITI", "ACIW", "GTYH", "ARW", "CRNC", "TEAM", "PD", "MTSC", "GVP", "TRMB", "MSFT", "SATS", "KOPN", "AVGO", "UI", "FTFT", "LASR", "XPER", "SAIL", "MANT", "CDK", "MEI", "VISL", "GDYN", "MRAM", "CREX", "MSI", "VUZI", "LDOS", "MODN", "CYBE", "BOSC", "MGIC", "CPSH", "JAMF", "CETX", "ANET", "GB", "AGYS", "ATOM", "WSTL", "RBCN", "KLAC", "FTV", "NCNO", "NVDA", "BOXL", "NOK", "ALTR", "ELSE", "MJCO", "ACMR", "MAXN", "CSPI", "ALRM", "AMOT", "TSRI", "SHSP", "EIGI", "JBL", "DGII", "GWRE", "DDOG", "OSIS", "HUBS", "VERB", "REKR", "PANW", "MCHP", "BSQR", "CYRN", "ROG", "CSOD", "TSEM", "WEX", "WRTC", "DELL", "CCC", "ESE", "EXLS", "INFN", "CDAY", "NUAN", "SCWX", "AAOI", "WK", "SLAB", "MRVL", "FLT", "SYNA", "UMC", "SAIC", "NOW", "IT", "RESN", "TUFN", "PCYG", "DOMO", "G", "CVLT", "BNSO", "EGAN", "ASUR", "III", "BL", "IPGP", "INTC", "TAOP", "PS", "STX", "HLIT", "FISV", "MU", "FEIM", "CMTL", "LOGI", "AKTS", "LRCX", "COUP", "ECOM", "UEPS", "EGHT", "AMD", "BILL", "RUN", "VRSN", "NTWK", "SYNC", "CLGX", "DBD", "BR", "INVE", "NCR", "CLPS", "AVGOP", "SREV", "LUNA", "ADTN", "BRKS", "VRNS", "BELFB", "FIT", "CNDT", "EVOL", "NEON", "CRM", "CAMP", "BAND", "TER", "OCC", "VSH", "HCKT", "MXIM", "GPRO", "EGOV", "SSYS", "MRIN", "FIVN", "ENV", "ONTO", "LINX", "CRUS", "VOXX", "ACIA", "RAMP", "BB", "AMAT", "ACN", "DCT", "GRMN", "SYKE", "OKTA", "APPF", "KN", "FSLR", "DSGX", "QCOM", "AVYA", "AEYE", "VERX", "VERI", "VCRA", "CNXN", "AMSWA", "QADB", "AMRH", "XRX", "SMIT", "DXC", "PI", "EMKR", "CXDO", "CTS", "MX" };

        public override void Initialize()
        {
            SetStartDate(2006, 1, 1);
            SetEndDate(2010, 1, 1);
            SetCash(100000);
            UniverseSettings.Resolution = LiveMode
                ? Resolution.Minute
                : Resolution.Hour;
            UniverseSettings.FillForward = true;
            SetBrokerageModel(BrokerageName.AlphaStreams);

            if(LiveMode)
            {
                LiveSymbols.ForEach(x =>
                {
                    AddEquity(x, Resolution.Minute, null, true);
                });
            }
            else
            {
                AddUniverseSelection(new FineFundamentalUniverseSelectionModel(SelectCoarse, SelectFine));
            }
            _vixSymbol = AddData<CBOE>("VIX", Resolution.Daily).Symbol;
        }

        public override void OnData(Slice slice)
        {
            try
            {
                if (!_rebalanceMeter.IsDue(Time)
                    || slice.Count() == 0
                    || !IsAllowedToTrade(slice))
                    return;

                SetTargetCounts();
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

        private bool IsAllowedToTrade(Slice slice)
        {
            if (!LiveMode)
                return true;

            lock (mutexLock)
            {
                if (slice.Count < ActiveSecurities.Count
                    && numAttemptsToTrade < NumLong)
                {
                    numAttemptsToTrade++;
                    return false;
                }
                return true;
            }
        }

        private decimal VixMomentum(IEnumerable<TradeBar> vixHistories)
        {
            if (vixHistories.Any())
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
            return 1m;
        }

        private void SetTargetCounts()
        {
            var vixHistories = History<CBOE>(_vixSymbol, 38, Resolution.Daily)
                .Cast<TradeBar>();
            if (vixHistories.Any())
            {
                var pastMomentum = VixMomentum(vixHistories.Take(35));
                var currentMomentum = VixMomentum(vixHistories.Skip(3));
                Plot("vix", "momentum", currentMomentum);

                if(currentMomentum > VixMomentumThreshold
                    && currentMomentum > pastMomentum)
                {
                    _targetLongCount = NumShort;
                    _targetShortCount = NumLong;
                    return;
                }

                if(currentMomentum < VixMomentumThreshold
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
                        && _momentums[x.Key].Current > MinLongMomentum
                        && (slice[x.Key] as BaseData).Price >= MinPrice
                        && _dollarVolumes.ContainsKey(x.Key)
                        && _dollarVolumes[x.Key] > MinDollarVolume
                        && _marketCaps.ContainsKey(x.Key)
                        && _marketCaps[x.Key] > MinMarketCap
                        && _opRevenueGrowth.ContainsKey(x.Key)
                        && _opRevenueGrowth[x.Key] > MinOpRevenueGrowth
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
                        && _dollarVolumes.ContainsKey(x.Key)
                        && _dollarVolumes[x.Key] > MinDollarVolume
                        && _marketCaps.ContainsKey(x.Key)
                        && _marketCaps[x.Key] > MinMarketCap
                        && _opRevenueGrowth.ContainsKey(x.Key)
                        && _opRevenueGrowth[x.Key] < MinOpRevenueGrowth
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
            _dollarVolumes.Clear();
            foreach(var candidate in candidates)
            {
                _dollarVolumes[candidate.Symbol] = candidate.DollarVolume;
            }

            if (!_universeMeter.IsDue(Time))
                return Universe.Unchanged;

            _longCandidates = candidates
                .Where(x => x.HasFundamentalData
                    )
                .Select(x => x.Symbol)
                .ToList();

            var shorts = Enumerable.Empty<Symbol>();

            return _longCandidates.Union(shorts);
        }

        private IEnumerable<Symbol> SelectFine(IEnumerable<FineFundamental> candidates)
        {
            _marketCaps.Clear();
            _opRevenueGrowth.Clear();
            foreach (var candidate in candidates)
            {
                _marketCaps[candidate.Symbol] = candidate.MarketCap;
                _opRevenueGrowth[candidate.Symbol] = candidate.OperationRatios.OperationRevenueGrowth3MonthAvg.Value;
            }

            if (!_universeMeter.IsDue(Time))
                return Universe.Unchanged;

            var longs = candidates
                .Where(
                    x => _longCandidates.Contains(x.Symbol)
                    && SectorsAllowed.Contains(x.AssetClassification.MorningstarSectorCode)
                    && ExchangesAllowed.Contains(x.SecurityReference.ExchangeId)
                    )
                .Select(x => x.Symbol)
                .ToList();
            _longCandidates = longs;

            var shorts = candidates
                .Where(
                    x => !_longCandidates.Contains(x.Symbol)
                    && SectorsAllowed.Contains(x.AssetClassification.MorningstarSectorCode)
                    && ExchangesAllowed.Contains(x.SecurityReference.ExchangeId)
                    )
                .Select(x => x.Symbol);
            _universeMeter.Update(Time);

            return _longCandidates.Union(shorts);
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
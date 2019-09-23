namespace QuantConnect.Algorithm.CSharp
{
    public class HorizontalVerticalAtmosphericScrubbers : QCAlgorithm
    {
        // TODO: 
        // optimize parameters for above and best performance
        // test MDY instead of SPY
        
        private static readonly int slowDays = 60;
        private static readonly decimal flipMargin = 0.04m;
        private string symbolInMarket = string.Empty;
        
        public override void Initialize()
        {
            SetStartDate(2003, 8, 4);
            SetEndDate(2019, 8, 30);
            SetCash(100000);
            var spy = AddEquity("MDY", Resolution.Daily);
            var tlt = AddEquity("TLT", Resolution.Daily);
            
            spy.SetDataNormalizationMode(DataNormalizationMode.Raw);
            tlt.SetDataNormalizationMode(DataNormalizationMode.Raw);
        }

        public override void OnData(Slice slice)
        {
            var spyMomentum = Momentum("MDY", slowDays);
            var tltMomentum = Momentum("TLT", slowDays);
            
            PlotPoints(spyMomentum, tltMomentum);
            
            if(spyMomentum > tltMomentum + flipMargin)
            {
                Rebalance("MDY", "TLT");
            }
            else if (tltMomentum > spyMomentum + flipMargin)
            {
                Rebalance("TLT", "MDY");
            }
        }
        
        private void PlotPoints(decimal spyMomentum, decimal tltMomentum)
        {
            Plot("momentum", "spyMomentum", (spyMomentum-1)*1);
            Plot("momentum", "tltMomentum", (tltMomentum-1)*1);
            
            Plot("price", "MDY", Securities["MDY"].Price);
            Plot("price", "tlt", Securities["TLT"].Price);
            
            Plot("leverage", "cash", Portfolio.Cash);
            Plot("leverage", "holdings", Portfolio.TotalHoldingsValue);
        }
        
        private void Rebalance(string buySymbol, string sellSymbol)
        {
            if(buySymbol == symbolInMarket && Portfolio.Cash < Portfolio.TotalHoldingsValue)
                return;
            
            Liquidate(sellSymbol);
            SetHoldings(buySymbol, 1m, false);
            symbolInMarket = buySymbol;
        }
        
        private int sharesToBuy(string symbol)
        {
            var value = Portfolio.Cash + Portfolio.TotalHoldingsValue;
            return (int)(value / Securities[symbol].Price);
        }
        
        private decimal Momentum(string symbol, int days)
        {
            var h = History<TradeBar>(symbol, days);
            return Securities[symbol].Price / h.First().Close;
        }
    }
}
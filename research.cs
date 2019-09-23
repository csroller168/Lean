namespace QuantConnect.Algorithm.CSharp
{
    public class HorizontalVerticalAtmosphericScrubbers : QCAlgorithm
    {
        // TODO: 
        // Audit to be sure the algo behaves as intended
            // issues
                // still periods of low leverage
                // long periods with nothing invested
        // optimize parameters for above and best performance
        // test MDY instead of SPY
        // NOTE: compare final performance with 0.0 flipMargin and no stoploss behavior
        
        private static readonly int slowDays = 60;
        private static readonly decimal flipMargin = 0.04m;
        //private static readonly decimal stopLossPct = 0.92m;
        //private static readonly decimal stopLossLimitPct = 0.90m;
        //private OrderTicket stopTicket = null; 
        //private DateTime? stopFilledDt = null;
        //private decimal peakPrice = Decimal.MinValue;
        private string symbolInMarket = string.Empty;
        
        public override void Initialize()
        {
            SetStartDate(2003, 8, 4);
            SetEndDate(2019, 8, 30);
            SetCash(100000);
            var spy = AddEquity("SPY", Resolution.Daily);
            var tlt = AddEquity("TLT", Resolution.Daily);
            
            spy.SetDataNormalizationMode(DataNormalizationMode.Raw);
            tlt.SetDataNormalizationMode(DataNormalizationMode.Raw);
        }

        public override void OnData(Slice slice)
        {
            var spyMomentum = Momentum("SPY", slowDays);
            var tltMomentum = Momentum("TLT", slowDays);
            
            PlotPoints(spyMomentum, tltMomentum);
            
            // If a stop hits, don't re-enter unless we'll flip
            //if(stopFilledDt != null)
            //{
            //  var desiredMomentum = symbolInMarket == "SPY" ? tltMomentum : spyMomentum;
            //  var stopMomentum = symbolInMarket == "SPY" ? spyMomentum : tltMomentum;
            //  if(desiredMomentum <= stopMomentum + flipMargin)
            //  {
            //      return;
            //  }
            //}
            
            if(spyMomentum > tltMomentum + flipMargin)
            {
                Rebalance("SPY", "TLT");
            }
            else if (tltMomentum > spyMomentum + flipMargin)
            {
                Rebalance("TLT", "SPY");
            }
            
            //if(!string.IsNullOrEmpty(symbolInMarket) &&
            //  Securities[symbolInMarket].Price > peakPrice)
            //{
            //  peakPrice = Securities[symbolInMarket].Price;
            //  stopTicket?.Update(new UpdateOrderFields() 
            //  { 
            //          StopPrice = stopLossPct * peakPrice
            //  });
            //}
        }
        
        //public override void OnOrderEvent(OrderEvent orderEvent)
        //{
            //return;
        //    if (orderEvent.Status != OrderStatus.Filled)
        //        return;
            
        //    if (stopTicket != null && orderEvent.OrderId == stopTicket.OrderId) 
        //    {
        //        stopFilledDt = Time;
        //    } 
        //    else if (orderEvent.Direction == OrderDirection.Buy) 
        //    {
        //      stopTicket = StopLimitOrder(
        //          orderEvent.Symbol, 
        //          Negative(orderEvent.FillQuantity), 
        //          orderEvent.FillPrice * stopLossPct,
        //          orderEvent.FillPrice * stopLossLimitPct);
        //    } 
        //    else 
        //    {
                // everything must go, all or nothing
        //      Liquidate(orderEvent.Symbol);
        //    }
        //}
        
        private void PlotPoints(decimal spyMomentum, decimal tltMomentum)
        {
            Plot("momentum", "spyMomentum", (spyMomentum-1)*1);
            Plot("momentum", "tltMomentum", (tltMomentum-1)*1);
            
            Plot("price", "spy", Securities["SPY"].Price);
            Plot("price", "tlt", Securities["TLT"].Price);
            
            Plot("leverage", "cash", Portfolio.Cash);
            Plot("leverage", "holdings", Portfolio.TotalHoldingsValue);
        }
        
        private void Rebalance(string buySymbol, string sellSymbol)
        {
            if(buySymbol == symbolInMarket && Portfolio.Cash < Portfolio.TotalHoldingsValue)
                return;
            
            //if(stopTicket?.Status != OrderStatus.Filled) 
            //{
            //  stopTicket?.Cancel();
            //}
            
            Liquidate(sellSymbol);
            //stopFilledDt = null;
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
        
        //private decimal Negative (decimal a)
        //{
       //   return a - a - a;
        //}
    }
}
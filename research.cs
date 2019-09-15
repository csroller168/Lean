namespace QuantConnect.Algorithm.CSharp
{
    public class HorizontalVerticalAtmosphericScrubbers : QCAlgorithm
    {
    	// TODO: 
    	// decide what to do in stop-loss event
    	//	if a stop-loss triggers, set a flag, stop trading entirely
    	//	re-enter with a clean slate if either security meets a short-term 
    	//		momentum threshold
    	//      maybe short term momentum trend? see charts in recent backtests
    	//      maybe nothing?
    	// look at perf pdf - figure out why so many long periods of inactivity
    	// optimize parameters for above and best performance
    	// test MDY instead of SPY
    	
    	private static readonly int slowDays = 60;
    	private static readonly int fastDays = 30;
    	private static readonly decimal flipMarginPct = 0.03m;
    	private static readonly decimal stopLossPct = 0.92m;
    	private OrderTicket stopTicket = null; 
        private DateTime? stopFilledDt = null;
        private decimal peakPrice = Decimal.MinValue;
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
        	
        	Plot("momentum", "spyMomentum", (spyMomentum-1)*1);
        	Plot("momentum", "tltMomentum", (tltMomentum-1)*1);
        	
        	Plot("price", "spy", Securities["SPY"].Price);
        	Plot("price", "tlt", Securities["TLT"].Price);
        	
        	if(spyMomentum > tltMomentum * (1+flipMarginPct))
        	{
        		Rebalance("SPY", "TLT", Momentum("SPY", fastDays));
        	}
        	else if (tltMomentum > spyMomentum * (1+flipMarginPct))
        	{
        		Rebalance("TLT", "SPY", Momentum("TLT", fastDays));
        	}
        	
        	if(!string.IsNullOrEmpty(symbolInMarket) &&
        		Securities[symbolInMarket].Price > peakPrice)
        	{
        		peakPrice = Securities[symbolInMarket].Price;
        		stopTicket?.Update(new UpdateOrderFields() { 
						StopPrice = stopLossPct * peakPrice
					});
        	}
        }
        
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status != OrderStatus.Filled)
                return;
            
            if (stopTicket != null && orderEvent.OrderId == stopTicket.OrderId) {
                stopFilledDt = Time;
            } else if (orderEvent.Direction == OrderDirection.Buy) {
            	stopTicket = StopMarketOrder(orderEvent.Symbol, Negative(orderEvent.FillQuantity), orderEvent.FillPrice * stopLossPct);
            }
        }
        
        private void Rebalance(string buySymbol, string sellSymbol, decimal momentum)
        {
        	stopTicket?.Cancel();
        	Liquidate(sellSymbol);
        	stopFilledDt = null;
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
        
        private decimal Negative (decimal a)
        {
        	return a - a - a;
        }
        
        private decimal One()
        {
        	return slowDays / slowDays;
        }
    }
}
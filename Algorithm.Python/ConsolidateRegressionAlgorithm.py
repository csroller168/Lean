﻿# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
# Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.


from clr import AddReference
AddReference("System")
AddReference("QuantConnect.Algorithm")
AddReference("QuantConnect.Common")

from System import *
from QuantConnect import *
from QuantConnect.Algorithm import *
from QuantConnect.Indicators import *
from QuantConnect.Securities import *
from QuantConnect.Data.Market import *
from QuantConnect.Data.Consolidators import *
from datetime import timedelta

### <summary>
### Regression algorithm reproducing data type bugs in the Consolidate API. Related to GH 4205.
### </summary>
class ConsolidateRegressionAlgorithm(QCAlgorithm):

    # Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
    def Initialize(self):
        self.SetStartDate(2013, 10, 8)
        self.SetEndDate(2013, 10, 9)

        SP500 = Symbol.Create(Futures.Indices.SP500EMini, SecurityType.Future, Market.USA)
        self._symbol = _symbol = self.FutureChainProvider.GetFutureContractList(SP500, self.StartDate)[0]
        self.AddFutureContract(_symbol)

        self._consolidationCount = [0, 0, 0]

        sma = SimpleMovingAverage(10)
        self.Consolidate(_symbol, Calendar.Monthly, lambda bar: self.UpdateTradeBar(sma, bar, -1)) # shouldn't consolidate

        sma2 = SimpleMovingAverage(10)
        self.Consolidate(_symbol, timedelta(1), lambda bar: self.UpdateTradeBar(sma2, bar, 0))

        sma3 = SimpleMovingAverage(10)
        self.Consolidate(_symbol, Resolution.Daily, TickType.Quote, lambda bar: self.UpdateQuoteBar(sma3, bar, 1))

        sma4 = SimpleMovingAverage(10)
        self.Consolidate(_symbol, timedelta(1), lambda bar: self.UpdateTradeBar(sma4, bar, 2))

    def UpdateTradeBar(self, sma, bar, position):
        self._consolidationCount[position] += 1
        sma.Update(bar.EndTime, bar.Volume)

    def UpdateQuoteBar(self, sma, bar, position):
        self._consolidationCount[position] += 1
        sma.Update(bar.EndTime, bar.Ask.High)

    def  OnEndOfAlgorithm(self):
        if any(i != 3 for i in self._consolidationCount):
            raise ValueError("Unexpected consolidation count")

    # OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
    def OnData(self, data):
        if not self.Portfolio.Invested:
           self.SetHoldings(self._symbol, 0.5)

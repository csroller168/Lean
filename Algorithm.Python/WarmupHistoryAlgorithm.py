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
AddReference("QuantConnect.Indicators")
AddReference("QuantConnect.Common")

from System import *
from QuantConnect import *
from QuantConnect.Data import *
from QuantConnect.Algorithm import *
from QuantConnect.Indicators import *

class WarmupHistoryAlgorithm(QCAlgorithm):
    '''This algorithm demonstrates using the history provider to
retrieve data to warm up indicators before data is received'''

    def Initialize(self):
        '''Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.'''

        self.SetStartDate(2014,5,2)   #Set Start Date
        self.SetEndDate(2014,5,2)     #Set End Date
        self.SetCash(100000)          #Set Strategy Cash
        # Find more symbols here: http://quantconnect.com/data
        forex = self.AddForex("EURUSD", Resolution.Second)
        
        fast_period = 60
        slow_period = 3600
        self.fast = self.EMA("EURUSD", fast_period)
        self.slow = self.EMA("EURUSD", slow_period)
        
        # "slow_period + 1" because rolling window waits for one to fall off the back to be considered ready
        history = map(lambda x: x["EURUSD"], self.History(slow_period + 1))
        for bar in history:
        	datapoint = IndicatorDataPoint(bar.EndTime, bar.Close)
        	self.fast.Update(datapoint)
        	self.slow.Update(datapoint)

        self.Log("FAST {0} READY. Samples: {1}".format("IS" if self.fast.IsReady else "IS NOT", self.fast.Samples))
        self.Log("SLOW {0} READY. Samples: {1}".format("IS" if self.slow.IsReady else "IS NOT", self.slow.Samples))


    def OnData(self, data):
        '''OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.'''
        
        if self.fast.Current.Value > self.slow.Current.Value:
            self.SetHoldings("EURUSD", 1)
        else:
            self.SetHoldings("EURUSD", -1)
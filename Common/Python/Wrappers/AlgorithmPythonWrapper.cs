﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using NodaTime;
using QuantConnect.Benchmarks;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Notifications;
using QuantConnect.Orders;
using QuantConnect.Scheduling;
using QuantConnect.Securities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Python.Runtime;
using ImpromptuInterface;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.Option;

namespace QuantConnect.Python.Wrappers
{
    /// <summary>
    /// Wrapper for an IAlgorithm instance created in Python.
    /// All calls to python should be inside a "using (Py.GIL()) {/* Your code here */}" block.
    /// </summary>
    public class AlgorithmPythonWrapper : IAlgorithm
    {
        private IAlgorithm _algorithm;
        private IBenchmark _benchmark;
        private IBrokerageModel _brokerageModel;
        private IBrokerageMessageHandler _brokerageMessageHandler;
        private IHistoryProvider _historyProvider;
        private ITradeBuilder _tradeBuilder;

        private dynamic _pyAlgorithm;

        // cache some properties to reduce C#/Python calls and/or data conversions
        private bool? _liveMode;
        private DateTime? _utcTime;
        private AlgorithmStatus? _status;
        private ScheduleManager _schedule;
        private SecurityManager _securities;
        private SecurityTransactionManager _transactions;
        private SecurityPortfolioManager _portfolio;
        private SubscriptionManager _subscriptionManager;
        private Dictionary<string, string> _runtimeStatistics;
        private ConcurrentQueue<string> _debugMessages;
        private ConcurrentQueue<string> _errorMessages;
        private ConcurrentQueue<string> _logMessages;
        private Exception _runTimeError;

        /// <summary>
        /// <see cref = "AlgorithmPythonWrapper"/> constructor.
        /// Creates and wraps the algorithm written in python.  
        /// </summary>
        /// <param name="module">Python module with the algorithm written in Python</param>
        public AlgorithmPythonWrapper(PyObject module)
        {
            _algorithm = null;

            try
            {
                using (Py.GIL())
                {
                    if (!module.HasAttr("QCAlgorithm"))
                    {
                        return;
                    }

                    var baseClass = module.GetAttr("QCAlgorithm");

                    // Load module with util methods
                    var onPythonData = Py.Import("AlgorithmPythonUtil").GetAttr("OnPythonData");

                    var moduleName = module.Repr().Split('\'')[1];

                    foreach (var name in module.Dir())
                    {
                        var attr = module.GetAttr(name.ToString());

                        if (attr.IsSubclass(baseClass) && attr.Repr().Contains(moduleName))
                        {
                            attr.SetAttr("OnPythonData", onPythonData);

                            _pyAlgorithm = attr.Invoke();
                            _algorithm = Impromptu.ActLike<IAlgorithm>(_pyAlgorithm);

                            return; 
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Log.Error(e);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.AlgorithmId" /> in Python
        /// </summary>
        public string AlgorithmId
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.AlgorithmId;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Benchmark" /> in Python
        /// </summary>
        public IBenchmark Benchmark
        {
            get
            {
                if (_benchmark == null)
                {
                    using (Py.GIL())
                    {
                        _benchmark = new BenchmarkPythonWrapper(_algorithm.Benchmark);
                    }
                }
                return _benchmark;
            }
        }

        /// <summary>
        /// Wrapper for <see cref="IAlgorithm.BrokerageMessageHandler" /> in Python
        /// </summary>
        public IBrokerageMessageHandler BrokerageMessageHandler
        {
            get
            {
                if (_brokerageMessageHandler == null)
                {
                    using (Py.GIL())
                    {
                        _brokerageMessageHandler = new BrokerageMessageHandlerPythonWrapper(_algorithm.BrokerageMessageHandler);
                    }
                }
                return _brokerageMessageHandler;
            }

            set
            {
                SetBrokerageMessageHandler(value);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.BrokerageModel" /> in Python
        /// </summary>
        public IBrokerageModel BrokerageModel
        {
            get
            {
                if (_brokerageModel == null)
                {
                    using (Py.GIL())
                    {
                        _brokerageModel = new BrokerageModelPythonWrapper(_algorithm.BrokerageModel);
                    }
                }
                return _brokerageModel;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.DebugMessages" /> in Python
        /// </summary>
        public ConcurrentQueue<string> DebugMessages
        {
            get
            {
                if (_debugMessages == null)
                {
                    using (Py.GIL())
                    {
                        _debugMessages = _algorithm.DebugMessages;
                    }
                }
                return _debugMessages;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.EndDate" /> in Python
        /// </summary>
        public DateTime EndDate
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.EndDate;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.ErrorMessages" /> in Python
        /// </summary>
        public ConcurrentQueue<string> ErrorMessages
        {
            get
            {
                if (_errorMessages == null)
                {
                    using (Py.GIL())
                    {
                        _errorMessages = _algorithm.ErrorMessages;
                    }
                }
                return _errorMessages;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.HistoryProvider" /> in Python
        /// </summary>
        public IHistoryProvider HistoryProvider
        {
            get
            {
                if (_historyProvider == null)
                {
                    using (Py.GIL())
                    {
                        _historyProvider = new HistoryProviderPythonWrapper(_algorithm.HistoryProvider);
                    }
                }
                return _historyProvider;
            }

            set
            {
                SetHistoryProvider(value);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.IsWarmingUp" /> in Python
        /// </summary>
        public bool IsWarmingUp
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.IsWarmingUp;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.LiveMode" /> in Python
        /// </summary>
        public bool LiveMode
        {
            get
            {
                if (_liveMode == null)
                {
                    using (Py.GIL())
                    {
                        _liveMode = _algorithm.LiveMode;
                    }
                }
                return _liveMode.Value;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.LogMessages" /> in Python
        /// </summary>
        public ConcurrentQueue<string> LogMessages
        {
            get
            {
                if (_logMessages == null)
                {
                    using (Py.GIL())
                    {
                        _logMessages = _algorithm.LogMessages;
                    }
                }
                return _logMessages;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Name" /> in Python
        /// </summary>
        public string Name
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.Name;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Notify" /> in Python
        /// </summary>
        public NotificationManager Notify
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.Notify;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Portfolio" /> in Python
        /// </summary>
        public SecurityPortfolioManager Portfolio
        {
            get
            {
                if (_portfolio == null)
                {
                    using (Py.GIL())
                    {
                        _portfolio = _algorithm.Portfolio;
                    }
                }
                return _portfolio;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.RunTimeError" /> in Python
        /// </summary>
        public Exception RunTimeError
        {
            get
            {
                return _runTimeError;
            }

            set
            {
                SetRunTimeError(value);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.RuntimeStatistics" /> in Python
        /// </summary>
        public Dictionary<string, string> RuntimeStatistics
        {
            get
            {
                if (_runtimeStatistics == null)
                {
                    using (Py.GIL())
                    {
                        _runtimeStatistics = _algorithm.RuntimeStatistics;
                    }
                }
                return _runtimeStatistics;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Schedule" /> in Python
        /// </summary>
        public ScheduleManager Schedule
        {
            get
            {
                if (_schedule == null)
                {
                    using (Py.GIL())
                    {
                        _schedule = _algorithm.Schedule;
                    }
                }
                return _schedule;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Securities" /> in Python
        /// </summary>
        public SecurityManager Securities
        {
            get
            {
                if (_securities == null)
                {
                    using (Py.GIL())
                    {
                        _securities = _algorithm.Securities;
                    }
                }
                return _securities;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SecurityInitializer" /> in Python
        /// </summary>
        public ISecurityInitializer SecurityInitializer
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.SecurityInitializer;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.TradeBuilder" /> in Python
        /// </summary>
        public ITradeBuilder TradeBuilder
        {
            get
            {
                if (_tradeBuilder == null)
                {
                    using (Py.GIL())
                    {
                        _tradeBuilder = new TradeBuilderPythonWrapper(_algorithm.TradeBuilder);
                    }
                }
                return _tradeBuilder;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.StartDate" /> in Python
        /// </summary>
        public DateTime StartDate
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.StartDate;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Status" /> in Python
        /// </summary>
        public AlgorithmStatus Status
        {
            get
            {
                if (_status == null)
                {
                    using (Py.GIL())
                    {
                        _status = _algorithm.Status;
                    }
                }
                return _status.Value;
            }

            set
            {
                SetStatus(value);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetStatus" /> in Python
        /// </summary>
        /// <param name="value"></param>
        public void SetStatus(AlgorithmStatus value)
        {
            using (Py.GIL())
            {
                _status = value;
                _algorithm.SetStatus(value);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetAvailableDataTypes" /> in Python
        /// </summary>
        /// <param name="availableDataTypes"></param>
        public void SetAvailableDataTypes(Dictionary<SecurityType, List<TickType>> availableDataTypes)
        {
            using (Py.GIL())
            {
                _algorithm.SetAvailableDataTypes(availableDataTypes);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SubscriptionManager" /> in Python
        /// </summary>
        public SubscriptionManager SubscriptionManager
        {
            get
            {
                if (_subscriptionManager == null)
                {
                    using (Py.GIL())
                    {
                        _subscriptionManager = _algorithm.SubscriptionManager;
                    }
                }
                return _subscriptionManager;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Time" /> in Python
        /// </summary>
        public DateTime Time
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.Time;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.TimeZone" /> in Python
        /// </summary>
        public DateTimeZone TimeZone
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.TimeZone;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Transactions" /> in Python
        /// </summary>
        public SecurityTransactionManager Transactions
        {
            get
            {
                if (_transactions == null)
                {
                    using (Py.GIL())
                    {
                        _transactions = _algorithm.Transactions;
                    }
                }
                return _transactions;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.UniverseManager" /> in Python
        /// </summary>
        public UniverseManager UniverseManager
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.UniverseManager;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.UniverseSettings" /> in Python
        /// </summary>
        public UniverseSettings UniverseSettings
        {
            get
            {
                using (Py.GIL())
                {
                    return _algorithm.UniverseSettings;
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.UtcTime" /> in Python
        /// </summary>
        public DateTime UtcTime
        {
            get
            {
                if (_utcTime == null)
                {
                    using (Py.GIL())
                    {
                        _utcTime = _algorithm.UtcTime;
                    }
                }
                return _utcTime.Value;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.AddSecurity" /> in Python
        /// </summary>
        /// <param name="securityType"></param>
        /// <param name="symbol"></param>
        /// <param name="resolution"></param>
        /// <param name="market"></param>
        /// <param name="fillDataForward"></param>
        /// <param name="leverage"></param>
        /// <param name="extendedMarketHours"></param>
        /// <returns></returns>
        public Security AddSecurity(SecurityType securityType, string symbol, Resolution resolution, string market, bool fillDataForward, decimal leverage, bool extendedMarketHours)
        {
            using (Py.GIL())
            {
                return _algorithm.AddSecurity(securityType, symbol, resolution, market, fillDataForward, leverage, extendedMarketHours);
            }
        }

        /// <summary>
        /// Creates and adds a new single <see cref="Future"/> contract to the algorithm
        /// </summary>
        /// <param name="symbol">The futures contract symbol</param>
        /// <param name="resolution">The <see cref="Resolution"/> of market data, Tick, Second, Minute, Hour, or Daily. Default is <see cref="Resolution.Minute"/></param>
        /// <param name="fillDataForward">If true, returns the last available data even if none in that timeslice. Default is <value>true</value></param>
        /// <param name="leverage">The requested leverage for this equity. Default is set by <see cref="SecurityInitializer"/></param>
        /// <returns>The new <see cref="Future"/> security</returns>
        public Future AddFutureContract(Symbol symbol, Resolution resolution = Resolution.Minute, bool fillDataForward = true, decimal leverage = 0m)
        {
            using (Py.GIL())
            {
                return _algorithm.AddFutureContract(symbol, resolution, fillDataForward, leverage);
            }
        }

        /// <summary>
        /// Creates and adds a new single <see cref="Option"/> contract to the algorithm
        /// </summary>
        /// <param name="symbol">The option contract symbol</param>
        /// <param name="resolution">The <see cref="Resolution"/> of market data, Tick, Second, Minute, Hour, or Daily. Default is <see cref="Resolution.Minute"/></param>
        /// <param name="fillDataForward">If true, returns the last available data even if none in that timeslice. Default is <value>true</value></param>
        /// <param name="leverage">The requested leverage for this equity. Default is set by <see cref="SecurityInitializer"/></param>
        /// <returns>The new <see cref="Option"/> security</returns>
        public Option AddOptionContract(Symbol symbol, Resolution resolution = Resolution.Minute, bool fillDataForward = true, decimal leverage = 0m)
        {
            using (Py.GIL())
            {
                return _algorithm.AddOptionContract(symbol, resolution, fillDataForward, leverage);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Debug" /> in Python
        /// </summary>
        /// <param name="message"></param>
        public void Debug(string message)
        {
            using (Py.GIL())
            {
                _algorithm.Debug(message);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Error" /> in Python
        /// </summary>
        /// <param name="message"></param>
        public void Error(string message)
        {
            using (Py.GIL())
            {
                _algorithm.Error(message);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.GetChartUpdates" /> in Python
        /// </summary>
        /// <param name="clearChartData"></param>
        /// <returns></returns>
        public List<Chart> GetChartUpdates(bool clearChartData = false)
        {
            using (Py.GIL())
            {
                return _algorithm.GetChartUpdates(clearChartData);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.GetLocked" /> in Python
        /// </summary>
        /// <returns></returns>
        public bool GetLocked()
        {
            using (Py.GIL())
            {
                return _algorithm.GetLocked();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.GetParameter" /> in Python
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetParameter(string name)
        {
            using (Py.GIL())
            {
                return _algorithm.GetParameter(name);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.GetWarmupHistoryRequests" /> in Python
        /// </summary>
        /// <returns></returns>
        public IEnumerable<HistoryRequest> GetWarmupHistoryRequests()
        {
            using (Py.GIL())
            {
                return _algorithm.GetWarmupHistoryRequests().ToList();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Initialize" /> in Python
        /// </summary>
        public void Initialize()
        {
            using (Py.GIL())
            {
                _algorithm.Initialize();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Liquidate" /> in Python
        /// </summary>
        /// <param name="symbolToLiquidate"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public List<int> Liquidate(Symbol symbolToLiquidate = null, string tag = "Liquidated")
        {
            using (Py.GIL())
            {
                return _algorithm.Liquidate(symbolToLiquidate, tag);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.Log" /> in Python
        /// </summary>
        /// <param name="message"></param>
        public void Log(string message)
        {
            using (Py.GIL())
            {
                _algorithm.Log(message);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnBrokerageDisconnect" /> in Python
        /// </summary>
        public void OnBrokerageDisconnect()
        {
            using (Py.GIL())
            {
                _algorithm.OnBrokerageDisconnect();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnBrokerageMessage" /> in Python
        /// </summary>
        /// <param name="messageEvent"></param>
        public void OnBrokerageMessage(BrokerageMessageEvent messageEvent)
        {
            using (Py.GIL())
            {
                _algorithm.OnBrokerageMessage(messageEvent);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnBrokerageReconnect" /> in Python
        /// </summary>
        public void OnBrokerageReconnect()
        {
            using (Py.GIL())
            {
                _algorithm.OnBrokerageReconnect();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnData" /> in Python
        /// </summary>
        public void OnData(Slice slice)
        {
            using (Py.GIL())
            {
                if (_subscriptionManager.HasCustomData)
                {
                    _pyAlgorithm.OnPythonData(slice);
                }
                else
                {
                    _algorithm.OnData(slice);
                }
            }
        }
        
        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnEndOfAlgorithm" /> in Python
        /// </summary>
        public void OnEndOfAlgorithm()
        {
            using (Py.GIL())
            {
                _algorithm.OnEndOfAlgorithm();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnEndOfDay()" /> in Python
        /// </summary>
        public void OnEndOfDay()
        {
            using (Py.GIL())
            {
                _algorithm.OnEndOfDay();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnEndOfDay(Symbol)" /> in Python
        /// </summary>
        /// <param name="symbol"></param>
        public void OnEndOfDay(Symbol symbol)
        {
            using (Py.GIL())
            {
                _algorithm.OnEndOfDay(symbol);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnMarginCall" /> in Python
        /// </summary>
        /// <param name="requests"></param>
        public void OnMarginCall(List<SubmitOrderRequest> requests)
        {
            using (Py.GIL())
            {
                _algorithm.OnMarginCall(requests);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnMarginCallWarning" /> in Python
        /// </summary>
        public void OnMarginCallWarning()
        {
            using (Py.GIL())
            {
                _algorithm.OnMarginCallWarning();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnOrderEvent" /> in Python
        /// </summary>
        /// <param name="newEvent"></param>
        public void OnOrderEvent(OrderEvent newEvent)
        {
            using (Py.GIL())
            {
                _algorithm.OnOrderEvent(newEvent);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnAssignmentOrderEvent" /> in Python
        /// </summary>
        /// <param name="newEvent"></param>
        public void OnAssignmentOrderEvent(OrderEvent newEvent)
        {
            using (Py.GIL())
            {
                _algorithm.OnAssignmentOrderEvent(newEvent);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.OnSecuritiesChanged" /> in Python
        /// </summary>
        /// <param name="changes"></param>
        public void OnSecuritiesChanged(SecurityChanges changes)
        {
            using (Py.GIL())
            {
                _algorithm.OnSecuritiesChanged(changes);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.PostInitialize" /> in Python
        /// </summary>
        public void PostInitialize()
        {
            using (Py.GIL())
            {
                _algorithm.PostInitialize();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.RemoveSecurity" /> in Python
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public bool RemoveSecurity(Symbol symbol)
        {
            using (Py.GIL())
            {
                return _algorithm.RemoveSecurity(symbol);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetAlgorithmId" /> in Python
        /// </summary>
        /// <param name="algorithmId"></param>
        public void SetAlgorithmId(string algorithmId)
        {
            using (Py.GIL())
            {
                _algorithm.SetAlgorithmId(algorithmId);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetBrokerageMessageHandler" /> in Python
        /// </summary>
        /// <param name="brokerageMessageHandler"></param>
        public void SetBrokerageMessageHandler(IBrokerageMessageHandler brokerageMessageHandler)
        {
            using (Py.GIL())
            {
                _algorithm.SetBrokerageMessageHandler(brokerageMessageHandler);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetBrokerageModel" /> in Python
        /// </summary>
        /// <param name="brokerageModel"></param>
        public void SetBrokerageModel(IBrokerageModel brokerageModel)
        {
            using (Py.GIL())
            {
                _algorithm.SetBrokerageModel(new BrokerageModelPythonWrapper(brokerageModel));
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetCash(decimal)" /> in Python
        /// </summary>
        /// <param name="startingCash"></param>
        public void SetCash(decimal startingCash)
        {
            using (Py.GIL())
            {
                _algorithm.SetCash(startingCash);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetCash(string, decimal, decimal)" /> in Python
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="startingCash"></param>
        /// <param name="conversionRate"></param>
        public void SetCash(string symbol, decimal startingCash, decimal conversionRate)
        {
            using (Py.GIL())
            {
                _algorithm.SetCash(symbol, startingCash, conversionRate);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetDateTime" /> in Python
        /// </summary>
        /// <param name="time"></param>
        public void SetDateTime(DateTime time)
        {
            using (Py.GIL())
            {
                _algorithm.SetDateTime(time);
                _utcTime = time;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetRunTimeError" /> in Python
        /// </summary>
        /// <param name="exception"></param>
        public void SetRunTimeError(Exception exception)
        {
            using (Py.GIL())
            {
                _algorithm.SetRunTimeError(exception);
                _runTimeError = exception;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetFinishedWarmingUp" /> in Python
        /// </summary>
        public void SetFinishedWarmingUp()
        {
            using (Py.GIL())
            {
                _algorithm.SetFinishedWarmingUp();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetHistoryProvider" /> in Python
        /// </summary>
        /// <param name="historyProvider"></param>
        public void SetHistoryProvider(IHistoryProvider historyProvider)
        {
            using (Py.GIL())
            {
                _algorithm.SetHistoryProvider(new HistoryProviderPythonWrapper(historyProvider));
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetLiveMode" /> in Python
        /// </summary>
        /// <param name="live"></param>
        public void SetLiveMode(bool live)
        {
            using (Py.GIL())
            {
                _algorithm.SetLiveMode(live);
                _liveMode = live;
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetLocked" /> in Python
        /// </summary>
        public void SetLocked()
        {
            using (Py.GIL())
            {
                _algorithm.SetLocked();
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetMaximumOrders" /> in Python
        /// </summary>
        /// <param name="max"></param>
        public void SetMaximumOrders(int max)
        {
            using (Py.GIL())
            {
                _algorithm.SetMaximumOrders(max);
            }
        }

        /// <summary>
        /// Wrapper for <see cref = "IAlgorithm.SetParameters" /> in Python
        /// </summary>
        /// <param name="parameters"></param>
        public void SetParameters(Dictionary<string, string> parameters)
        {
            using (Py.GIL())
            {
                _algorithm.SetParameters(parameters);
            }
        }
    }
}

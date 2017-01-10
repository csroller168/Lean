/*
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
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using System.Threading;
using QuantConnect.Data.Auxiliary;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators.Factories
{
    /// <summary>
    /// Provides an implementation of <see cref="ISubscriptionEnumeratorFactory"/> for the <see cref="FuturesChainUniverse"/> in backtesting
    /// </summary>
    public class FuturesChainUniverseSubscriptionEnumeratorFactory : ISubscriptionEnumeratorFactory
    {
        private readonly Func<SubscriptionRequest, IEnumerator<BaseData>, IEnumerator<BaseData>> _enumeratorConfigurator;
        private readonly bool _isLiveMode;

        private readonly IDataQueueUniverseProvider _symbolUniverse;
        private readonly ITimeProvider _timeProvider;

        private readonly IDataFileCacheProvider _dataFileCacheProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuturesChainUniverseSubscriptionEnumeratorFactory"/> class
        /// </summary>
        /// <param name="enumeratorConfigurator">Function used to configure the sub-enumerators before sync (fill-forward/filter/ect...)</param>
        /// <param name="dataFileCacheProvider">Provider used to get data when it is not present on disk</param>
        public FuturesChainUniverseSubscriptionEnumeratorFactory(Func<SubscriptionRequest, IEnumerator<BaseData>, IEnumerator<BaseData>> enumeratorConfigurator,
                                                          IDataFileCacheProvider dataFileCacheProvider)
        {
            _isLiveMode = false;
            _enumeratorConfigurator = enumeratorConfigurator;
            _dataFileCacheProvider = dataFileCacheProvider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FuturesChainUniverseSubscriptionEnumeratorFactory"/> class
        /// </summary>
        /// <param name="symbolUniverse">Symbol universe provider of the data queue</param>
        /// <param name="dataFileCacheProvider">Provider used to get data when it is not present on disk</param>
        public FuturesChainUniverseSubscriptionEnumeratorFactory(IDataQueueUniverseProvider symbolUniverse, ITimeProvider timeProvider,
                                                                IDataFileCacheProvider dataFileCacheProvider)
        {
            _isLiveMode = true;
            _symbolUniverse = symbolUniverse;
            _timeProvider = timeProvider;
            _enumeratorConfigurator = (sr, input) => input;
            _dataFileCacheProvider = dataFileCacheProvider;
        }

        /// <summary>
        /// Creates an enumerator to read the specified request
        /// </summary>
        /// <param name="request">The subscription request to be read</param>
        /// <param name="dataFileProvider">Provider used to get data when it is not present on disk</param>
        /// <returns>An enumerator reading the subscription request</returns>
        public IEnumerator<BaseData> CreateEnumerator(SubscriptionRequest request, IDataFileProvider dataFileProvider)
        {
            if (_isLiveMode)
            {
                var localTime = request.StartTimeUtc.ConvertFromUtc(request.Configuration.ExchangeTimeZone);
                
                // loading the list of futures contracts and converting them into zip entries
                var symbols = _symbolUniverse.LookupSymbols(request.Security.Symbol.Underlying.Value, request.Security.Type);
                var zipEntries = symbols.Select(x => new ZipEntryName { Time = localTime, Symbol = x } as BaseData).ToList();

                var underlyingEnumerator = new TradeBarBuilderEnumerator(request.Configuration.Increment, request.Security.Exchange.TimeZone, _timeProvider);
                underlyingEnumerator.ProcessData(new Tick { Value = 0 });

                // configuring the enumerator
                var subscriptionConfiguration = GetSubscriptionConfigurations(request).First();
                var subscriptionRequest = new SubscriptionRequest(request, configuration: subscriptionConfiguration);
                var configuredEnumerator = _enumeratorConfigurator(subscriptionRequest, underlyingEnumerator);

                return new DataQueueFuturesChainUniverseDataCollectionEnumerator(request.Security.Symbol, configuredEnumerator, zipEntries);
            }
            else
            {
                var factory = new BaseDataSubscriptionEnumeratorFactory(_dataFileCacheProvider);

                var enumerators = GetSubscriptionConfigurations(request)
                    .Select(c => new SubscriptionRequest(request, configuration: c))
                    .Select(sr => _enumeratorConfigurator(request, factory.CreateEnumerator(sr, dataFileProvider)));

                var sync = new SynchronizingEnumerator(enumerators);
                return new FuturesChainUniverseDataCollectionAggregatorEnumerator(sync, request.Security.Symbol);
            }
        }

        private IEnumerable<SubscriptionDataConfig> GetSubscriptionConfigurations(SubscriptionRequest request)
        {
            // canonical also needs underlying price data
            var config = request.Configuration;
            var underlying = config.Symbol.Underlying;
            var resolution = config.Resolution;

            var configurations = new List<SubscriptionDataConfig>
            {
                // add underlying trade data
                new SubscriptionDataConfig(config, resolution: resolution, fillForward: true, symbol: underlying, objectType: typeof (TradeBar), tickType: TickType.Trade),
            };

            if (!_isLiveMode)
            {
                // rewrite the primary to be fill forward
                configurations.Add(new SubscriptionDataConfig(config, resolution: resolution, fillForward: true));
            }

            return configurations;
        }
    }
}
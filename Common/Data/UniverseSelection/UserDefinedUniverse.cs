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

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Represents the universe defined by the user's algorithm. This is
    /// the default universe where manually added securities live by
    /// market/security type. They can also be manually generated and
    /// can be configured to fire on certain interval and will always
    /// return the internal list of symbols.
    /// </summary>
    public class UserDefinedUniverse : Universe, INotifyCollectionChanged
    {
        private readonly TimeSpan _interval;
        private readonly HashSet<Symbol> _symbols;
        private readonly SubscriptionSettings _subscriptionSettings;
        private readonly Func<DateTime, IEnumerable<Symbol>> _selector;

        /// <summary>
        /// Event fired when a symbol is added or removed from this universe
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Gets the interval of this user defined universe
        /// </summary>
        public TimeSpan Interval
        {
            get { return _interval; }
        }

        /// <summary>
        /// Gets the settings used for subscriptons added for this universe
        /// </summary>
        public override SubscriptionSettings SubscriptionSettings
        {
            get { return _subscriptionSettings; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDefinedUniverse"/> class
        /// </summary>
        /// <param name="configuration">The configuration used to resolve the data for universe selection</param>
        /// <param name="subscriptionSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="interval">The interval at which selection should be performed</param>
        /// <param name="symbols">The initial set of symbols in this universe</param>
        public UserDefinedUniverse(SubscriptionDataConfig configuration, SubscriptionSettings subscriptionSettings, TimeSpan interval, IEnumerable<Symbol> symbols)
            : base(configuration)
        {
            _interval = interval;
            _symbols = symbols.ToHashSet();
            _subscriptionSettings = subscriptionSettings;
            _selector = time => _symbols;

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDefinedUniverse"/> class
        /// </summary>
        /// <param name="configuration">The configuration used to resolve the data for universe selection</param>
        /// <param name="subscriptionSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="interval">The interval at which selection should be performed</param>
        /// <param name="selector">Universe selection function invoked for each time returned via GetTriggerTimes</param>
        public UserDefinedUniverse(SubscriptionDataConfig configuration, SubscriptionSettings subscriptionSettings, TimeSpan interval, Func<DateTime,IEnumerable<string>> selector)
            : base(configuration)
        {
            _interval = interval;
            _subscriptionSettings = subscriptionSettings;
            _selector = time => selector(time).Select(sym => Symbol.Create(sym, Configuration.SecurityType, Configuration.Market));
        }

        /// <summary>
        /// Creates a user defined universe symbol
        /// </summary>
        /// <param name="securityType">The security</param>
        /// <param name="market">The market</param>
        /// <returns>A symbol for user defined universe of the specified security type and market</returns>
        public static Symbol CreateSymbol(SecurityType securityType, string market)
        {
            var ticker = string.Format("qc-universe-userdefined-{0}-{1}", market.ToLower(), securityType);
            SecurityIdentifier sid;
            switch (securityType)
            {
                case SecurityType.Base:
                    sid = SecurityIdentifier.GenerateBase(ticker, market);
                    break;
                
                case SecurityType.Equity:
                    sid = SecurityIdentifier.GenerateEquity(SecurityIdentifier.DefaultDate, ticker, market);
                    break;
                
                case SecurityType.Option:
                    sid = SecurityIdentifier.GenerateOption(SecurityIdentifier.DefaultDate, ticker, market, 0, 0, 0);
                    break;
                
                case SecurityType.Forex:
                    sid = SecurityIdentifier.GenerateForex(ticker, market);
                    break;

                case SecurityType.Commodity:
                case SecurityType.Future:
                case SecurityType.Cfd:
                default:
                    throw new NotImplementedException("The specified security type is not implemented yet: " + securityType);
            }

            return new Symbol(sid, ticker);
        }

        /// <summary>
        /// Adds the specified symbol to this universe
        /// </summary>
        /// <param name="symbol">The symbol to be added to this universe</param>
        /// <returns>True if the symbol was added, false if it was already present</returns>
        public bool Add(Symbol symbol)
        {
            if (_symbols.Add(symbol))
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, symbol));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the specified symbol from this universe
        /// </summary>
        /// <param name="symbol">The symbol to be removed</param>
        /// <returns>True if the symbol was removed, false if the symbol was not present</returns>
        public bool Remove(Symbol symbol)
        {
            if (_symbols.Remove(symbol))
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, symbol));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the symbols defined by the user for this universe
        /// </summary>
        /// <param name="utcTime">The curren utc time</param>
        /// <param name="data">The symbols to remain in the universe</param>
        /// <returns>The data that passes the filter</returns>
        public override IEnumerable<Symbol> SelectSymbols(DateTime utcTime, IEnumerable<BaseData> data)
        {
            return _selector(utcTime);
        }

        /// <summary>
        /// Returns an enumerator that defines when this user defined universe will be invoked
        /// </summary>
        /// <returns>An enumerator of DateTime that defines when this universe will be invoked</returns>
        public virtual IEnumerable<DateTime> GetTriggerTimes(DateTime startTimeUtc, DateTime endTimeUtc, MarketHoursDatabase marketHoursDatabase)
        {
            var exchangeHours = marketHoursDatabase.GetExchangeHours(Configuration);
            var localStartTime = startTimeUtc.ConvertFromUtc(Configuration.ExchangeTimeZone);
            var localEndTime = endTimeUtc.ConvertFromUtc(Configuration.ExchangeTimeZone);

            var first = true;
            foreach (var dateTime in LinqExtensions.Range(localStartTime, localEndTime, dt => dt + Interval))
            {
                if (first)
                {
                    yield return dateTime;
                    first = false;
                }
                if (exchangeHours.IsOpen(dateTime, dateTime + Interval, Configuration.ExtendedMarketHours))
                {
                    yield return dateTime;
                }
            }
        }

        /// <summary>
        /// Event invocator for the <see cref="CollectionChanged"/> event
        /// </summary>
        /// <param name="e">The notify collection changed event arguments</param>
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var handler = CollectionChanged;
            if (handler != null) handler(this, e);
        }
    }
}

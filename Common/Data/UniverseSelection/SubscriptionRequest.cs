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
*/

using System;
using QuantConnect.Securities;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Defines the parameters required to add a subscription to a data feed.
    /// </summary>
    public class SubscriptionRequest
    {
        private readonly Lazy<DateTime> _localStartTime;
        private readonly Lazy<DateTime> _localEndTime; 

        /// <summary>
        /// Gets true if the subscription is a universe
        /// </summary>
        public bool IsUniverseSubscription { get; private set; }

        /// <summary>
        /// Gets the universe this subscription resides in
        /// </summary>
        public Universe Universe { get; private set; }

        /// <summary>
        /// Gets the security. This is the destination of data for non-internal subscriptions.
        /// </summary>
        public Security Security { get; private set; }

        /// <summary>
        /// Gets the subscription configuration. This defines how/where to read the data.
        /// </summary>
        public SubscriptionDataConfig Configuration { get; private set; }

        /// <summary>
        /// Gets the beginning of the requested time interval in UTC
        /// </summary>
        public DateTime StartTimeUtc { get; private set; }

        /// <summary>
        /// Gets the end of the requested time interval in UTC
        /// </summary>
        public DateTime EndTimeUtc { get; private set; }

        /// <summary>
        /// Gets the <see cref="StartTimeUtc"/> in the security's exchange time zone
        /// </summary>
        public DateTime StartTimeLocal
        {
            get { return _localStartTime.Value; }
        }

        /// <summary>
        /// Gets the <see cref="EndTimeUtc"/> in the security's exchange time zone
        /// </summary>
        public DateTime EndTimeLocal
        {
            get { return _localEndTime.Value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionRequest"/> class
        /// </summary>
        public SubscriptionRequest(bool isUniverseSubscription,
            Universe universe,
            Security security,
            SubscriptionDataConfig configuration,
            DateTime startTimeUtc,
            DateTime endTimeUtc)
        {
            IsUniverseSubscription = isUniverseSubscription;
            Universe = universe;
            Security = security;
            Configuration = configuration;
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;

            _localStartTime = new Lazy<DateTime>(() => StartTimeUtc.ConvertFromUtc(Configuration.ExchangeTimeZone));
            _localEndTime = new Lazy<DateTime>(() => EndTimeUtc.ConvertFromUtc(Configuration.ExchangeTimeZone));
        }
    }
}
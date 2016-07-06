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
 *
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators.Factories
{
    /// <summary>
    /// Provides an implementation of <see cref="ISubscriptionEnumeratorFactory"/> that reads
    /// an entire <see cref="SubscriptionDataSource"/> into a single <see cref="FineFundamental"/>
    /// to be emitted on the tradable date at midnight
    /// </summary>
    public class FineFundamentalSubscriptionEnumeratorFactory : ISubscriptionEnumeratorFactory
    {
        private readonly Func<SubscriptionRequest, IEnumerable<DateTime>> _tradableDaysProvider;
        private string _lastUsedFileName;

        /// <summary>
        /// Initializes a new instance of the <see cref="FineFundamentalSubscriptionEnumeratorFactory"/> class.
        /// </summary>
        /// <param name="tradableDaysProvider">Function used to provide the tradable dates to the enumerator.
        /// Specify null to default to <see cref="SubscriptionRequest.TradableDays"/></param>
        public FineFundamentalSubscriptionEnumeratorFactory(Func<SubscriptionRequest, IEnumerable<DateTime>> tradableDaysProvider = null)
        {
            _tradableDaysProvider = tradableDaysProvider ?? (request => request.TradableDays);
        }

        /// <summary>
        /// Creates an enumerator to read the specified request
        /// </summary>
        /// <param name="request">The subscription request to be read</param>
        /// <returns>An enumerator reading the subscription request</returns>
        public IEnumerator<BaseData> CreateEnumerator(SubscriptionRequest request)
        {
            var tradableDays = _tradableDaysProvider(request);

            var fineFundamental = new FineFundamental();
            var fineFundamentalConfiguration = new SubscriptionDataConfig(request.Configuration, typeof(FineFundamental), request.Security.Symbol);

            return (
                from date in tradableDays

                let fineFundamentalSource = GetSource(fineFundamental, fineFundamentalConfiguration, date)
                let fineFundamentalFactory = SubscriptionDataSourceReader.ForSource(fineFundamentalSource, fineFundamentalConfiguration, date, false)
                let fineFundamentalForDate = (FineFundamental)fineFundamentalFactory.Read(fineFundamentalSource).FirstOrDefault()

                select new FineFundamental
                {
                    DataType = MarketDataType.Auxiliary,
                    Symbol = request.Configuration.Symbol,
                    Time = date,
                    CompanyReference = fineFundamentalForDate != null ? fineFundamentalForDate.CompanyReference : null,
                    SecurityReference = fineFundamentalForDate != null ? fineFundamentalForDate.SecurityReference : null,
                    FinancialStatements = fineFundamentalForDate != null ? fineFundamentalForDate.FinancialStatements : null,
                    EarningReports = fineFundamentalForDate != null ? fineFundamentalForDate.EarningReports : null,
                    OperationRatios = fineFundamentalForDate != null ? fineFundamentalForDate.OperationRatios : null,
                    EarningRatios = fineFundamentalForDate != null ? fineFundamentalForDate.EarningRatios : null,
                    ValuationRatios = fineFundamentalForDate != null ? fineFundamentalForDate.ValuationRatios : null
                }
                ).GetEnumerator();
        }

        /// <summary>
        /// Returns a SubscriptionDataSource for the FineFundamental class, 
        /// returning data from a previous date if not available for the requested date
        /// </summary>
        private SubscriptionDataSource GetSource(FineFundamental fine, SubscriptionDataConfig config, DateTime date)
        {
            var source = fine.GetSource(config, date, false);

            var fileName = date.ToString("yyyyMMdd");

            if (!File.Exists(source.Source))
            {
                if (_lastUsedFileName == null)
                {
                    // find first file date
                    var path = Path.GetDirectoryName(source.Source) ?? string.Empty;
                    var firstFileName = Path.GetFileNameWithoutExtension(Directory.GetFiles(path, "*.zip").OrderBy(x => x).First());
                    var firstDate = DateTime.ParseExact(firstFileName, "yyyyMMdd", CultureInfo.InvariantCulture);

                    // requested date before first date, return current invalid source anyway
                    if (date < firstDate)
                        return source;

                    // requested date after first date, save date of first existing file
                    _lastUsedFileName = firstFileName;

                    // loop back in time until we find an existing file
                    while (string.CompareOrdinal(fileName, _lastUsedFileName) > 0)
                    {
                        // get previous date
                        date = date.AddDays(-1);

                        // get file name for this date
                        source = fine.GetSource(config, date, false);
                        fileName = Path.GetFileNameWithoutExtension(source.Source);

                        if (!File.Exists(source.Source))
                            continue;

                        // we found the file, save its name and return the source
                        _lastUsedFileName = fileName;

                        break;
                    }
                }
                else
                {
                    // return source for last existing file date
                    date = DateTime.ParseExact(_lastUsedFileName, "yyyyMMdd", CultureInfo.InvariantCulture);
                    source = fine.GetSource(config, date, false);
                }
            }
            else
            {
                _lastUsedFileName = fileName;
            }

            return source;
        }
    }
}
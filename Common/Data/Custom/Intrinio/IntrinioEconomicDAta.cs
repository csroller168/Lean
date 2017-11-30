﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Configuration;

namespace QuantConnect.Data.Custom.Intrinio
{
    /// <summary>
    /// TRanformation available for the Economic data.
    /// </summary>
    public enum IntrinioDataTransformation
    {
        /// <summary>
        /// The rate of change
        /// </summary>
        Roc,
        /// <summary>
        /// Rate of change from Year Ago
        /// </summary>
        AnnualyRoc,
        /// <summary>
        /// The compounded annual rate of change
        /// </summary>
        CompoundedAnnualRoc,
        /// <summary>
        /// The continuously compounded annual rate of change
        /// </summary>
        AnnualyCCRoc,
        /// <summary>
        /// The continuously compounded rateof change
        /// </summary>
        CCRoc,
        /// <summary>
        /// The level, no transformation.
        /// </summary>
        Level,
        /// <summary>
        /// The natural log
        /// </summary>
        Ln,
        /// <summary>
        /// The percent change
        /// </summary>
        Pc,
        /// <summary>
        /// The percent change from year ago
        /// </summary>
        AnnualyPc,
    }

    /// <summary>
    /// Access the massive repository of economic data from the Federal Reserve Economic Data system via the Intrinio API.
    /// </summary>
    /// <seealso cref="QuantConnect.Data.BaseData" />
    public class IntrinioEconomicData : BaseData
    {
        private readonly string _user = Config.Get("intrinio-username");
        private readonly string _password = Config.Get("intrinio-password");

        private bool _firstTime = true;

        private string _baseUrl = @"https://api.intrinio.com/historical_data.csv?sort_order=asc&";
        private readonly IntrinioDataTransformation _dataTransformation;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntrinioEconomicData"/> class.
        /// </summary>
        public IntrinioEconomicData() : this(IntrinioDataTransformation.Level)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntrinioEconomicData"/> class.
        /// </summary>
        /// <param name="dataTransformation">The item.</param>
        public IntrinioEconomicData(IntrinioDataTransformation dataTransformation) { _dataTransformation = dataTransformation; }


        /// <summary>
        /// Return the URL string source of the file. This will be converted to a stream
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="date">Date of this source file</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>
        /// String URL of source file.
        /// </returns>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            SubscriptionDataSource subscription;
            if (_firstTime)
            {
                var item = GetStringForDataTransformation(_dataTransformation);
                var url = string.Format("{0}identifier={1}&item={2}", _baseUrl,
                                        config.Symbol.Value, item);
                var byteKey = Encoding.ASCII.GetBytes(string.Format("{0}:{1}", _user, _password));
                var authorizationHeaders = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("Authorization",
                                                     string.Format("Basic ({0})", Convert.ToBase64String(byteKey)))
                };
                _firstTime = false;
                subscription = new SubscriptionDataSource(url, SubscriptionTransportMedium.RemoteFile, FileFormat.Csv,
                                                          authorizationHeaders);
            }
            else
            {
                subscription = new SubscriptionDataSource("", SubscriptionTransportMedium.RemoteFile);
            }
            return subscription;
        }

        /// <summary>
        /// Reader converts each line of the data source into BaseData objects. Each data type creates its own factory method, and returns a new instance of the object
        /// each time it is called. The returned object is assumed to be time stamped in the config.ExchangeTimeZone.
        /// </summary>
        /// <param name="config">Subscription data config setup object</param>
        /// <param name="line">Line of the source document</param>
        /// <param name="date">Date of the requested data</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>
        /// Instance of the T:BaseData object generated by this line of the CSV
        /// </returns>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            var obs = line.Split(',');
            var time = DateTime.MinValue;
            if (!DateTime.TryParseExact(obs[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) return null;
            var value = obs[1].ToDecimal();
            return new IntrinioEconomicData
            {
                Symbol = config.Symbol,
                Time = time,
                //EndTime = time + QuantConnect.Time.OneDay,
                Value = value,
                //DataType = MarketDataType.Auxiliary
            };
        }

        private static string GetStringForDataTransformation(IntrinioDataTransformation dataTransformation)
        {
            var item = "level";
            switch (dataTransformation)
            {
                case IntrinioDataTransformation.Roc:
                    item = "change";
                    break;
                case IntrinioDataTransformation.AnnualyRoc:
                    item = "yr_change";
                    break;
                case IntrinioDataTransformation.CompoundedAnnualRoc:
                    item = "c_annual_roc";
                    break;
                case IntrinioDataTransformation.AnnualyCCRoc:
                    item = "cc_annual_roc";
                    break;
                case IntrinioDataTransformation.CCRoc:
                    item = "cc_roc";
                    break;
                case IntrinioDataTransformation.Level:
                    item = "level";
                    break;
                case IntrinioDataTransformation.Ln:
                    item = "log";
                    break;
                case IntrinioDataTransformation.Pc:
                    item = "percent_change";
                    break;
                case IntrinioDataTransformation.AnnualyPc:
                    item = "yr_percent_change";
                    break;
            }
            return item;
        }
    }
}

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
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Logging;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Attempts to download data from the api and save it in the data folder specified in config.json.
    /// This implementation will overwrite data if it already exists.
    /// </summary>
    public class ApiDataFileProvider : IDataFileProvider
    {
        private readonly int _uid = Config.GetInt("job-user-id", 0);
        private readonly string _token = Config.Get("api-access-token", "1");
        private readonly string _dataPath = Config.Get("data-folder", "../../../Data/");
        public bool Fetch(Symbol symbol, DateTime date, Resolution resolution, TickType tickType)
        {
            Log.Trace(
                string.Format(
                    "Attempting to get data from QuantConnect.com's data library for symbol({0}), resolution({1}) and date({2}).",
                    symbol.ID, resolution, date.Date.ToShortDateString()));

            var api = new Api.Api();
            api.Initialize(_uid, _token, _dataPath);

            var download = api.DownloadData(symbol, resolution, date);

            if (download)
            {
                Log.Trace(
                    string.Format(
                        "Successfully retrieved data for symbol({0}), resolution({1}) and date({2}).",
                        symbol.ID, resolution, date.Date.ToShortDateString()));
                return true;
            }


            Log.Error(
                    string.Format(
                        "Unable to remotely retrieve data for symbol({0}), resolution({1}) and date({2}). Please make sure you have the necessary data in your online QuantConnect data library.",
                        symbol.ID, resolution, date.Date.ToShortDateString()));
            return false;
        }
    }
}

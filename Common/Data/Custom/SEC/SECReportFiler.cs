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

using Newtonsoft.Json;
using System.Collections.Generic;

namespace QuantConnect.Data.Custom.SEC
{
    public class SECReportFiler
    {
        /// <summary>
        /// SEC data containing company data such as company name, cik, etc.
        /// </summary>
        [JsonProperty("COMPANY-DATA")]
        public SECReportCompanyData CompanyData;

        /// <summary>
        /// Information regarding the filing itself
        /// </summary>
        [JsonProperty("FILING-VALUES")]
        public SECReportFilingValues Values;
        
        /// <summary>
        /// Information related to the business' address
        /// </summary>
        [JsonProperty("BUSINESS-ADDRESS"), JsonConverter(typeof(PossibleListConverter<SECReportBusinessAddress>))]
        public List<SECReportBusinessAddress> BusinessAddress;

        /// <summary>
        /// Company mailing address information
        /// </summary>
        [JsonProperty("MAIL-ADDRESS"), JsonConverter(typeof(PossibleListConverter<SECReportMailAddress>))]
        public List<SECReportMailAddress> MailingAddress;

        /// <summary>
        /// Former company names
        /// </summary>
        [JsonProperty("FORMER-COMPANY"), JsonConverter(typeof(PossibleListConverter<SECReportFormerCompany>))]
        public List<SECReportFormerCompany> FormerCompanies;
    }
}
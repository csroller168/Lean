/* 
 * OANDA v20 REST API
 *
 * The full OANDA v20 REST API Specification. This specification defines how to interact with v20 Accounts, Trades, Orders, Pricing and more.
 *
 * OpenAPI spec version: 3.0.14
 * Contact: api@oanda.com
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;

namespace Oanda.RestV20.Model
{
    /// <summary>
    /// The reason that the Limit Order was initiated
    /// </summary>
    /// <value>The reason that the Limit Order was initiated</value>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LimitOrderReason
    {
        
        /// <summary>
        /// Enum CLIENTORDER for "CLIENT_ORDER"
        /// </summary>
        [EnumMember(Value = "CLIENT_ORDER")]
        CLIENTORDER,
        
        /// <summary>
        /// Enum REPLACEMENT for "REPLACEMENT"
        /// </summary>
        [EnumMember(Value = "REPLACEMENT")]
        REPLACEMENT
    }

}

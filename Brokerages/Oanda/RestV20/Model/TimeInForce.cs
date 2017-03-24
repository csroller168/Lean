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
    /// The time-in-force of an Order. TimeInForce describes how long an Order should remain pending before being automatically cancelled by the execution system.
    /// </summary>
    /// <value>The time-in-force of an Order. TimeInForce describes how long an Order should remain pending before being automatically cancelled by the execution system.</value>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TimeInForce
    {
        
        /// <summary>
        /// Enum GTC for "GTC"
        /// </summary>
        [EnumMember(Value = "GTC")]
        GTC,
        
        /// <summary>
        /// Enum GTD for "GTD"
        /// </summary>
        [EnumMember(Value = "GTD")]
        GTD,
        
        /// <summary>
        /// Enum GFD for "GFD"
        /// </summary>
        [EnumMember(Value = "GFD")]
        GFD,
        
        /// <summary>
        /// Enum FOK for "FOK"
        /// </summary>
        [EnumMember(Value = "FOK")]
        FOK,
        
        /// <summary>
        /// Enum IOC for "IOC"
        /// </summary>
        [EnumMember(Value = "IOC")]
        IOC
    }

}

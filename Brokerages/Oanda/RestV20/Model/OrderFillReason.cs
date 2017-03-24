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
    /// The reason that an Order was filled
    /// </summary>
    /// <value>The reason that an Order was filled</value>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OrderFillReason
    {
        
        /// <summary>
        /// Enum LIMITORDER for "LIMIT_ORDER"
        /// </summary>
        [EnumMember(Value = "LIMIT_ORDER")]
        LIMITORDER,
        
        /// <summary>
        /// Enum STOPORDER for "STOP_ORDER"
        /// </summary>
        [EnumMember(Value = "STOP_ORDER")]
        STOPORDER,
        
        /// <summary>
        /// Enum MARKETIFTOUCHEDORDER for "MARKET_IF_TOUCHED_ORDER"
        /// </summary>
        [EnumMember(Value = "MARKET_IF_TOUCHED_ORDER")]
        MARKETIFTOUCHEDORDER,
        
        /// <summary>
        /// Enum TAKEPROFITORDER for "TAKE_PROFIT_ORDER"
        /// </summary>
        [EnumMember(Value = "TAKE_PROFIT_ORDER")]
        TAKEPROFITORDER,
        
        /// <summary>
        /// Enum STOPLOSSORDER for "STOP_LOSS_ORDER"
        /// </summary>
        [EnumMember(Value = "STOP_LOSS_ORDER")]
        STOPLOSSORDER,
        
        /// <summary>
        /// Enum TRAILINGSTOPLOSSORDER for "TRAILING_STOP_LOSS_ORDER"
        /// </summary>
        [EnumMember(Value = "TRAILING_STOP_LOSS_ORDER")]
        TRAILINGSTOPLOSSORDER,
        
        /// <summary>
        /// Enum MARKETORDER for "MARKET_ORDER"
        /// </summary>
        [EnumMember(Value = "MARKET_ORDER")]
        MARKETORDER,
        
        /// <summary>
        /// Enum MARKETORDERTRADECLOSE for "MARKET_ORDER_TRADE_CLOSE"
        /// </summary>
        [EnumMember(Value = "MARKET_ORDER_TRADE_CLOSE")]
        MARKETORDERTRADECLOSE,
        
        /// <summary>
        /// Enum MARKETORDERPOSITIONCLOSEOUT for "MARKET_ORDER_POSITION_CLOSEOUT"
        /// </summary>
        [EnumMember(Value = "MARKET_ORDER_POSITION_CLOSEOUT")]
        MARKETORDERPOSITIONCLOSEOUT,
        
        /// <summary>
        /// Enum MARKETORDERMARGINCLOSEOUT for "MARKET_ORDER_MARGIN_CLOSEOUT"
        /// </summary>
        [EnumMember(Value = "MARKET_ORDER_MARGIN_CLOSEOUT")]
        MARKETORDERMARGINCLOSEOUT,
        
        /// <summary>
        /// Enum MARKETORDERDELAYEDTRADECLOSE for "MARKET_ORDER_DELAYED_TRADE_CLOSE"
        /// </summary>
        [EnumMember(Value = "MARKET_ORDER_DELAYED_TRADE_CLOSE")]
        MARKETORDERDELAYEDTRADECLOSE
    }

}

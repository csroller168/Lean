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
    /// A TakeProfitOrderRequest specifies the parameters that may be set when creating a Take Profit Order.
    /// </summary>
    [DataContract]
    public partial class TakeProfitOrderRequest :  IEquatable<TakeProfitOrderRequest>, IValidatableObject
    {
        /// <summary>
        /// The type of the Order to Create. Must be set to \"TAKE_PROFIT\" when creating a Take Profit Order.
        /// </summary>
        /// <value>The type of the Order to Create. Must be set to \"TAKE_PROFIT\" when creating a Take Profit Order.</value>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum TypeEnum
        {
            
            /// <summary>
            /// Enum MARKET for "MARKET"
            /// </summary>
            [EnumMember(Value = "MARKET")]
            MARKET,
            
            /// <summary>
            /// Enum LIMIT for "LIMIT"
            /// </summary>
            [EnumMember(Value = "LIMIT")]
            LIMIT,
            
            /// <summary>
            /// Enum STOP for "STOP"
            /// </summary>
            [EnumMember(Value = "STOP")]
            STOP,
            
            /// <summary>
            /// Enum MARKETIFTOUCHED for "MARKET_IF_TOUCHED"
            /// </summary>
            [EnumMember(Value = "MARKET_IF_TOUCHED")]
            MARKETIFTOUCHED,
            
            /// <summary>
            /// Enum TAKEPROFIT for "TAKE_PROFIT"
            /// </summary>
            [EnumMember(Value = "TAKE_PROFIT")]
            TAKEPROFIT,
            
            /// <summary>
            /// Enum STOPLOSS for "STOP_LOSS"
            /// </summary>
            [EnumMember(Value = "STOP_LOSS")]
            STOPLOSS,
            
            /// <summary>
            /// Enum TRAILINGSTOPLOSS for "TRAILING_STOP_LOSS"
            /// </summary>
            [EnumMember(Value = "TRAILING_STOP_LOSS")]
            TRAILINGSTOPLOSS
        }

        /// <summary>
        /// The time-in-force requested for the TakeProfit Order. Restricted to \"GTC\", \"GFD\" and \"GTD\" for TakeProfit Orders.
        /// </summary>
        /// <value>The time-in-force requested for the TakeProfit Order. Restricted to \"GTC\", \"GFD\" and \"GTD\" for TakeProfit Orders.</value>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum TimeInForceEnum
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

        /// <summary>
        /// The type of the Order to Create. Must be set to \"TAKE_PROFIT\" when creating a Take Profit Order.
        /// </summary>
        /// <value>The type of the Order to Create. Must be set to \"TAKE_PROFIT\" when creating a Take Profit Order.</value>
        [DataMember(Name="type", EmitDefaultValue=false)]
        public TypeEnum? Type { get; set; }
        /// <summary>
        /// The time-in-force requested for the TakeProfit Order. Restricted to \"GTC\", \"GFD\" and \"GTD\" for TakeProfit Orders.
        /// </summary>
        /// <value>The time-in-force requested for the TakeProfit Order. Restricted to \"GTC\", \"GFD\" and \"GTD\" for TakeProfit Orders.</value>
        [DataMember(Name="timeInForce", EmitDefaultValue=false)]
        public TimeInForceEnum? TimeInForce { get; set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="TakeProfitOrderRequest" /> class.
        /// </summary>
        /// <param name="Type">The type of the Order to Create. Must be set to \&quot;TAKE_PROFIT\&quot; when creating a Take Profit Order..</param>
        /// <param name="TradeID">The ID of the Trade to close when the price threshold is breached..</param>
        /// <param name="ClientTradeID">The client ID of the Trade to be closed when the price threshold is breached..</param>
        /// <param name="Price">The price threshold specified for the TakeProfit Order. The associated Trade will be closed by a market price that is equal to or better than this threshold..</param>
        /// <param name="TimeInForce">The time-in-force requested for the TakeProfit Order. Restricted to \&quot;GTC\&quot;, \&quot;GFD\&quot; and \&quot;GTD\&quot; for TakeProfit Orders..</param>
        /// <param name="GtdTime">The date/time when the TakeProfit Order will be cancelled if its timeInForce is \&quot;GTD\&quot;..</param>
        /// <param name="ClientExtensions">The client extensions to add to the Order. Do not set, modify, or delete clientExtensions if your account is associated with MT4..</param>
        public TakeProfitOrderRequest(TypeEnum? Type = default(TypeEnum?), string TradeID = default(string), string ClientTradeID = default(string), string Price = default(string), TimeInForceEnum? TimeInForce = default(TimeInForceEnum?), string GtdTime = default(string), ClientExtensions ClientExtensions = default(ClientExtensions))
        {
            this.Type = Type;
            this.TradeID = TradeID;
            this.ClientTradeID = ClientTradeID;
            this.Price = Price;
            this.TimeInForce = TimeInForce;
            this.GtdTime = GtdTime;
            this.ClientExtensions = ClientExtensions;
        }
        
        /// <summary>
        /// The ID of the Trade to close when the price threshold is breached.
        /// </summary>
        /// <value>The ID of the Trade to close when the price threshold is breached.</value>
        [DataMember(Name="tradeID", EmitDefaultValue=false)]
        public string TradeID { get; set; }
        /// <summary>
        /// The client ID of the Trade to be closed when the price threshold is breached.
        /// </summary>
        /// <value>The client ID of the Trade to be closed when the price threshold is breached.</value>
        [DataMember(Name="clientTradeID", EmitDefaultValue=false)]
        public string ClientTradeID { get; set; }
        /// <summary>
        /// The price threshold specified for the TakeProfit Order. The associated Trade will be closed by a market price that is equal to or better than this threshold.
        /// </summary>
        /// <value>The price threshold specified for the TakeProfit Order. The associated Trade will be closed by a market price that is equal to or better than this threshold.</value>
        [DataMember(Name="price", EmitDefaultValue=false)]
        public string Price { get; set; }
        /// <summary>
        /// The date/time when the TakeProfit Order will be cancelled if its timeInForce is \&quot;GTD\&quot;.
        /// </summary>
        /// <value>The date/time when the TakeProfit Order will be cancelled if its timeInForce is \&quot;GTD\&quot;.</value>
        [DataMember(Name="gtdTime", EmitDefaultValue=false)]
        public string GtdTime { get; set; }
        /// <summary>
        /// The client extensions to add to the Order. Do not set, modify, or delete clientExtensions if your account is associated with MT4.
        /// </summary>
        /// <value>The client extensions to add to the Order. Do not set, modify, or delete clientExtensions if your account is associated with MT4.</value>
        [DataMember(Name="clientExtensions", EmitDefaultValue=false)]
        public ClientExtensions ClientExtensions { get; set; }
        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class TakeProfitOrderRequest {\n");
            sb.Append("  Type: ").Append(Type).Append("\n");
            sb.Append("  TradeID: ").Append(TradeID).Append("\n");
            sb.Append("  ClientTradeID: ").Append(ClientTradeID).Append("\n");
            sb.Append("  Price: ").Append(Price).Append("\n");
            sb.Append("  TimeInForce: ").Append(TimeInForce).Append("\n");
            sb.Append("  GtdTime: ").Append(GtdTime).Append("\n");
            sb.Append("  ClientExtensions: ").Append(ClientExtensions).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }
  
        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="obj">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object obj)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            return this.Equals(obj as TakeProfitOrderRequest);
        }

        /// <summary>
        /// Returns true if TakeProfitOrderRequest instances are equal
        /// </summary>
        /// <param name="other">Instance of TakeProfitOrderRequest to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(TakeProfitOrderRequest other)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            if (other == null)
                return false;

            return 
                (
                    this.Type == other.Type ||
                    this.Type != null &&
                    this.Type.Equals(other.Type)
                ) && 
                (
                    this.TradeID == other.TradeID ||
                    this.TradeID != null &&
                    this.TradeID.Equals(other.TradeID)
                ) && 
                (
                    this.ClientTradeID == other.ClientTradeID ||
                    this.ClientTradeID != null &&
                    this.ClientTradeID.Equals(other.ClientTradeID)
                ) && 
                (
                    this.Price == other.Price ||
                    this.Price != null &&
                    this.Price.Equals(other.Price)
                ) && 
                (
                    this.TimeInForce == other.TimeInForce ||
                    this.TimeInForce != null &&
                    this.TimeInForce.Equals(other.TimeInForce)
                ) && 
                (
                    this.GtdTime == other.GtdTime ||
                    this.GtdTime != null &&
                    this.GtdTime.Equals(other.GtdTime)
                ) && 
                (
                    this.ClientExtensions == other.ClientExtensions ||
                    this.ClientExtensions != null &&
                    this.ClientExtensions.Equals(other.ClientExtensions)
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            // credit: http://stackoverflow.com/a/263416/677735
            unchecked // Overflow is fine, just wrap
            {
                int hash = 41;
                // Suitable nullity checks etc, of course :)
                if (this.Type != null)
                    hash = hash * 59 + this.Type.GetHashCode();
                if (this.TradeID != null)
                    hash = hash * 59 + this.TradeID.GetHashCode();
                if (this.ClientTradeID != null)
                    hash = hash * 59 + this.ClientTradeID.GetHashCode();
                if (this.Price != null)
                    hash = hash * 59 + this.Price.GetHashCode();
                if (this.TimeInForce != null)
                    hash = hash * 59 + this.TimeInForce.GetHashCode();
                if (this.GtdTime != null)
                    hash = hash * 59 + this.GtdTime.GetHashCode();
                if (this.ClientExtensions != null)
                    hash = hash * 59 + this.ClientExtensions.GetHashCode();
                return hash;
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        { 
            yield break;
        }
    }

}

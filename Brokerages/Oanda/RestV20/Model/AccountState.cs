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
    /// An AccountState Object is used to represent an Account&#39;s current price-dependent state. Price-dependent Account state is dependent on OANDA&#39;s current Prices, and includes things like unrealized PL, NAV and Trailing Stop Loss Order state.
    /// </summary>
    [DataContract]
    public partial class AccountState :  IEquatable<AccountState>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccountState" /> class.
        /// </summary>
        /// <param name="UnrealizedPL">The total unrealized profit/loss for all Trades currently open in the Account. Represented in the Account&#39;s home currency..</param>
        /// <param name="NAV">The net asset value of the Account. Equal to Account balance + unrealizedPL. Represented in the Account&#39;s home currency..</param>
        /// <param name="MarginUsed">Margin currently used for the Account. Represented in the Account&#39;s home currency..</param>
        /// <param name="MarginAvailable">Margin available for Account. Represented in the Account&#39;s home currency..</param>
        /// <param name="PositionValue">The value of the Account&#39;s open positions represented in the Account&#39;s home currency..</param>
        /// <param name="MarginCloseoutUnrealizedPL">The Account&#39;s margin closeout unrealized PL..</param>
        /// <param name="MarginCloseoutNAV">The Account&#39;s margin closeout NAV..</param>
        /// <param name="MarginCloseoutMarginUsed">The Account&#39;s margin closeout margin used..</param>
        /// <param name="MarginCloseoutPercent">The Account&#39;s margin closeout percentage. When this value is 1.0 or above the Account is in a margin closeout situation..</param>
        /// <param name="WithdrawalLimit">The current WithdrawalLimit for the account which will be zero or a positive value indicating how much can be withdrawn from the account..</param>
        /// <param name="MarginCallMarginUsed">The Account&#39;s margin call margin used..</param>
        /// <param name="MarginCallPercent">The Account&#39;s margin call percentage. When this value is 1.0 or above the Account is in a margin call situation..</param>
        /// <param name="Orders">The price-dependent state of each pending Order in the Account..</param>
        /// <param name="Trades">The price-dependent state for each open Trade in the Account..</param>
        /// <param name="Positions">The price-dependent state for each open Position in the Account..</param>
        public AccountState(string UnrealizedPL = default(string), string NAV = default(string), string MarginUsed = default(string), string MarginAvailable = default(string), string PositionValue = default(string), string MarginCloseoutUnrealizedPL = default(string), string MarginCloseoutNAV = default(string), string MarginCloseoutMarginUsed = default(string), string MarginCloseoutPercent = default(string), string WithdrawalLimit = default(string), string MarginCallMarginUsed = default(string), string MarginCallPercent = default(string), List<DynamicOrderState> Orders = default(List<DynamicOrderState>), List<CalculatedTradeState> Trades = default(List<CalculatedTradeState>), List<CalculatedPositionState> Positions = default(List<CalculatedPositionState>))
        {
            this.UnrealizedPL = UnrealizedPL;
            this.NAV = NAV;
            this.MarginUsed = MarginUsed;
            this.MarginAvailable = MarginAvailable;
            this.PositionValue = PositionValue;
            this.MarginCloseoutUnrealizedPL = MarginCloseoutUnrealizedPL;
            this.MarginCloseoutNAV = MarginCloseoutNAV;
            this.MarginCloseoutMarginUsed = MarginCloseoutMarginUsed;
            this.MarginCloseoutPercent = MarginCloseoutPercent;
            this.WithdrawalLimit = WithdrawalLimit;
            this.MarginCallMarginUsed = MarginCallMarginUsed;
            this.MarginCallPercent = MarginCallPercent;
            this.Orders = Orders;
            this.Trades = Trades;
            this.Positions = Positions;
        }
        
        /// <summary>
        /// The total unrealized profit/loss for all Trades currently open in the Account. Represented in the Account&#39;s home currency.
        /// </summary>
        /// <value>The total unrealized profit/loss for all Trades currently open in the Account. Represented in the Account&#39;s home currency.</value>
        [DataMember(Name="unrealizedPL", EmitDefaultValue=false)]
        public string UnrealizedPL { get; set; }
        /// <summary>
        /// The net asset value of the Account. Equal to Account balance + unrealizedPL. Represented in the Account&#39;s home currency.
        /// </summary>
        /// <value>The net asset value of the Account. Equal to Account balance + unrealizedPL. Represented in the Account&#39;s home currency.</value>
        [DataMember(Name="NAV", EmitDefaultValue=false)]
        public string NAV { get; set; }
        /// <summary>
        /// Margin currently used for the Account. Represented in the Account&#39;s home currency.
        /// </summary>
        /// <value>Margin currently used for the Account. Represented in the Account&#39;s home currency.</value>
        [DataMember(Name="marginUsed", EmitDefaultValue=false)]
        public string MarginUsed { get; set; }
        /// <summary>
        /// Margin available for Account. Represented in the Account&#39;s home currency.
        /// </summary>
        /// <value>Margin available for Account. Represented in the Account&#39;s home currency.</value>
        [DataMember(Name="marginAvailable", EmitDefaultValue=false)]
        public string MarginAvailable { get; set; }
        /// <summary>
        /// The value of the Account&#39;s open positions represented in the Account&#39;s home currency.
        /// </summary>
        /// <value>The value of the Account&#39;s open positions represented in the Account&#39;s home currency.</value>
        [DataMember(Name="positionValue", EmitDefaultValue=false)]
        public string PositionValue { get; set; }
        /// <summary>
        /// The Account&#39;s margin closeout unrealized PL.
        /// </summary>
        /// <value>The Account&#39;s margin closeout unrealized PL.</value>
        [DataMember(Name="marginCloseoutUnrealizedPL", EmitDefaultValue=false)]
        public string MarginCloseoutUnrealizedPL { get; set; }
        /// <summary>
        /// The Account&#39;s margin closeout NAV.
        /// </summary>
        /// <value>The Account&#39;s margin closeout NAV.</value>
        [DataMember(Name="marginCloseoutNAV", EmitDefaultValue=false)]
        public string MarginCloseoutNAV { get; set; }
        /// <summary>
        /// The Account&#39;s margin closeout margin used.
        /// </summary>
        /// <value>The Account&#39;s margin closeout margin used.</value>
        [DataMember(Name="marginCloseoutMarginUsed", EmitDefaultValue=false)]
        public string MarginCloseoutMarginUsed { get; set; }
        /// <summary>
        /// The Account&#39;s margin closeout percentage. When this value is 1.0 or above the Account is in a margin closeout situation.
        /// </summary>
        /// <value>The Account&#39;s margin closeout percentage. When this value is 1.0 or above the Account is in a margin closeout situation.</value>
        [DataMember(Name="marginCloseoutPercent", EmitDefaultValue=false)]
        public string MarginCloseoutPercent { get; set; }
        /// <summary>
        /// The current WithdrawalLimit for the account which will be zero or a positive value indicating how much can be withdrawn from the account.
        /// </summary>
        /// <value>The current WithdrawalLimit for the account which will be zero or a positive value indicating how much can be withdrawn from the account.</value>
        [DataMember(Name="withdrawalLimit", EmitDefaultValue=false)]
        public string WithdrawalLimit { get; set; }
        /// <summary>
        /// The Account&#39;s margin call margin used.
        /// </summary>
        /// <value>The Account&#39;s margin call margin used.</value>
        [DataMember(Name="marginCallMarginUsed", EmitDefaultValue=false)]
        public string MarginCallMarginUsed { get; set; }
        /// <summary>
        /// The Account&#39;s margin call percentage. When this value is 1.0 or above the Account is in a margin call situation.
        /// </summary>
        /// <value>The Account&#39;s margin call percentage. When this value is 1.0 or above the Account is in a margin call situation.</value>
        [DataMember(Name="marginCallPercent", EmitDefaultValue=false)]
        public string MarginCallPercent { get; set; }
        /// <summary>
        /// The price-dependent state of each pending Order in the Account.
        /// </summary>
        /// <value>The price-dependent state of each pending Order in the Account.</value>
        [DataMember(Name="orders", EmitDefaultValue=false)]
        public List<DynamicOrderState> Orders { get; set; }
        /// <summary>
        /// The price-dependent state for each open Trade in the Account.
        /// </summary>
        /// <value>The price-dependent state for each open Trade in the Account.</value>
        [DataMember(Name="trades", EmitDefaultValue=false)]
        public List<CalculatedTradeState> Trades { get; set; }
        /// <summary>
        /// The price-dependent state for each open Position in the Account.
        /// </summary>
        /// <value>The price-dependent state for each open Position in the Account.</value>
        [DataMember(Name="positions", EmitDefaultValue=false)]
        public List<CalculatedPositionState> Positions { get; set; }
        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class AccountState {\n");
            sb.Append("  UnrealizedPL: ").Append(UnrealizedPL).Append("\n");
            sb.Append("  NAV: ").Append(NAV).Append("\n");
            sb.Append("  MarginUsed: ").Append(MarginUsed).Append("\n");
            sb.Append("  MarginAvailable: ").Append(MarginAvailable).Append("\n");
            sb.Append("  PositionValue: ").Append(PositionValue).Append("\n");
            sb.Append("  MarginCloseoutUnrealizedPL: ").Append(MarginCloseoutUnrealizedPL).Append("\n");
            sb.Append("  MarginCloseoutNAV: ").Append(MarginCloseoutNAV).Append("\n");
            sb.Append("  MarginCloseoutMarginUsed: ").Append(MarginCloseoutMarginUsed).Append("\n");
            sb.Append("  MarginCloseoutPercent: ").Append(MarginCloseoutPercent).Append("\n");
            sb.Append("  WithdrawalLimit: ").Append(WithdrawalLimit).Append("\n");
            sb.Append("  MarginCallMarginUsed: ").Append(MarginCallMarginUsed).Append("\n");
            sb.Append("  MarginCallPercent: ").Append(MarginCallPercent).Append("\n");
            sb.Append("  Orders: ").Append(Orders).Append("\n");
            sb.Append("  Trades: ").Append(Trades).Append("\n");
            sb.Append("  Positions: ").Append(Positions).Append("\n");
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
            return this.Equals(obj as AccountState);
        }

        /// <summary>
        /// Returns true if AccountState instances are equal
        /// </summary>
        /// <param name="other">Instance of AccountState to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(AccountState other)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            if (other == null)
                return false;

            return 
                (
                    this.UnrealizedPL == other.UnrealizedPL ||
                    this.UnrealizedPL != null &&
                    this.UnrealizedPL.Equals(other.UnrealizedPL)
                ) && 
                (
                    this.NAV == other.NAV ||
                    this.NAV != null &&
                    this.NAV.Equals(other.NAV)
                ) && 
                (
                    this.MarginUsed == other.MarginUsed ||
                    this.MarginUsed != null &&
                    this.MarginUsed.Equals(other.MarginUsed)
                ) && 
                (
                    this.MarginAvailable == other.MarginAvailable ||
                    this.MarginAvailable != null &&
                    this.MarginAvailable.Equals(other.MarginAvailable)
                ) && 
                (
                    this.PositionValue == other.PositionValue ||
                    this.PositionValue != null &&
                    this.PositionValue.Equals(other.PositionValue)
                ) && 
                (
                    this.MarginCloseoutUnrealizedPL == other.MarginCloseoutUnrealizedPL ||
                    this.MarginCloseoutUnrealizedPL != null &&
                    this.MarginCloseoutUnrealizedPL.Equals(other.MarginCloseoutUnrealizedPL)
                ) && 
                (
                    this.MarginCloseoutNAV == other.MarginCloseoutNAV ||
                    this.MarginCloseoutNAV != null &&
                    this.MarginCloseoutNAV.Equals(other.MarginCloseoutNAV)
                ) && 
                (
                    this.MarginCloseoutMarginUsed == other.MarginCloseoutMarginUsed ||
                    this.MarginCloseoutMarginUsed != null &&
                    this.MarginCloseoutMarginUsed.Equals(other.MarginCloseoutMarginUsed)
                ) && 
                (
                    this.MarginCloseoutPercent == other.MarginCloseoutPercent ||
                    this.MarginCloseoutPercent != null &&
                    this.MarginCloseoutPercent.Equals(other.MarginCloseoutPercent)
                ) && 
                (
                    this.WithdrawalLimit == other.WithdrawalLimit ||
                    this.WithdrawalLimit != null &&
                    this.WithdrawalLimit.Equals(other.WithdrawalLimit)
                ) && 
                (
                    this.MarginCallMarginUsed == other.MarginCallMarginUsed ||
                    this.MarginCallMarginUsed != null &&
                    this.MarginCallMarginUsed.Equals(other.MarginCallMarginUsed)
                ) && 
                (
                    this.MarginCallPercent == other.MarginCallPercent ||
                    this.MarginCallPercent != null &&
                    this.MarginCallPercent.Equals(other.MarginCallPercent)
                ) && 
                (
                    this.Orders == other.Orders ||
                    this.Orders != null &&
                    this.Orders.SequenceEqual(other.Orders)
                ) && 
                (
                    this.Trades == other.Trades ||
                    this.Trades != null &&
                    this.Trades.SequenceEqual(other.Trades)
                ) && 
                (
                    this.Positions == other.Positions ||
                    this.Positions != null &&
                    this.Positions.SequenceEqual(other.Positions)
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
                if (this.UnrealizedPL != null)
                    hash = hash * 59 + this.UnrealizedPL.GetHashCode();
                if (this.NAV != null)
                    hash = hash * 59 + this.NAV.GetHashCode();
                if (this.MarginUsed != null)
                    hash = hash * 59 + this.MarginUsed.GetHashCode();
                if (this.MarginAvailable != null)
                    hash = hash * 59 + this.MarginAvailable.GetHashCode();
                if (this.PositionValue != null)
                    hash = hash * 59 + this.PositionValue.GetHashCode();
                if (this.MarginCloseoutUnrealizedPL != null)
                    hash = hash * 59 + this.MarginCloseoutUnrealizedPL.GetHashCode();
                if (this.MarginCloseoutNAV != null)
                    hash = hash * 59 + this.MarginCloseoutNAV.GetHashCode();
                if (this.MarginCloseoutMarginUsed != null)
                    hash = hash * 59 + this.MarginCloseoutMarginUsed.GetHashCode();
                if (this.MarginCloseoutPercent != null)
                    hash = hash * 59 + this.MarginCloseoutPercent.GetHashCode();
                if (this.WithdrawalLimit != null)
                    hash = hash * 59 + this.WithdrawalLimit.GetHashCode();
                if (this.MarginCallMarginUsed != null)
                    hash = hash * 59 + this.MarginCallMarginUsed.GetHashCode();
                if (this.MarginCallPercent != null)
                    hash = hash * 59 + this.MarginCallPercent.GetHashCode();
                if (this.Orders != null)
                    hash = hash * 59 + this.Orders.GetHashCode();
                if (this.Trades != null)
                    hash = hash * 59 + this.Trades.GetHashCode();
                if (this.Positions != null)
                    hash = hash * 59 + this.Positions.GetHashCode();
                return hash;
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        { 
            yield break;
        }
    }

}

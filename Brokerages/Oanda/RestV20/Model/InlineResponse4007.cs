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
    /// InlineResponse4007
    /// </summary>
    [DataContract]
    public partial class InlineResponse4007 :  IEquatable<InlineResponse4007>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InlineResponse4007" /> class.
        /// </summary>
        /// <param name="TakeProfitOrderCancelRejectTransaction">An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account..</param>
        /// <param name="TakeProfitOrderRejectTransaction">A TakeProfitOrderRejectTransaction represents the rejection of the creation of a TakeProfit Order..</param>
        /// <param name="StopLossOrderCancelRejectTransaction">An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account..</param>
        /// <param name="StopLossOrderRejectTransaction">A StopLossOrderRejectTransaction represents the rejection of the creation of a StopLoss Order..</param>
        /// <param name="TrailingStopLossOrderCancelRejectTransaction">An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account..</param>
        /// <param name="TrailingStopLossOrderRejectTransaction">A TrailingStopLossOrderRejectTransaction represents the rejection of the creation of a TrailingStopLoss Order..</param>
        /// <param name="LastTransactionID">The ID of the most recent Transaction created for the Account..</param>
        /// <param name="ErrorCode">The code of the error that has occurred. This field may not be returned for some errors..</param>
        /// <param name="ErrorMessage">The human-readable description of the error that has occurred..</param>
        public InlineResponse4007(OrderCancelRejectTransaction TakeProfitOrderCancelRejectTransaction = default(OrderCancelRejectTransaction), TakeProfitOrderRejectTransaction TakeProfitOrderRejectTransaction = default(TakeProfitOrderRejectTransaction), OrderCancelRejectTransaction StopLossOrderCancelRejectTransaction = default(OrderCancelRejectTransaction), StopLossOrderRejectTransaction StopLossOrderRejectTransaction = default(StopLossOrderRejectTransaction), OrderCancelRejectTransaction TrailingStopLossOrderCancelRejectTransaction = default(OrderCancelRejectTransaction), TrailingStopLossOrderRejectTransaction TrailingStopLossOrderRejectTransaction = default(TrailingStopLossOrderRejectTransaction), string LastTransactionID = default(string), string ErrorCode = default(string), string ErrorMessage = default(string))
        {
            this.TakeProfitOrderCancelRejectTransaction = TakeProfitOrderCancelRejectTransaction;
            this.TakeProfitOrderRejectTransaction = TakeProfitOrderRejectTransaction;
            this.StopLossOrderCancelRejectTransaction = StopLossOrderCancelRejectTransaction;
            this.StopLossOrderRejectTransaction = StopLossOrderRejectTransaction;
            this.TrailingStopLossOrderCancelRejectTransaction = TrailingStopLossOrderCancelRejectTransaction;
            this.TrailingStopLossOrderRejectTransaction = TrailingStopLossOrderRejectTransaction;
            this.LastTransactionID = LastTransactionID;
            this.ErrorCode = ErrorCode;
            this.ErrorMessage = ErrorMessage;
        }
        
        /// <summary>
        /// An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account.
        /// </summary>
        /// <value>An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account.</value>
        [DataMember(Name="takeProfitOrderCancelRejectTransaction", EmitDefaultValue=false)]
        public OrderCancelRejectTransaction TakeProfitOrderCancelRejectTransaction { get; set; }
        /// <summary>
        /// A TakeProfitOrderRejectTransaction represents the rejection of the creation of a TakeProfit Order.
        /// </summary>
        /// <value>A TakeProfitOrderRejectTransaction represents the rejection of the creation of a TakeProfit Order.</value>
        [DataMember(Name="takeProfitOrderRejectTransaction", EmitDefaultValue=false)]
        public TakeProfitOrderRejectTransaction TakeProfitOrderRejectTransaction { get; set; }
        /// <summary>
        /// An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account.
        /// </summary>
        /// <value>An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account.</value>
        [DataMember(Name="stopLossOrderCancelRejectTransaction", EmitDefaultValue=false)]
        public OrderCancelRejectTransaction StopLossOrderCancelRejectTransaction { get; set; }
        /// <summary>
        /// A StopLossOrderRejectTransaction represents the rejection of the creation of a StopLoss Order.
        /// </summary>
        /// <value>A StopLossOrderRejectTransaction represents the rejection of the creation of a StopLoss Order.</value>
        [DataMember(Name="stopLossOrderRejectTransaction", EmitDefaultValue=false)]
        public StopLossOrderRejectTransaction StopLossOrderRejectTransaction { get; set; }
        /// <summary>
        /// An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account.
        /// </summary>
        /// <value>An OrderCancelRejectTransaction represents the rejection of the cancellation of an Order in the client&#39;s Account.</value>
        [DataMember(Name="trailingStopLossOrderCancelRejectTransaction", EmitDefaultValue=false)]
        public OrderCancelRejectTransaction TrailingStopLossOrderCancelRejectTransaction { get; set; }
        /// <summary>
        /// A TrailingStopLossOrderRejectTransaction represents the rejection of the creation of a TrailingStopLoss Order.
        /// </summary>
        /// <value>A TrailingStopLossOrderRejectTransaction represents the rejection of the creation of a TrailingStopLoss Order.</value>
        [DataMember(Name="trailingStopLossOrderRejectTransaction", EmitDefaultValue=false)]
        public TrailingStopLossOrderRejectTransaction TrailingStopLossOrderRejectTransaction { get; set; }
        /// <summary>
        /// The ID of the most recent Transaction created for the Account.
        /// </summary>
        /// <value>The ID of the most recent Transaction created for the Account.</value>
        [DataMember(Name="lastTransactionID", EmitDefaultValue=false)]
        public string LastTransactionID { get; set; }
        /// <summary>
        /// The code of the error that has occurred. This field may not be returned for some errors.
        /// </summary>
        /// <value>The code of the error that has occurred. This field may not be returned for some errors.</value>
        [DataMember(Name="errorCode", EmitDefaultValue=false)]
        public string ErrorCode { get; set; }
        /// <summary>
        /// The human-readable description of the error that has occurred.
        /// </summary>
        /// <value>The human-readable description of the error that has occurred.</value>
        [DataMember(Name="errorMessage", EmitDefaultValue=false)]
        public string ErrorMessage { get; set; }
        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class InlineResponse4007 {\n");
            sb.Append("  TakeProfitOrderCancelRejectTransaction: ").Append(TakeProfitOrderCancelRejectTransaction).Append("\n");
            sb.Append("  TakeProfitOrderRejectTransaction: ").Append(TakeProfitOrderRejectTransaction).Append("\n");
            sb.Append("  StopLossOrderCancelRejectTransaction: ").Append(StopLossOrderCancelRejectTransaction).Append("\n");
            sb.Append("  StopLossOrderRejectTransaction: ").Append(StopLossOrderRejectTransaction).Append("\n");
            sb.Append("  TrailingStopLossOrderCancelRejectTransaction: ").Append(TrailingStopLossOrderCancelRejectTransaction).Append("\n");
            sb.Append("  TrailingStopLossOrderRejectTransaction: ").Append(TrailingStopLossOrderRejectTransaction).Append("\n");
            sb.Append("  LastTransactionID: ").Append(LastTransactionID).Append("\n");
            sb.Append("  ErrorCode: ").Append(ErrorCode).Append("\n");
            sb.Append("  ErrorMessage: ").Append(ErrorMessage).Append("\n");
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
            return this.Equals(obj as InlineResponse4007);
        }

        /// <summary>
        /// Returns true if InlineResponse4007 instances are equal
        /// </summary>
        /// <param name="other">Instance of InlineResponse4007 to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(InlineResponse4007 other)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            if (other == null)
                return false;

            return 
                (
                    this.TakeProfitOrderCancelRejectTransaction == other.TakeProfitOrderCancelRejectTransaction ||
                    this.TakeProfitOrderCancelRejectTransaction != null &&
                    this.TakeProfitOrderCancelRejectTransaction.Equals(other.TakeProfitOrderCancelRejectTransaction)
                ) && 
                (
                    this.TakeProfitOrderRejectTransaction == other.TakeProfitOrderRejectTransaction ||
                    this.TakeProfitOrderRejectTransaction != null &&
                    this.TakeProfitOrderRejectTransaction.Equals(other.TakeProfitOrderRejectTransaction)
                ) && 
                (
                    this.StopLossOrderCancelRejectTransaction == other.StopLossOrderCancelRejectTransaction ||
                    this.StopLossOrderCancelRejectTransaction != null &&
                    this.StopLossOrderCancelRejectTransaction.Equals(other.StopLossOrderCancelRejectTransaction)
                ) && 
                (
                    this.StopLossOrderRejectTransaction == other.StopLossOrderRejectTransaction ||
                    this.StopLossOrderRejectTransaction != null &&
                    this.StopLossOrderRejectTransaction.Equals(other.StopLossOrderRejectTransaction)
                ) && 
                (
                    this.TrailingStopLossOrderCancelRejectTransaction == other.TrailingStopLossOrderCancelRejectTransaction ||
                    this.TrailingStopLossOrderCancelRejectTransaction != null &&
                    this.TrailingStopLossOrderCancelRejectTransaction.Equals(other.TrailingStopLossOrderCancelRejectTransaction)
                ) && 
                (
                    this.TrailingStopLossOrderRejectTransaction == other.TrailingStopLossOrderRejectTransaction ||
                    this.TrailingStopLossOrderRejectTransaction != null &&
                    this.TrailingStopLossOrderRejectTransaction.Equals(other.TrailingStopLossOrderRejectTransaction)
                ) && 
                (
                    this.LastTransactionID == other.LastTransactionID ||
                    this.LastTransactionID != null &&
                    this.LastTransactionID.Equals(other.LastTransactionID)
                ) && 
                (
                    this.ErrorCode == other.ErrorCode ||
                    this.ErrorCode != null &&
                    this.ErrorCode.Equals(other.ErrorCode)
                ) && 
                (
                    this.ErrorMessage == other.ErrorMessage ||
                    this.ErrorMessage != null &&
                    this.ErrorMessage.Equals(other.ErrorMessage)
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
                if (this.TakeProfitOrderCancelRejectTransaction != null)
                    hash = hash * 59 + this.TakeProfitOrderCancelRejectTransaction.GetHashCode();
                if (this.TakeProfitOrderRejectTransaction != null)
                    hash = hash * 59 + this.TakeProfitOrderRejectTransaction.GetHashCode();
                if (this.StopLossOrderCancelRejectTransaction != null)
                    hash = hash * 59 + this.StopLossOrderCancelRejectTransaction.GetHashCode();
                if (this.StopLossOrderRejectTransaction != null)
                    hash = hash * 59 + this.StopLossOrderRejectTransaction.GetHashCode();
                if (this.TrailingStopLossOrderCancelRejectTransaction != null)
                    hash = hash * 59 + this.TrailingStopLossOrderCancelRejectTransaction.GetHashCode();
                if (this.TrailingStopLossOrderRejectTransaction != null)
                    hash = hash * 59 + this.TrailingStopLossOrderRejectTransaction.GetHashCode();
                if (this.LastTransactionID != null)
                    hash = hash * 59 + this.LastTransactionID.GetHashCode();
                if (this.ErrorCode != null)
                    hash = hash * 59 + this.ErrorCode.GetHashCode();
                if (this.ErrorMessage != null)
                    hash = hash * 59 + this.ErrorMessage.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// To validate all properties of the instance
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation Result</returns>
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        { 
            yield break;
        }
    }

}

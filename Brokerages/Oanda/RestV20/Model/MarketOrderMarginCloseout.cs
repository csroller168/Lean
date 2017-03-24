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
    /// Details for the Market Order extensions specific to a Market Order placed that is part of a Market Order Margin Closeout in a client&#39;s account
    /// </summary>
    [DataContract]
    public partial class MarketOrderMarginCloseout :  IEquatable<MarketOrderMarginCloseout>, IValidatableObject
    {
        /// <summary>
        /// The reason the Market Order was created to perform a margin closeout
        /// </summary>
        /// <value>The reason the Market Order was created to perform a margin closeout</value>
        [JsonConverter(typeof(StringEnumConverter))]
        public enum ReasonEnum
        {
            
            /// <summary>
            /// Enum MARGINCHECKVIOLATION for "MARGIN_CHECK_VIOLATION"
            /// </summary>
            [EnumMember(Value = "MARGIN_CHECK_VIOLATION")]
            MARGINCHECKVIOLATION,
            
            /// <summary>
            /// Enum REGULATORYMARGINCALLVIOLATION for "REGULATORY_MARGIN_CALL_VIOLATION"
            /// </summary>
            [EnumMember(Value = "REGULATORY_MARGIN_CALL_VIOLATION")]
            REGULATORYMARGINCALLVIOLATION
        }

        /// <summary>
        /// The reason the Market Order was created to perform a margin closeout
        /// </summary>
        /// <value>The reason the Market Order was created to perform a margin closeout</value>
        [DataMember(Name="reason", EmitDefaultValue=false)]
        public ReasonEnum? Reason { get; set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="MarketOrderMarginCloseout" /> class.
        /// </summary>
        /// <param name="Reason">The reason the Market Order was created to perform a margin closeout.</param>
        public MarketOrderMarginCloseout(ReasonEnum? Reason = default(ReasonEnum?))
        {
            this.Reason = Reason;
        }
        
        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class MarketOrderMarginCloseout {\n");
            sb.Append("  Reason: ").Append(Reason).Append("\n");
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
            return this.Equals(obj as MarketOrderMarginCloseout);
        }

        /// <summary>
        /// Returns true if MarketOrderMarginCloseout instances are equal
        /// </summary>
        /// <param name="other">Instance of MarketOrderMarginCloseout to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(MarketOrderMarginCloseout other)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            if (other == null)
                return false;

            return 
                (
                    this.Reason == other.Reason ||
                    this.Reason != null &&
                    this.Reason.Equals(other.Reason)
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
                if (this.Reason != null)
                    hash = hash * 59 + this.Reason.GetHashCode();
                return hash;
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        { 
            yield break;
        }
    }

}

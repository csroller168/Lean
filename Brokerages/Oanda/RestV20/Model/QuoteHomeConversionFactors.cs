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
    /// QuoteHomeConversionFactors represents the factors that can be used used to convert quantities of a Price&#39;s Instrument&#39;s quote currency into the Account&#39;s home currency.
    /// </summary>
    [DataContract]
    public partial class QuoteHomeConversionFactors :  IEquatable<QuoteHomeConversionFactors>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuoteHomeConversionFactors" /> class.
        /// </summary>
        /// <param name="PositiveUnits">The factor used to convert a positive amount of the Price&#39;s Instrument&#39;s quote currency into a positive amount of the Account&#39;s home currency.  Conversion is performed by multiplying the quote units by the conversion factor..</param>
        /// <param name="NegativeUnits">The factor used to convert a negative amount of the Price&#39;s Instrument&#39;s quote currency into a negative amount of the Account&#39;s home currency.  Conversion is performed by multiplying the quote units by the conversion factor..</param>
        public QuoteHomeConversionFactors(string PositiveUnits = default(string), string NegativeUnits = default(string))
        {
            this.PositiveUnits = PositiveUnits;
            this.NegativeUnits = NegativeUnits;
        }
        
        /// <summary>
        /// The factor used to convert a positive amount of the Price&#39;s Instrument&#39;s quote currency into a positive amount of the Account&#39;s home currency.  Conversion is performed by multiplying the quote units by the conversion factor.
        /// </summary>
        /// <value>The factor used to convert a positive amount of the Price&#39;s Instrument&#39;s quote currency into a positive amount of the Account&#39;s home currency.  Conversion is performed by multiplying the quote units by the conversion factor.</value>
        [DataMember(Name="positiveUnits", EmitDefaultValue=false)]
        public string PositiveUnits { get; set; }
        /// <summary>
        /// The factor used to convert a negative amount of the Price&#39;s Instrument&#39;s quote currency into a negative amount of the Account&#39;s home currency.  Conversion is performed by multiplying the quote units by the conversion factor.
        /// </summary>
        /// <value>The factor used to convert a negative amount of the Price&#39;s Instrument&#39;s quote currency into a negative amount of the Account&#39;s home currency.  Conversion is performed by multiplying the quote units by the conversion factor.</value>
        [DataMember(Name="negativeUnits", EmitDefaultValue=false)]
        public string NegativeUnits { get; set; }
        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class QuoteHomeConversionFactors {\n");
            sb.Append("  PositiveUnits: ").Append(PositiveUnits).Append("\n");
            sb.Append("  NegativeUnits: ").Append(NegativeUnits).Append("\n");
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
            return this.Equals(obj as QuoteHomeConversionFactors);
        }

        /// <summary>
        /// Returns true if QuoteHomeConversionFactors instances are equal
        /// </summary>
        /// <param name="other">Instance of QuoteHomeConversionFactors to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(QuoteHomeConversionFactors other)
        {
            // credit: http://stackoverflow.com/a/10454552/677735
            if (other == null)
                return false;

            return 
                (
                    this.PositiveUnits == other.PositiveUnits ||
                    this.PositiveUnits != null &&
                    this.PositiveUnits.Equals(other.PositiveUnits)
                ) && 
                (
                    this.NegativeUnits == other.NegativeUnits ||
                    this.NegativeUnits != null &&
                    this.NegativeUnits.Equals(other.NegativeUnits)
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
                if (this.PositiveUnits != null)
                    hash = hash * 59 + this.PositiveUnits.GetHashCode();
                if (this.NegativeUnits != null)
                    hash = hash * 59 + this.NegativeUnits.GetHashCode();
                return hash;
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        { 
            yield break;
        }
    }

}

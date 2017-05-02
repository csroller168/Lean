/*
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
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Smooth and high sensitive moving Average. This moving average reduce lag of the informations
    /// but still being smooth to reduce noises.
    /// Source: http://www.arnaudlegoux.com/index.html
    /// </summary>
    /// <seealso cref="IndicatorDataPoint"/>
    public class ArnaudLegouxMovingAverage : WindowIndicator<IndicatorDataPoint>
    {
        private decimal[] weightVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArnaudLegouxMovingAverage"/> class.
        /// </summary>
        /// <param name="name">string - a name for the indicator</param>
        /// <param name="period">int - the number of periods to calculate the ALMA</param>
        /// <param name="sigma">int - this parameter is responsible for the shape of the curve coefficients.</param>
        /// <param name="offset">decimal -  This parameter allows regulating the smoothness and high sensitivity of the Moving Average. The range for this parameter is [0, 1]. </param>
        public ArnaudLegouxMovingAverage(string name, int period, int sigma, decimal offset= 0.85m)
            : base(name, period)
        {
            if (offset < 0 || offset > 1) throw new ArgumentException("Offset parameter range is [0,1]", "offset");
            var m = Math.Floor(offset * (period - 1));
            var s = period * 1m / sigma;
            var tmpVector = Vector<double>.Build.Dense(period,
                i => Math.Exp((double) (-(i - m) * (i - m) / (2 * s * s))));
            tmpVector = tmpVector.Divide(tmpVector.Sum());
            weightVector = tmpVector.Select(x => (decimal) x).Reverse().ToArray();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArnaudLegouxMovingAverage"/> class.
        /// </summary>
        /// <param name="period">int - the number of periods to calculate the ALMA</param>
        /// <param name="sigma">int - .</param>
        /// <param name="offset">decimal -  This parameter allows regulating the smoothness and high sensitivity of the Moving Average. The range for this parameter is [0, 1]. </param>
        public ArnaudLegouxMovingAverage(int period, int sigma, decimal offset = 0.85m)
            : this(string.Format("ALMA_{0}_{1}_{2}", period, sigma, offset), period, sigma, offset)
        {
        }

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>
        /// A new value for this indicator
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window,
            IndicatorDataPoint input)
        {
            if (!IsReady) return input;
            var alma = Decimal.Zero;
            for (int i = 0; i < window.Count; i++)
            {
                alma += window[i].Price * weightVector[i];
            }
            return alma;
        }
    }
}
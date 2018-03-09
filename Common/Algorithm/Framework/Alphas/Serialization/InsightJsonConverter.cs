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
 *
*/

using System;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Framework.Alphas.Serialization
{
    /// <summary>
    /// Defines how insights should be serialized to json
    /// </summary>
    public class InsightJsonConverter : TypeChangeJsonConverter<Alpha, SerializedInsight>
    {
        /// <summary>
        /// Convert the input value to a value to be serialzied
        /// </summary>
        /// <param name="value">The input value to be converted before serialziation</param>
        /// <returns>A new instance of TResult that is to be serialzied</returns>
        protected override SerializedInsight Convert(Alpha value)
        {
            return new SerializedInsight(value);
        }

        /// <summary>
        /// Converts the input value to be deserialized
        /// </summary>
        /// <param name="value">The deserialized value that needs to be converted to T</param>
        /// <returns>The converted value</returns>
        protected override Alpha Convert(SerializedInsight value)
        {
            var insight = new Alpha(
                Time.UnixTimeStampToDateTime(value.GeneratedTime),
                new Symbol(SecurityIdentifier.Parse(value.Symbol), value.Ticker),
                value.Type,
                value.Direction,
                TimeSpan.FromSeconds(value.Period),
                value.Magnitude,
                value.Confidence
            )
            {
                CloseTimeUtc = Time.UnixTimeStampToDateTime(value.CloseTime),
                EstimatedValue = value.EstimatedValue,
                ReferenceValue = value.Reference
            };

            // set score values
            if (value.ScoreMagnitude != 0)
            {
                insight.Score.SetScore(AlphaScoreType.Magnitude, value.ScoreMagnitude, insight.CloseTimeUtc);
            }

            if (value.ScoreDirection != 0)
            {
                insight.Score.SetScore(AlphaScoreType.Direction, value.ScoreDirection, insight.CloseTimeUtc);
            }
            if (value.ScoreIsFinal)
            {
                insight.Score.Finalize(insight.CloseTimeUtc);
            }

            return insight;
        }
    }
}

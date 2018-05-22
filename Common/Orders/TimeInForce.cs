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
*/

using System;
using QuantConnect.Interfaces;
using QuantConnect.Orders.TimeInForces;
using QuantConnect.Securities;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Time In Force - defines the length of time over which an order will continue working before it is canceled
    /// </summary>
    public abstract class TimeInForce : IEquatable<TimeInForce>, ITimeInForceHandler
    {
        /// <summary>
        /// Gets a <see cref="TimeInForce"/> instance with <see cref="TimeInForceType.GoodTilCanceled"/> type
        /// </summary>
        public static readonly TimeInForce GoodTilCanceled = new GoodTilCanceledTimeInForce();

        /// <summary>
        /// Gets a <see cref="TimeInForce"/> instance with <see cref="TimeInForceType.Day"/> type
        /// </summary>
        public static readonly TimeInForce Day = new DayTimeInForce();

        /// <summary>
        /// The type of Time In Force
        /// </summary>
        public TimeInForceType Type { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeInForce"/> class
        /// </summary>
        protected TimeInForce(TimeInForceType type)
        {
            Type = type;
        }

        /// <summary>
        /// Converts a <see cref="TimeInForceType"/> value to a new <see cref="TimeInForce"/> instance.
        /// </summary>
        public static implicit operator TimeInForce(TimeInForceType type)
        {
            switch (type)
            {
                case TimeInForceType.GoodTilCanceled:
                    return new GoodTilCanceledTimeInForce();

                case TimeInForceType.Day:
                    return new DayTimeInForce();

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, $"Implicit conversion from TimeInForceType.{type} to TimeInForce not supported.");
            }
        }

        /// <summary>
        /// Returns a <see cref="TimeInForceType"/> value from a <see cref="TimeInForce"/> instance.
        /// </summary>
        public static implicit operator TimeInForceType(TimeInForce timeInForce)
        {
            return timeInForce.Type;
        }

        /// <summary>
        /// Equals operator
        /// </summary>
        /// <param name="left">The left operand</param>
        /// <param name="right">The right operand</param>
        /// <returns>True if both symbols are equal, otherwise false</returns>
        public static bool operator ==(TimeInForce left, TimeInForce right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (ReferenceEquals(left, null)) return false;
            if (ReferenceEquals(right, null)) return false;

            return left.Type == right.Type;
        }

        /// <summary>
        /// Not equals operator
        /// </summary>
        /// <param name="left">The left operand</param>
        /// <param name="right">The right operand</param>
        /// <returns>True if both symbols are not equal, otherwise false</returns>
        public static bool operator !=(TimeInForce left, TimeInForce right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Determines whether the specified <see cref="TimeInForce"/> is equal to the current <see cref="TimeInForce"/>.
        /// </summary>
        /// <param name="obj">The object to compare with the current object. </param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public bool Equals(TimeInForce obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            return Type.Equals(obj.Type);
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <param name="obj">The object to compare with the current object. </param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            if (obj is TimeInForceType)
                return (TimeInForceType) obj == Type;

            var other = obj as TimeInForce;
            return other != null && Type.Equals(other.Type);
        }

        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>A hash code for the current <see cref="TimeInForce"/>.</returns>
        public override int GetHashCode()
        {
            return Type.GetHashCode();
        }

        /// <summary>
        /// Checks if an order is expired
        /// </summary>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to be checked</param>
        /// <returns>Returns true if the order has expired, false otherwise</returns>
        public abstract bool IsOrderExpired(Security security, Order order);

        /// <summary>
        /// Checks if an order fill is valid
        /// </summary>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to be checked</param>
        /// <param name="fill">The order fill to be checked</param>
        /// <returns>Returns true if the order fill can be emitted, false otherwise</returns>
        public abstract bool IsFillValid(Security security, Order order, OrderEvent fill);
    }
}

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
using NUnit.Framework;
using QuantConnect.Indicators;

namespace QuantConnect.Tests.Indicators
{
    [TestFixture]
    public class SwissArmyKnifeTests
    {

        [Test]
        public void ResetsProperly()
        {
            var sak = new SwissArmyKnife(4, 0.1, SwissArmyKnifeTool.BandPass);

            foreach (var data in TestHelper.GetDataStream(5))
            {
                sak.Update(data);
            }
            Assert.IsTrue(sak.IsReady);
            Assert.AreNotEqual(0m, sak.Current.Value);
            Assert.AreNotEqual(0, sak.Samples);

            sak.Reset();

            TestHelper.AssertIndicatorIsInDefaultState(sak);
        }

        [Test]
        public void ComparesBandPassAgainstExternalData()
        {
            var indicator = new SwissArmyKnife("", 20, 0.1, SwissArmyKnifeTool.BandPass);
            RunTestIndicator(indicator, "BP");
        }

        private static void RunTestIndicator(IndicatorBase<IndicatorDataPoint> indicator, string field)
        {
            TestHelper.TestIndicator(indicator, "spy_swiss.txt", field, (actual, expected) => { AssertResult(expected, actual.Current.Value); });
        }

        private static void AssertResult(double expected, decimal actual)
        {
            System.Diagnostics.Debug.WriteLine(expected + "," + actual + "," + Math.Abs((decimal)expected - actual));
            Assert.IsTrue(Math.Abs((decimal)expected - actual) < 0.05m);
        }

    }
}

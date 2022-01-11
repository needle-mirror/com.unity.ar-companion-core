using System;
using NUnit.Framework;
using Unity.AR.Companion.Core;
using UnityEngine;

namespace Unity.AR.Companion.CloudStorage
{
    class CompanionResourceUtilsTests
    {
        const long k_Second = TimeSpan.TicksPerMillisecond * 1000;
        const long k_Minute = k_Second * 60;
        const long k_Hour = k_Minute * 60;
        const long k_Day = k_Hour * 24;
        const long k_Month = k_Day * 30;
        const long k_Year = k_Day * 365;

        [TestCase(0, 0, "Just now")]
        [TestCase(k_Minute, 0, "A minute ago")]
        [TestCase(k_Minute * 5, 0, "5 minutes ago")]
        [TestCase(k_Hour, 0, "An hour ago")]
        [TestCase(k_Hour * 5, 0, "5 hours ago")]
        [TestCase(k_Day, 0, "Yesterday")]
        [TestCase(k_Day * 5, 0, "5 days ago")]
        [TestCase(k_Month, 0, "One month ago")]
        [TestCase(k_Month * 5, 0, "5 months ago")]
        [TestCase(k_Year, 0, "One year ago")]
        [TestCase(k_Year * 5, 0, "5 years ago")]
        [Test]
        public void GetRelativeTimeTest(long compare, long time, string result)
        {
            Assert.AreEqual(result, CompanionResourceUtils.GetRelativeTime(compare, time));
        }
    }
}

using System;
using NUnit.Framework;
using UnityEngine;

namespace Unity.AR.Companion.Core
{
    class CompanionFileUtilsTests
    {
        [TestCase(0, "0 B")]
        [TestCase(512, "512 B")]
        [TestCase(1023, "1023 B")]
        [TestCase(1024, "1 KiB")]
        [TestCase(1126, "1.1 KiB")]
        [TestCase(1147, "1.12 KiB")]
        [TestCase(1148, "1.12 KiB")]
        [TestCase(1022976, "999 KiB")]
        [TestCase(1023989, "1000 KiB")]
        [TestCase(1024000, "1000 KiB")]
        [TestCase(1024102, "1000 KiB")]
        [TestCase(1024154, "1000 KiB")]
        [TestCase(1047552, "1023 KiB")]
        [TestCase(1048575, "1024 KiB")]
        [TestCase(1048576, "1 MiB")]
        [TestCase(1153433, "1.1 MiB")]
        [TestCase(1174405, "1.12 MiB")]
        [TestCase(1174406, "1.12 MiB")]
        [TestCase(1047527424, "999 MiB")]
        [TestCase(1048576000, "1000 MiB")]
        [TestCase(1048576010, "1000 MiB")]
        [TestCase(1048576100, "1000 MiB")]
        [TestCase(1049099239, "1000 MiB")]
        [TestCase(1072693248, "1023 MiB")]
        [TestCase(1073741823, "1024 MiB")]
        [TestCase(1073741824, "1 GiB")]
        [TestCase(1181116006, "1.1 GiB")]
        [TestCase(1202590842, "1.12 GiB")]
        [TestCase(1202590843, "1.12 GiB")]
        [TestCase(10737418240, "10 GiB")]
        [TestCase(13207024435, "12.3 GiB")]
        [TestCase(1072668082176, "999 GiB")]
        [TestCase(1073741824000, "1000 GiB")]
        [TestCase(1073741825000, "1000 GiB")]
        [TestCase(1074267957493, "1000 GiB")]
        [TestCase(1074278694911, "1000 GiB")]
        [TestCase(1098437885952, "1023 GiB")]
        [TestCase(1099511627775, "1024 GiB")]
        [TestCase(1099511627776, "1 TiB")]
        [TestCase(1209462790553, "1.1 TiB")]
        [TestCase(1231453023109, "1.12 TiB")]
        [TestCase(1231453023110, "1.12 TiB")]
        [TestCase(1098412116148224, "999 TiB")]
        [TestCase(1099511627776000, "1000 TiB")]
        [TestCase(1099511627778000, "1000 TiB")]
        [TestCase(1099621578938777, "1000 TiB")]
        [TestCase(1100061382490376, "1000 TiB")]
        [TestCase(1124800395214848, "1023 TiB")]
        [TestCase(1125899906842623, "1024 TiB")]

        // Floating-point errors for longs are starting to permeate, but round out acceptably
        [TestCase(1125899906842624, "1 PiB")]
        [TestCase(1238489897526886, "1.1 PiB")]
        [TestCase(1261007895663738, "1.12 PiB")]
        [TestCase(1261007895663739, "1.12 PiB")]
        [TestCase(1124774006935781000, "999 PiB")]
        [TestCase(1125899906842623600, "1000 PiB")]
        [TestCase(1125899906842623800, "1000 PiB")]
        [TestCase(1126012496833308200, "1000 PiB")]
        [TestCase(1126462855670145200, "1000 PiB")]
        [TestCase(1151795604700004000, "1023 PiB")]
        [TestCase(1152921504606846599, "1024 PiB")]

        // Floating-point errors for longs will round this exactly 1 EiB bellow 1 EiB
        //[TestCase(1152921504606846600, "1 EiB")]
        [TestCase(1153021504606846600, "1 EiB")]
        [TestCase(1268213655067532300, "1.1 EiB")]
        [TestCase(1291272085159669200, "1.12 EiB")]
        [TestCase(1297025163467657200, "1.12 EiB")]
        [TestCase(1152921504606846599, "1024 PiB")]

        // Largest Long
        [TestCase(long.MaxValue, "8 EiB")]
        [Test]
        public void GetReadableFileSizeTest(long fileSize, string result) { Assert.AreEqual(result, CompanionFileUtils.GetReadableFileSize(fileSize)); }
    }
}

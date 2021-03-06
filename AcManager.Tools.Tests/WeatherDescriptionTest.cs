﻿using System;
using System.Linq;
using AcManager.Tools.Data;
using AcTools.Utils.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcManager.Tools.Tests {
    [TestClass]
    public class WeatherDescriptionTest {
        [TestMethod]
        public void FindClosestTest() {
            var values = Enum.GetValues(typeof(WeatherType)).OfType<WeatherType>().ToArray();
            WeatherType? result = null;

            foreach (var type in values) {
                for (var i = 1; i < values.Length; i++) {
                    result = WeatherDescription.FindClosestWeather(GoodShuffle.Get(values).Take(i), type);
                }
            }

            Console.WriteLine($"result: {result}");
        }
    }
}

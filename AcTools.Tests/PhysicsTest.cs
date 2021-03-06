using System.Diagnostics;
using System.IO;
using System.Reflection;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class PhysicsTest {
        [TestMethod]
        public void TurboTest() {
            var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!testDir.EndsWith("AcTools.Tests") && testDir.Length > 4) testDir = Path.GetDirectoryName(testDir);
            testDir = Path.Combine(testDir, "test");

            var data = new DataDirectoryWrapper(Path.Combine(testDir, "physics", "turbo_test"));
            var torque = TorquePhysicUtils.LoadCarTorque(data);

            Assert.AreEqual(100d, torque.InterpolateLinear(0d), 0.1);
            Assert.AreEqual(125d, torque.InterpolateLinear(1500d), 0.1);
            Assert.AreEqual(150d, torque.InterpolateLinear(3000d), 0.1);
            Assert.AreEqual(150d, torque.InterpolateLinear(4000d), 0.1);
            Assert.AreEqual(150d, torque.InterpolateLinear(5000d), 0.1);
        }

        [TestMethod]
        public void CtrlTest() {
            var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!testDir.EndsWith("AcTools.Tests") && testDir.Length > 4) testDir = Path.GetDirectoryName(testDir);
            testDir = Path.Combine(testDir, "test");

            var data = new DataDirectoryWrapper(Path.Combine(testDir, "physics", "ctrl_test"));
            var torque = TorquePhysicUtils.LoadCarTorque(data);

            Assert.AreEqual(100d, torque.InterpolateLinear(0d), 0.1);
            Assert.AreEqual(100d, torque.InterpolateLinear(1000d), 0.1);
            Assert.AreEqual(125d, torque.InterpolateLinear(1500d), 0.1);
            Assert.AreEqual(166.7, torque.InterpolateLinear(2000d), 0.1);
            Assert.AreEqual(183.3, torque.InterpolateLinear(2500d), 0.1);
            Assert.AreEqual(200d, torque.InterpolateLinear(3000d), 0.1);
            Assert.AreEqual(150d, torque.InterpolateLinear(3500d), 0.1);
            Assert.AreEqual(100d, torque.InterpolateLinear(4000d), 0.1);
            Assert.AreEqual(100d, torque.InterpolateLinear(5000d), 0.1);
        }

        [TestMethod]
        public void InterpolationMode() {
            var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!testDir.EndsWith("AcTools.Tests") && testDir.Length > 4) testDir = Path.GetDirectoryName(testDir);
            testDir = Path.Combine(testDir, "test");

            var data = new DataDirectoryWrapper(Path.Combine(testDir, "physics", "two_points"));

            var powerLutPointsOnly = TorquePhysicUtils.LoadCarTorque(data, false, 0);
            Assert.AreEqual(100d, powerLutPointsOnly.InterpolateLinear(0d), 0.1);
            Assert.AreEqual(125d, powerLutPointsOnly.InterpolateLinear(1250d), 0.1);
            Assert.AreEqual(150d, powerLutPointsOnly.InterpolateLinear(2500d), 0.1);
            Assert.AreEqual(200d, powerLutPointsOnly.InterpolateLinear(5000d), 0.1);

            var detailedMode = TorquePhysicUtils.LoadCarTorque(data, false);
            Assert.AreEqual(100d, detailedMode.InterpolateLinear(0d), 0.1);
            Assert.AreEqual(109.8d, detailedMode.InterpolateLinear(1250d), 0.1);
            Assert.AreEqual(139.2d, detailedMode.InterpolateLinear(2500d), 0.1);
            Assert.AreEqual(164.2, detailedMode.InterpolateLinear(3200d), 0.1);
            Assert.AreEqual(200d, detailedMode.InterpolateLinear(4000d), 0.1);
            Assert.AreEqual(200d, detailedMode.InterpolateLinear(5000d), 0.1);
        }

        [TestMethod]
        public void NegativePoint() {
            var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!testDir.EndsWith("AcTools.Tests") && testDir.Length > 4) testDir = Path.GetDirectoryName(testDir);
            testDir = Path.Combine(testDir, "test");

            var data = new DataDirectoryWrapper(Path.Combine(testDir, "physics", "negative_point"));

            var powerLutPointsOnly = TorquePhysicUtils.LoadCarTorque(data);
            Assert.AreEqual(100, powerLutPointsOnly.InterpolateLinear(-100d), 0.1);
            Assert.AreEqual(100d, powerLutPointsOnly.InterpolateLinear(0d), 0.1);
            Assert.AreEqual(200d, powerLutPointsOnly.InterpolateLinear(5000d), 0.1);
        }

        [TestMethod]
        public void ConsiderLimiterMode() {
            var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!testDir.EndsWith("AcTools.Tests") && testDir.Length > 4) testDir = Path.GetDirectoryName(testDir);
            testDir = Path.Combine(testDir, "test");

            var data = new DataDirectoryWrapper(Path.Combine(testDir, "physics", "two_points"));

            var notConsider = TorquePhysicUtils.LoadCarTorque(data, false, 0);
            Assert.AreEqual(160, notConsider.InterpolateLinear(3000d), 0.1);
            Assert.AreEqual(200d, notConsider.InterpolateLinear(5000d), 0.1);

            var consider = TorquePhysicUtils.LoadCarTorque(data, true, 0);
            Assert.AreEqual(156.3d, consider.InterpolateLinear(3000d), 0.1);
            Assert.AreEqual(156.3d, consider.InterpolateLinear(5000d), 0.1);

            var notConsiderDetailed = TorquePhysicUtils.LoadCarTorque(data, false);
            Assert.AreEqual(156.3d, notConsiderDetailed.InterpolateLinear(3000d), 0.1);
            Assert.AreEqual(200d, notConsiderDetailed.InterpolateLinear(5000d), 0.1);

            var considerDetailed = TorquePhysicUtils.LoadCarTorque(data);
            Assert.AreEqual(156.3d, considerDetailed.InterpolateLinear(3000d), 0.1);
            Assert.AreEqual(156.3d, considerDetailed.InterpolateLinear(5000d), 0.1);
        }
    }
}
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sequlite.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.Statistics.Tests
{
    [TestClass()]
    public class RollingStatsTests
    {

        public TestContext TestContext { get; set; }
        private static RollingStats _s;

        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            _s = new RollingStats();
        }

        [DataSource(@"Microsoft.VisualStudio.TestTools.DataSource.CSV", "|DataDirectory|\\test_data_random.csv", "test_data_random#csv", DataAccessMethod.Sequential), DeploymentItem("test_data_random.csv"), TestMethod()]
        public void RollingStatsTest_Count()
        {
            // arrange
            int row = TestContext.DataRow.Table.Rows.IndexOf(TestContext.DataRow);
            if(row == 0)
            {
                _s.Reset();
            }
            double v = Convert.ToDouble(TestContext.DataRow["Value"]);
            double count = Convert.ToDouble(TestContext.DataRow["Count"]);
            
            // act
            _s.Update(v);

            // assert
            Assert.AreEqual(count, _s.Count, 0.1);
        }

        [DataSource(@"Microsoft.VisualStudio.TestTools.DataSource.CSV", "|DataDirectory|\\test_data_random.csv", "test_data_random#csv", DataAccessMethod.Sequential), DeploymentItem("test_data_random.csv"), TestMethod()]
        public void RollingStatsTest_Mean()
        {
            int row = TestContext.DataRow.Table.Rows.IndexOf(TestContext.DataRow);
            if (row == 0)
            {
                _s.Reset();
            }
            double v = Convert.ToDouble(TestContext.DataRow["Value"]);
            double expected = Convert.ToDouble(TestContext.DataRow["CumMean"]);
            _s.Update(v);

            Assert.AreEqual(expected, _s.Mean, 0.001);
        }

        [DataSource(@"Microsoft.VisualStudio.TestTools.DataSource.CSV", "|DataDirectory|\\test_data_random.csv", "test_data_random#csv", DataAccessMethod.Sequential), DeploymentItem("test_data_random.csv"), TestMethod()]
        public void RollingStatsTest_ConfidenceHigh()
        {
            int row = TestContext.DataRow.Table.Rows.IndexOf(TestContext.DataRow);
            if (row == 0)
            {
                _s.Reset();
            }
            double v = Convert.ToDouble(TestContext.DataRow["Value"]);
            double expected = Convert.ToDouble(TestContext.DataRow["CumCI95H"]);
            _s.Update(v);

            Assert.AreEqual(expected, _s.ConfidenceHigh, 0.001);
        }

        [DataSource(@"Microsoft.VisualStudio.TestTools.DataSource.CSV", "|DataDirectory|\\test_data_random.csv", "test_data_random#csv", DataAccessMethod.Sequential), DeploymentItem("test_data_random.csv"), TestMethod()]
        public void RollingStatsTest_ConfidenceLow()
        {
            int row = TestContext.DataRow.Table.Rows.IndexOf(TestContext.DataRow);
            if (row == 0)
            {
                _s.Reset();
            }
            double v = Convert.ToDouble(TestContext.DataRow["Value"]);
            double expected = Convert.ToDouble(TestContext.DataRow["CumCI95L"]);
            _s.Update(v);

            Assert.AreEqual(expected, _s.ConfidenceLow, 0.001);
        }

        [DataSource(@"Microsoft.VisualStudio.TestTools.DataSource.CSV", "|DataDirectory|\\test_data_random.csv", "test_data_random#csv", DataAccessMethod.Sequential), DeploymentItem("test_data_random.csv"), TestMethod()]
        public void RollingStatsTest_Variance()
        {
            int row = TestContext.DataRow.Table.Rows.IndexOf(TestContext.DataRow);
            if (row == 0)
            {
                _s.Reset();
            }
            double v = Convert.ToDouble(TestContext.DataRow["Value"]);
            double expected = Convert.ToDouble(TestContext.DataRow["CumVar"]);
            _s.Update(v);

            Assert.AreEqual(expected, _s.Variance, 0.001);
        }

        [DataSource(@"Microsoft.VisualStudio.TestTools.DataSource.CSV", "|DataDirectory|\\test_data_random.csv", "test_data_random#csv", DataAccessMethod.Sequential), DeploymentItem("test_data_random.csv"), TestMethod()]
        public void RollingStatsTest_StdDev()
        {
            int row = TestContext.DataRow.Table.Rows.IndexOf(TestContext.DataRow);
            if (row == 0)
            {
                _s.Reset();
            }
            double v = Convert.ToDouble(TestContext.DataRow["Value"]);
            double expected = Convert.ToDouble(TestContext.DataRow["CumStdDev"]);
            _s.Update(v);

            Assert.AreEqual(expected, _s.StdDev, 0.001);
        }

        [TestMethod()]
        public void ResetTest()
        {
            _s.Reset();
            Assert.AreEqual(_s.Count, 0);
            Assert.AreEqual(_s.Mean, 0);
        }

       /* [TestMethod()]
        public void UpdateTest()
        {
            Assert.Fail();
        }*/
    }
}
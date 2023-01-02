using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sequlite.ALF.RecipeLib;
using System;

namespace Sequlite.ALF.RecipeLib.Tests
{
    [TestClass]
    public class RecipeHelpersTests
    {
        [TestMethod]
        public void HasOvershot_PositiveSlopeAboveTarget_Returns_True()
        {
            double start = 1.0;
            double current = 3.0;
            double target = 2.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HasOvershot_NegativeSlopeAboveTarget_Returns_False()
        {
            double start = 3.0;
            double current = 2.0;
            double target = 1.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void HasOvershot_NegativeSlopeBelowTarget_Returns_True()
        {
            double start = 3.0;
            double current = 1.0;
            double target = 2.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void HasOvershot_PositiveSlopeBelowTarget_Returns_False()
        {
            double start = 1.0;
            double current = 2.0;
            double target = 3.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void HasOvershot_PositiveSlopeAtTarget_Returns_False()
        {
            double start = 1.0;
            double current = 2.0;
            double target = 2.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsFalse(result);
        }
        [TestMethod]
        public void HasOvershot_NegativeSlopeAtTarget_Returns_False()
        {
            double start = 2.0;
            double current = 1.0;
            double target = 1.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsFalse(result);
        }
        
        [TestMethod]
        public void HasOvershot_ZeroSlopeAboveTarget_Returns_False()
        {
            double start = 2.0;
            double current = 2.0;
            double target = 1.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsFalse(result);
        }
        [TestMethod]
        public void HasOvershot_ZeroSlopeAtTarget_Returns_False()
        {
            double start = 2.0;
            double current = 2.0;
            double target = 2.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsFalse(result);
        }
        [TestMethod]
        public void HasOvershot_ZeroSlopeBelowTarget_Returns_False()
        {
            double start = 2.0;
            double current = 2.0;
            double target = 3.0;

            bool result = RecipeHelpers.HasOvershot(start, current, target);

            Assert.IsFalse(result);
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.RecipeLib.Tests
{
    [TestClass()]
    public class ImageListKeeperTests
    {
        [TestMethod()]
        public void Insert_ValidFile_ChangesCount()
        {
            var fakeTile = "tL101A";
            var fakeFile = "Experiment_Read_Inc1_G1_bL101A_X00.00Y00.00mm_-350.00um_0.069s_80Int_PD2599.tif";
            var listKeeper = ImageListKeeper.GetImageListKeeper();

            listKeeper.Insert(fakeTile, fakeFile);

            Assert.AreEqual(1, listKeeper.TotalImageCount());
        }

        [TestMethod()]
        public void Insert_InvalidFile_Throws()
        {
            var fakeTile = "tL101A";
            var fakeFile = "Experiment_Read_Inc1_G1.tif";
            var listKeeper = ImageListKeeper.GetImageListKeeper();

             Assert.ThrowsException<System.ArgumentException>(() => listKeeper.Insert(fakeTile, fakeFile));
        }

        [TestMethod()]
        public void ClearTile_Returns_ZeroTileCount()
        {
            var fakeTile = "tL101A";
            var fakeFile = "Experiment_Read_Inc1_G1_bL101A_X00.00Y00.00mm_-350.00um_0.069s_80Int_PD2599.tif";
            var listKeeper = ImageListKeeper.GetImageListKeeper();

            listKeeper.Insert(fakeTile, fakeFile);
            listKeeper.ClearTile(fakeTile);

            Assert.AreEqual(0, listKeeper.TileImageCount(fakeTile));
        }

        [TestMethod()]
        public void Reset_ReturnsZeroTotalCount()
        {
            var fakeTile = "tL101A";
            var fakeFile = "Experiment_Read_Inc1_G1_bL101A_X00.00Y00.00mm_-350.00um_0.069s_80Int_PD2599.tif";
            var listKeeper = ImageListKeeper.GetImageListKeeper();

            listKeeper.Insert(fakeTile, fakeFile);
            listKeeper.Reset();

            Assert.AreEqual(0, listKeeper.TotalImageCount());
        }

        [TestMethod()]
        public void TileCountCompleteTest()
        {
            Assert.Fail();
        }
    }
}
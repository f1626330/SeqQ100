using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Sequlite.ALF.RecipeLib
{
    /// <summary>
    /// Keeps track of the location of tiles that are missing images.
    /// Tiles with missing images are revisited and the images are re-captured.
    /// </summary>
    public class TileWatcher
    {
        private List<Tuple<int, int>> _lostTiles; //< first = index of region with missing image(s), second = index of the focus to re-capture (0 = top, 1 = bottom)
        private object _lostTilesLocker; //< used for locking _lostTiles since it is accessed from the ProcessImageQ thread
        private ISeqLog _logger = SeqLogFactory.GetSeqFileLog("Tile Watcher");

        private static TileWatcher _TileWatcher = null;
        private static object _instanceCreationLocker = new object(); //< used for locking during instantiation

        /// <summary>
        /// Singleton constructor
        /// </summary>
        /// <returns>A singleton instance of the TileWatcher class</returns>
        public static TileWatcher GetTileWatcher()
        {
            if (_TileWatcher == null)
            {
                lock (_instanceCreationLocker)
                {
                    if (_TileWatcher == null)
                    {
                        _TileWatcher = new TileWatcher();
                    }
                }
            }
            return _TileWatcher;
        }
        private TileWatcher()
        {
            _lostTilesLocker = new object();
            _lostTiles = new List<Tuple<int, int>>();
            _logger.LogMessage($"TileWatcher initialized. Thread: {Thread.CurrentThread.ManagedThreadId}.", SeqLogMessageTypeEnum.INFO);
        }

        ~TileWatcher()
        {
            _logger.LogMessage($"TileWatcher finalized. Thread: {Thread.CurrentThread.ManagedThreadId}.", SeqLogMessageTypeEnum.INFO);

        }
        /// <summary>
        /// Retrieve the information for a lost tile
        /// </summary>
        /// <param name="index">The index of the lost tile [0 , DroppedTileCount()]</param>
        /// <param name="regionNum">The region index of the lost tile</param>
        /// <param name="focusNum">The focus index of the lost tile</param>
        public void LostTileInfo(in int index, ref int regionNum, ref int focusNum)
        {
            try
            {
                lock (_lostTilesLocker)
                {
                    regionNum = _lostTiles[index].Item1;
                    focusNum = _lostTiles[index].Item2;
                }
            }
            catch (ArgumentOutOfRangeException e)
            {
                _logger.Log($"Index OOB Exception in LostTileInfo: {e}", SeqLogFlagEnum.DEBUG);
            }
        }

        /// <summary>
        /// Adds tile location to a list. Only succeeds if the tile is not already in the list, limiting the number of recapture tries to 1.
        /// </summary>
        /// <param name="tileSite">The location of a tile [region, focus]</param>
        /// <returns>True if the tile was added to the list. False if the tile was already in the list </returns>
        public bool MarkTileLost(Tuple<int, int> tileSite)
        {
            lock (_lostTilesLocker)
            {
                if (!_lostTiles.Contains(tileSite))
                {
                    _lostTiles.Add(tileSite);
                    _logger.LogMessage($"Marked lost tile: {tileSite}. Count:{_lostTiles.Count}. Thread: {Thread.CurrentThread.ManagedThreadId}", SeqLogMessageTypeEnum.INFO);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// Returns the number of tiles with missing images that
        /// have been detected since the list time the list was reset.
        /// </summary>
        /// <returns>The number of elements in the dropped tile list</returns>
        public int LostTileCount()
        {
            return _lostTiles.Count();
        }

        /// <summary>
        /// Clears the lost tile list. Usually called at the end of a cycle after image re-capture has been attempted.
        /// </summary>
        public void Reset()
        {
            _lostTiles.Clear();
            _logger.LogMessage($"TileWatcher reset. Thread: {Thread.CurrentThread.ManagedThreadId}.", SeqLogMessageTypeEnum.INFO);
        }

        /// <summary>
        /// Generates a list of tile names for the images captured in an incorporation cycle. Each name is
        /// expected to appear in the list four times.
        /// <seealso cref="OLAJobManager.BuildTileList"/>
        /// </summary>
        /// <param name="imageDir">The directory where the images are stored. </param>
        /// <param name="cycle">The number of the incorporation cycle (loop) to look at images from. </param>
        /// <param name="tileName">Optional paramater to restrict results to a specific tile name. 
        /// If omitted or blank, all tiles for the cycle are listed. </param>
        /// <returns>A list of tile names generated during an incorporation cycle. (ex: tL101B)
        /// Each unique tile name is expected to appear in the list four times. </returns>
        private List<string> ListTilesForCycle(in DirectoryInfo imageDir, in int cycle, string tileName = "")
        {
            List<string> tileNames = null;

            try
            {
                tileNames = new List<string>();

                // image file names are expected to be of the form:
                // ExperimentName_Read_Inc1_G1_bL101A_X00.00Y00.00mm_-350.00um_0.069s_80Int_PD2599.tif
                // only consider images from this cycle
                string cycleTag = "_Inc" + cycle + "_";
                var matchingFiles = imageDir.GetFiles("*" + cycleTag + "*.tif", SearchOption.TopDirectoryOnly);

                // a list of the color tags that are expected in the file name
                var colorList = new List<string> { "G1", "G2", "R3", "R4" };

                foreach (FileInfo info in matchingFiles)
                {
                    string fileName = info.Name;
                    // trim everything before the cycle tag in case the user chooses a strange experiment name
                    fileName = fileName.Substring(fileName.IndexOf(cycleTag) + 1);
                    // get a list of the name parts
                    string[] imageNameParts = fileName.Split('_');

                    for (int i = 1; i < imageNameParts.Length; ++i)
                    {
                        if (colorList.Contains(imageNameParts[i - 1]))
                        {
                            // the tile name must follow the color (G1, G2, R3, R4)
                            // so, if the previous items was a color then the current item must be a name
                            string name = imageNameParts[i];
                            if (name.EndsWith("mm"))
                            {
                                name = name.Remove(name.Length - 2);
                            }

                            // if no tileName is specified, include images for all tiles
                            // otherwise, list only images whose names match tileName
                            if (string.IsNullOrEmpty(tileName))
                            {
                                tileNames.Add(name);
                            }
                            else if (name.Equals(tileName))
                            {
                                tileNames.Add(name);
                            }

                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception in ListTilesForCycle: {e}");
            }

            return tileNames;
        }

        public bool VerifyTile(in string imageSavePath, in int cycle, string tileName = "")
        {
            int imageMask = 0;
            try
            {
                // get the folder where the images are saved
                DirectoryInfo imageDir = new DirectoryInfo(imageSavePath);

                // image file names are expected to be of the form:
                // ExperimentName_Read_Inc1_G1_bL101A_X00.00Y00.00mm_-350.00um_0.069s_80Int_PD2599.tif
                // only consider images from this cycle
                string cycleTag = "_Inc" + cycle + "_";
                var matchingFiles = imageDir.GetFiles("*" + cycleTag + "*" + tileName + "*.tif", SearchOption.TopDirectoryOnly);

                // a list of the color tags that are expected in the file name
                var colorList = new List<string> { "G1", "G2", "R3", "R4" };

                foreach (FileInfo info in matchingFiles)
                {
                    string fileName = info.Name;
                    // trim everything before the cycle tag in case the user chooses a strange experiment name
                    fileName = fileName.Substring(fileName.IndexOf(cycleTag) + 1);
                    // get a list of the name parts
                    string[] imageNameParts = fileName.Split('_');

                    for (int i = 1; i < imageNameParts.Length; ++i)
                    {
                        int bitNumber = colorList.IndexOf(imageNameParts[i - 1]);
                        if(bitNumber == -1)
                        {
                            // value not found
                            continue;
                        }
                        else if (bitNumber < 0 || bitNumber > 3) // restrict to setting first 4 bits
                        {
                            throw new ArgumentOutOfRangeException($"Image Mask Bit {bitNumber} OOB. Valid range: [0,3]");
                        }
                        else
                        {
                            // set the bit of the imageMask
                            imageMask |= 1 << bitNumber;
                            break;
                        }
                    }
                }
            }
            catch(ArgumentOutOfRangeException e)
            {
                Debug.WriteLine($"Argument Out of Range Exception in CheckTileImages: {e}");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Unhandled Exception in CheckTileImages: {e}");
            }

            // image mask should be b1111 (decimal 15) if all four images are present
            if(imageMask == 15)
            {
                return true;
            }
            else
            {
                _logger.LogMessage($"Image verification failed for tile {tileName}. Mask: {imageMask}", SeqLogMessageTypeEnum.WARNING);
                return false;
            }
        }

        /// <summary>
        /// Attempts to find all saved images for a given tile and cycle number.
        /// </summary>
        /// <param name="imageDir">The directory to search for images. </param>
        /// <param name="cycleNumber">Limits the search to images acquired for a specific incorporation cycle. </param>
        /// <param name="tileName">Limits the search to images acquired for a specific tile. </param>
        /// <returns>Returns 0 if no missing images were found, [1-4] if missing images were found,
        /// or a negative number if extra images were found.
        /// </returns>
        public int MissingImageCount(in string imageDir, in int cycleNumber, string tileName)
        {
            int missingImageCount = -1;

            // modeled from OldJobManager.BuildTileList and ImageProcessingCMD.FileCreateProcTxt
            try
            {
                // get the folder where the images are saved
                DirectoryInfo imageDataDir = new DirectoryInfo(imageDir);

                // generate a list of all tile names found for this cycle
                List<string> tileNameList = ListTilesForCycle(imageDataDir, cycleNumber, tileName);

                // check that the tile name appears four times in the list
                const int expectedImageCount = 4;
                int imageCount = tileNameList.Where(x => x.Equals(tileName)).Count();
                missingImageCount = expectedImageCount - imageCount;

                _logger.LogMessage($"Missing image check found {missingImageCount} missing images for tile: " +
                    $"{tileName} Cycle number: {cycleNumber}.", SeqLogMessageTypeEnum.WARNING);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception in MissingImagesForTile: {e}");
            }

            return missingImageCount;
        }

    }
}

using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sequlite.ALF.RecipeLib
{
    /// <summary>
    /// Keeps a record of all unique file names. Prevents duplicate images for a tile by matching: [cycle, color, tile].
    /// If an image is a duplicate, it replaces the previous entry in the record.
    ///
    /// New entries are recorded when the image processing queue writes the file to disk.
    /// Duplicate entries are not allowed. Duplicates are identified using a "fully qualified" token generated from the image name.
    /// The "fully qualified" image token consists of [cycle + color + tile] (ex: Inc3_G1_bL101A)
    /// 
    /// The number of images that have been acquired for each tile is used to
    /// notify OLA of a complete tile and to attempt recovery of missing images.
    /// 
    /// The total number of images that have been acquired for each cycle is used to 
    /// notify OLA of a complete loop and to check for missing images/begin recovery if necessary.
    /// 
    /// Entries are written to file every time a complete tile has been imaged.
    /// The file (list.txt) is updated and the image names acquired for that tile are appended to the file.
    /// 
    /// </summary>
    public class ImageListKeeper
    {

        private const int IMG_PER_TILE = 4; //< the expected number of images to acquire for each tile

        private static ImageListKeeper _listKeeper = null;
        private static object _instanceCreationLocker = new object(); //< used for locking during instantiation

        // regex to extract tokens for each part of an image file name
        //private const string _pattern = @"_Inc(?<cycle>\d+)_(?<color>(G1|G2|R3|R4))_(?<surface>(b|t))L(?<lane>(1|2|3|4))(?<column>\d{2})(?<row>(A|B|C|D))_.*\.tif";

        // regex to extract a "fully qualified" image token [cycle + color + tile] (ex: Inc14_R3_tL101A)
        private const string _tokenPattern = @"_(?<token>Inc(\d+)_(G1|G2|R3|R4)_(b|t)L(1|2|3|4)(\d{2})(A|B|C|D))_.*\.tif";

        // regex to extract the tile name (ex: tL101A)
        private const string _tilePattern = @"_(?<tile>(b|t)L(1|2|3|4)(\d{2})(A|B|C|D))_.*\.tif";

        // * image record is reset at the beginning of each cycle
        // * fileNames are added to a map when the file is processed
        // * all fileNames for a tile are updated to file only once the tile is complete and verified
        // * the number of images for a tile can be found by counting the number of entiries in the map
        // * the the number of images for a cycle can be found by counting the number of
        // image record = map<tileName-> map<token->fileName> >
        // tL101A
        //     Inc1_G1_tL101A
        //         b123x_Inc1_G1_tL101A_X30.00Y06.10mm_-741.03um_0.059s_50Int_PD1173.tif
        //     Inc1_G2_tL101A
        //         b123x_Inc1_G2_tL101A_X30.00Y06.10mm_-741.03um_0.059s_50Int_PD1173.tif
        //     ...
        // tL101B
        //     Inc1_G1_tL101B
        //         b123x_Inc1_G1_tL101B_X30.00Y06.10mm_-741.03um_0.059s_50Int_PD1173.tif
        //     Inc1_G2_tL101B
        //         b123x_Inc1_G2_tL101B_X30.00Y06.10mm_-741.03um_0.059s_50Int_PD1173.tif
        //     Inc1_R3_tL101B
        //         b123x_Inc1_R3_tL101B_X30.00Y06.10mm_-741.03um_0.059s_50Int_PD1173.tif
        //     ...
        // ...

        // outer dictionary (string to Dictionary): track data by tile (key). Each tile maps to a dictionary of token->fileName
        // inner dictionary (string to string): track data by token (key). Each token maps to a filename
        private Dictionary<string, Dictionary<string, string>> _imageRecord;

        /// <summary>
        /// Singleton constructor
        /// </summary>
        /// <returns>A singleton instance of the ImageListKeeper class</returns>
        public static ImageListKeeper GetImageListKeeper()
        {
            if (_listKeeper == null)
            {
                lock (_instanceCreationLocker)
                {
                    if (_listKeeper == null)
                    {
                        _listKeeper = new ImageListKeeper();
                    }
                }
            }
            return _listKeeper;
        }
        private ImageListKeeper()
        {
            _imageRecord = new Dictionary<string, Dictionary<string, string>>();

            ISeqLog logger = SeqLogFactory.GetSeqFileLog(nameof(ImageListKeeper));
            logger.LogMessage($"ImageListKeeper initialized. Thread: {Thread.CurrentThread.ManagedThreadId}.", SeqLogMessageTypeEnum.INFO);
        }

        ~ImageListKeeper()
        {
            ISeqLog logger = SeqLogFactory.GetSeqFileLog(nameof(ImageListKeeper));
            logger.LogMessage($"ImageListKeeper finalized. Thread: {Thread.CurrentThread.ManagedThreadId}.", SeqLogMessageTypeEnum.INFO);

        }

        public string SaveFilePath { get; set; } //< the directory to write the file (list.txt) TK

        /// <summary>
        /// Adds an image file name to the record. The image file name is expected to follow the format:
        /// @"_Inc<cycle>_<color>_<surface>L<lane><column><row>"
        /// A unique token extracted from the file name is used to prevent duplicate entries.
        /// </summary>
        /// <param name="tileName">The tile that this image correseponds to</param>
        /// <param name="imageFileName">The name of the image</param>
        /// <returns>True if insertion was successful, false if insertion failed</returns>
        public void Insert(in string tileName, in string imageFileName)
        {
            if(tileName == null || imageFileName == null) { return; }
            string token = ExtractToken(imageFileName);
            if (token == String.Empty)
            {
                ISeqLog logger = SeqLogFactory.GetSeqFileLog(nameof(ImageListKeeper));
                logger.LogMessage($"Image file does not contain valid token: {imageFileName}", SeqLogMessageTypeEnum.WARNING);
            }
            if (!ExtractTileName(imageFileName).Equals(tileName))
            {
                ISeqLog logger = SeqLogFactory.GetSeqFileLog(nameof(ImageListKeeper));
                logger.LogMessage($"Image file does not match tile name. File: {imageFileName} Tile name: {tileName}", SeqLogMessageTypeEnum.WARNING);
            }
            Dictionary<string, string> tileImages; // dictionary of token->fileName
                                                   // check if a dictionary has been created for this cycle
            if (_imageRecord.ContainsKey(tileName))
            {
                tileImages = _imageRecord[tileName];
            }
            else
            {
                // create a new token->fileName dictionary for this cycle
                tileImages = new Dictionary<string, string>();
                _imageRecord.Add(tileName, tileImages);
            }
            tileImages[token] = imageFileName; // if the key exists, it is replaced (prevent duplicates)
                                               //_imageRecord[cycle] = cycleImages; // TK check: should not need to replace if by-reference
        }

        public bool ClearTile(in string tileName)
        {
            return _imageRecord.Remove(tileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tileName"></param>
        /// <returns>True if the number of images stored for the tile matches the expected number of images</returns>
        public bool TileCountComplete(in string tileName)
        {
            return IMG_PER_TILE == TileImageCount(tileName);
        }

        /// <summary>
        /// Extracts a unique fully-qualified token from an image file name.
        /// The fully-qualified token consists of [cycle + color + tile] (ex: Inc3_G1_bL101A)
        /// The image file name is expected to follow the format:
        /// @"*_Inc<cycle>_<color>_<surface>L<lane><column><row>"
        /// </summary>
        /// <param name="imageFileName"></param>
        /// <returns>If successful, returns a string containing the token, otherwise returns an empty string</returns>
        private string ExtractToken(in string imageFileName)
        {
            string token = string.Empty;

            // extract a unique token from the image file name [cycle + color + tile]
            Match m = Regex.Match(imageFileName, _tokenPattern);

            if (m.Success && m.Groups["token"].Success)
            {
                token = m.Groups["token"].Value;
            }

            return token;
        }

        private string ExtractTileName(in string imageFileName)
        {
            string tile = string.Empty;

            // extract a unique token from the image file name [cycle + color + tile]
            Match m = Regex.Match(imageFileName, _tilePattern);

            if (m.Success && m.Groups["tile"].Success)
            {
                tile = m.Groups["tile"].Value;
            }

            return tile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cycle"></param>
        /// <param name="imageFileName"></param>
        /// <returns></returns>
        public int TileImageCount(in string tileName)
        {
            try
            {
                if (_imageRecord.ContainsKey(tileName))
                {
                    // return the number of tokens saved for the tile
                    return _imageRecord[tileName].Keys.ToList().Count;
                }
                else
                {
                    // cycle is not in the record
                }
            }
            catch (Exception e)
            {
                ISeqLog logger = SeqLogFactory.GetSeqFileLog(nameof(ImageListKeeper));
                logger.LogMessage($"Exception in TileImageCount: {e}", SeqLogMessageTypeEnum.WARNING);
            }
            return 0;
        }

        /// <summary>
        /// Counts the total number of images acquired for a cycle
        /// </summary>
        /// <returns>The number of images recorded for the cycle.</returns>
        public int TotalImageCount()
        {
            int count = 0;
            foreach (Dictionary<string, string> tileRecord in _imageRecord.Values)
            {
                count += tileRecord.Count;
            }
            return count;
        }

        /// <summary>
        /// Updates the file set in SaveFilePath
        /// </summary>
        public void DumpTileToFile(in string tileName)
        {
            // check if a dictionary has been created for this cycle
            if (_imageRecord.ContainsKey(tileName))
            {
                try
                {
                    using (StreamWriter sw = File.AppendText(SaveFilePath)) // Path.Combine(imgData.RecipeRunImageDataDir, "list.txt")))
                    {
                        Dictionary<string, string> tileImages = _imageRecord[tileName];
                        foreach (KeyValuePair<string, string> image in tileImages)
                        {
                            sw.WriteLine(image.Value);
                        }
                    }
                }
                catch (Exception e)
                {
                    ISeqLog logger = SeqLogFactory.GetSeqFileLog(nameof(ImageListKeeper));
                    logger.LogMessage($"Exception in DumpTileToFile: {e}", SeqLogMessageTypeEnum.WARNING);
                }
            }
        }

        public void DumpCycleToFile()
        {
            try
            {
                using (StreamWriter sw = File.AppendText(SaveFilePath)) // Path.Combine(imgData.RecipeRunImageDataDir, "list.txt")))
                {
                    foreach (Dictionary<string, string> tileDict in _imageRecord.Values)
                    {
                        foreach (KeyValuePair<string, string> imageDict in tileDict)
                        {
                            sw.WriteLine(imageDict.Value);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ISeqLog logger = SeqLogFactory.GetSeqFileLog(nameof(ImageListKeeper));
                logger.LogMessage($"Exception in DumpCycleToFile: {e}", SeqLogMessageTypeEnum.WARNING);
            }
        }

        /// <summary>
        /// Erases the image record. Usually called at the end of each cycle 
        /// after the file has been updated with image records
        /// </summary>
        public void Reset()
        {
            _imageRecord.Clear();
        }
    }
}

using AForge.Imaging;
using AForge.Imaging.Filters;
using Sequlite.ALF.Common;
using Sequlite.Image.Processing.Imaging;
//using AForge.Math.Geometry;
using System;
using System.Collections.Generic;
//using System.Reflection;
using System.Diagnostics;   //Debug.Assert
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sequlite.Image.Processing
{
    /// <summary>
    /// image process class
    /// </summary>
    public class ImageProcessing
    {
        public enum FlipAxis
        {
            Horizontal,
            Vertical
        }
        //3-channel image processing event
        private static AutoResetEvent threadProcChan1Event = new AutoResetEvent(false);
        private static AutoResetEvent threadProcChan2Event = new AutoResetEvent(false);
        private static AutoResetEvent threadProcChan3Event = new AutoResetEvent(false);
        //private static AutoResetEvent threadProcChan4Event = new AutoResetEvent(false);
        private static string VersionPattern = @"\s*\w+\s*\[\s*Version\s*=\s*(?<version_number>\d+)\s*\w*\s*\]";
        /// <summary>
        /// file type define
        /// </summary>
        public const int BMP_FILE = 0x03;
        public const int JPG_FILE = 0x02;
        public const int TIFF_FILE = 0x01;

        private static ISeqLog Logger = SeqLogFactory.GetSeqFileLog("IMAGEPROC");
        public static bool IsSupportedPixelFormat(PixelFormatType pixelFormat)
        {
            if (pixelFormat != PixelFormatType.P8u_C1 && pixelFormat != PixelFormatType.P8u_C3 && pixelFormat != PixelFormatType.P8u_C4 &&
                pixelFormat != PixelFormatType.P16u_C1 && pixelFormat != PixelFormatType.P16u_C3 && pixelFormat != PixelFormatType.P16u_C4)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        public static ImageInfo ReadMetadata(string filename)
        {
            byte[] byteArray = null;
            ImageInfo imageInfo = null;
            BitmapSource srcBitmap = null;
            BitmapMetadata metadata = null;
            try
            {
                srcBitmap = BitmapFrame.Create(new Uri(filename));
                if (srcBitmap != null)
                {
                    metadata = (BitmapMetadata)srcBitmap.Metadata;
                    if (metadata != null)
                    {
                        int version =  GetImageMetadataVersion(metadata);
                        if (version > 1) //new data
                        {
                            //imageinfo is json string
                            string jsonString =  (string)metadata.GetQuery("/ifd/{ushort=40092}");
                            imageInfo = JsonSerializer.Deserialize<ImageInfo>(jsonString);
                        }
                        else
                        {
                            imageInfo = null;
                            try
                            {
                                byteArray = (byte[])metadata.GetQuery("/ifd/{ushort=40092}");
                                imageInfo = (ImageInfo)ByteArrayToObject(byteArray);
                            }
                            catch(Exception)
                            {
                                Logger.LogWarning("Failed to read binary imageinfo metadata, it may be in an older format.");
                                imageInfo = null;
                            }

                            if (imageInfo == null)
                            {
                                Logger.Log("Try to read metadata from an old version image.");
                                //meta data was save as binary data in Sequlite.ALF.Imaging.dll
                                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                                Assembly asem = null;
                                foreach (var assembly in assemblies)
                                {
                                    if (assembly.FullName.Contains("Sequlite.ALF.Imaging"))
                                    {
                                        Logger.Log("Found assemble Sequlite.ALF.Imaging is loaded.");
                                        asem = assembly;
                                        break;
                                    }
                                }
                                if (asem != null)
                                {
                                    string namespaceName = "Sequlite.ALF.Imaging";
                                    foreach (var t in asem.GetTypes())
                                    {

                                        if ((t.Namespace == namespaceName) && t.IsClass && t.Name == "ReadMetadata")
                                        {
                                            string methodName = "ReadImageInfoAndConvert";
                                            var m = t.GetMethod(methodName, (BindingFlags.Static | BindingFlags.Public));
                                            var result = m?.Invoke(null, new object[] { filename });
                                            if (result != null)
                                            {
                                                imageInfo = result as ImageInfo;
                                            }
                                            break;
                                        }
                                    }

                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                imageInfo = null;
                Logger.LogError("Failed to read metadata from image file:  " + filename + ". Excption: " + ex.ToString());
            }
            finally
            {
                srcBitmap = null;
                metadata = null;
                byteArray = null;
            }
            return imageInfo;
        }
        public static int GetImageMetadataVersion(BitmapMetadata metadata)
        {
            int version = 1;
            string str = metadata.ApplicationName;
            if (!string.IsNullOrEmpty(str))
            {
                Match m = Regex.Match(str, VersionPattern, RegexOptions.IgnoreCase);
                if (m.Success && m.Groups.Count > 0)
                {
                    string test = m.Groups["version_number"].Value;
                    int n;
                    bool b = int.TryParse(test, out n);
                    if (b)
                    {
                        version = n;
                    }

                }
            }
            return version;
        }

        #region public static void Save(string strFilePath, WriteableBitmap srcImage, ImageInfo srcImageInfo, bool bCompressed, bool bOverrideDefaultDpi = false, bool saveImage = true)
        /// <summary>
        /// save to file
        /// </summary>
        /// <param name="strFilePath">file directory</param>
        /// <param name="srcImage">target image data</param>
        /// <param name="srcImageInfo">image info</param>
        /// <param name="bCompressed">compression</param>
        public static void Save(string strFilePath, WriteableBitmap srcImage, ImageInfo srcImageInfo,
            bool bCompressed, bool bOverrideDefaultDpi = false, bool saveImage = true)
        {
            WriteableBitmap imageToSave = null;
            using (FileStream fileStream = new System.IO.FileStream(strFilePath, System.IO.FileMode.Create, FileAccess.ReadWrite))
            {
                
                BitmapMetadata metadata = null;

                if (srcImageInfo == null)
                {
                    srcImageInfo = new ImageInfo();
                }
                else
                {
                    //Invert chemi image.
                    if (srcImageInfo.MixChannel.IsInvertChecked == true)
                    {
                        imageToSave = Invert(srcImage);
                        srcImageInfo.IsPixelInverted = true;
                    }
                }

                if (imageToSave == null)
                {
                    imageToSave = srcImage;  // image data not inverted
                }

                // Change DPI values (default: 300 dpi)
                // DPI <= 96; set to 300 dpi
                if (imageToSave.DpiX != 300)
                {
                    double dDpiX = imageToSave.DpiX;
                    double dDpiY = imageToSave.DpiY;

                    if (!bOverrideDefaultDpi)
                    {
                        if (dDpiX <= 96 || dDpiY <= 96)
                        {
                            dDpiX = 300.0;
                            dDpiY = 300.0;
                        }
                    }

                    int width = imageToSave.PixelWidth;
                    int height = imageToSave.PixelHeight;
                    int stride = imageToSave.BackBufferStride;
                    PixelFormat format = imageToSave.Format;
                    BitmapPalette palette = imageToSave.Palette;
                    byte[] data = new byte[(long)stride * (long)height];

                    imageToSave.CopyPixels(data, stride, 0);
                    imageToSave = null;
                    WriteableBitmap target = new WriteableBitmap(width, height,
                                                                 dDpiX, dDpiY,
                                                                 format, palette);
                    target.WritePixels(new Int32Rect(0, 0, width, height), data, stride, 0);
                    data = null;
                    imageToSave = target;
                }

                TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                try
                {
                    if (bCompressed)
                    {
                        encoder.Compression = TiffCompressOption.Lzw;
                    }
                    else
                    {
                        encoder.Compression = TiffCompressOption.None;
                    }

                    if (imageToSave.Metadata != null)
                    {
                        metadata = (BitmapMetadata)imageToSave.Metadata.Clone();
                    }
                    else
                    {
                        metadata = new BitmapMetadata("tiff");
                    }

                    //uint paddingAmount = 4096; // 4Kb padding
                    //metadata.SetQuery("/ifd/PaddingSchema:Padding", paddingAmount);
                    //metadata.SetQuery("/ifd/exif/PaddingSchema:Padding", paddingAmount);
                    //metadata.SetQuery("/xmp/PaddingSchema:Padding", paddingAmount);

                    ImageChannelType currentSelected = srcImageInfo.SelectedChannel;

                    srcImageInfo.SelectedChannel = ImageChannelType.Mix;

                    //metadata.ApplicationName = "Sequlite ALF";
                    SetImageMetadataApplicationNameWithVersion(metadata, 2); //bump version if needed
                    metadata.SetQuery("/ifd/exif:{ushort=271}", "Sequlite Genomics US Inc.");   // Make
                    string model = (srcImageInfo.InstrumentModel == null) ? "ALF" : srcImageInfo.InstrumentModel;
                    metadata.SetQuery("/ifd/exif:{ushort=272}", model);
                    //metadata.DateTaken = srcImageInfo.DateTime;
                    //to do: change following line to json serialization
                    //byte[] metadataByte = ObjectToByteArray(srcImageInfo);   // serialize image info
                    //metadata.SetQuery("/ifd/{ushort=40092}", metadataByte);  // save image info in metadata's comments tag
                    string jsonString = JsonSerializer.Serialize<ImageInfo>(srcImageInfo); // serialize image info
                    metadata.SetQuery("/ifd/{ushort=40092}", jsonString);  // save image info in metadata's comments tag

                    BitmapFrame tifFrame = null;
                    tifFrame = BitmapFrame.Create(imageToSave, null, metadata, null);
                    encoder.Frames.Add(tifFrame);
                    //if !saveImage, 0 byte file will be created
                    if (saveImage)
                    {
                        encoder.Save(fileStream);
                    }

                    srcImageInfo.SelectedChannel = currentSelected;

                    // metadataByte = null;
                    tifFrame = null;
                }
                catch (System.Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    throw ex;
                }
                finally
                {
                    imageToSave = null;

                    fileStream?.Close();
                    fileStream?.Dispose();
                }
            }
        }
        #endregion public static void Save(string strFilePath, WriteableBitmap srcImage, ImageInfo srcImageInfo, bool bCompressed, bool bOverrideDefaultDpi = false, bool saveImage = true)

        #region Save function with SaveNET
        public static void Save(string FilePath, byte[] Imagedata, ImageInfo ImageInfo, bool compress, bool createDirectory = true)
        {
            try
            {
                SaveNET.ImageParams parameters = new SaveNET.ImageParams(
                                (ulong)ImageInfo.MixChannel.ROI.Width,
                                (ulong)ImageInfo.MixChannel.ROI.Height,
                                (ulong)ImageInfo.MixChannel.BitDepth,
                                true);
                SaveNET.ImageWriter writer = new SaveNET.ImageWriter(parameters, FilePath);
                SaveNET.ETiffCompression compmode = compress ? SaveNET.ETiffCompression.Lzw : SaveNET.ETiffCompression.NoCompression;
                writer.SetTiff("tif", compmode, false);
                writer.Save(Imagedata, createDirectory);
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            
        }
        #endregion Save function with SaveNET

        #region public static Object ByteArrayToObject(byte[] arrBytes)
        /// <summary>
        /// Deserialize: Convert a byte array to an Object
        /// </summary>
        /// <param name="arrBytes"></param>
        /// <returns></returns>
        public static Object ByteArrayToObject(byte[] arrBytes)
        {
            if (arrBytes == null) { return null; }

            MemoryStream memStream = new MemoryStream();
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            memStream.Write(arrBytes, 0, arrBytes.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            Object obj = (Object)binFormatter.Deserialize(memStream);

            return obj;
        }
        #endregion
        private static void SetImageMetadataApplicationNameWithVersion(BitmapMetadata metadata, int version)
        {
            metadata.ApplicationName = string.Format("Sequlite ALF[Version={0}]", version); //must be a positive integer
        }
        public static unsafe WriteableBitmap Crop(WriteableBitmap srcBitmap, Rect roiRect)
        {
            if (srcBitmap == null)
                return null;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            byte* pSrcData = null;
            byte* pDstData = null;
            double dDpiX = srcBitmap.DpiX;
            double dDpiY = srcBitmap.DpiY;
            WriteableBitmap dstBitmap = new WriteableBitmap((int)roiRect.Width, (int)roiRect.Height, dDpiX, dDpiY, srcBitmap.Format, null);

            pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();
            pDstData = (byte*)dstBitmap.BackBuffer.ToPointer();

            int srcStep = srcBitmap.BackBufferStride;
            int dstSetp = dstBitmap.BackBufferStride;

            IppImaging.Crop(pSrcData, srcStep, pDstData, dstSetp, roiRect, pixelFormat);

            // Force garbage collection.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            // Force garbage collection.
            //GC.Collect();
            // Wait for all finalizers to complete before continuing.
            //GC.WaitForPendingFinalizers();

            return dstBitmap;
        }

        public static unsafe WriteableBitmap Invert(WriteableBitmap srcBitmap)
        {
            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int width = srcBitmap.PixelWidth;
            int height = srcBitmap.PixelHeight;
            double dDpiX = srcBitmap.DpiX;
            double dDpiY = srcBitmap.DpiY;
            WriteableBitmap dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, srcBitmap.Format, null);

            byte* pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();
            byte* pDstData = (byte*)dstBitmap.BackBuffer.ToPointer();

            //int srcStep = ((srcBitmap.PixelWidth * srcBitmap.Format.BitsPerPixel) + 31) / 32 * 4;
            int srcStep = srcBitmap.BackBufferStride;
            int dstSetp = dstBitmap.BackBufferStride;

            IppiSize roiSize = new IppiSize(width, height);
            IppImaging.Invert(pSrcData, srcStep, roiSize, pixelFormat, pDstData, dstSetp);

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return dstBitmap;
        }

        public static unsafe WriteableBitmap Rotate(WriteableBitmap srcBitmap, double angle)
        {
            if (srcBitmap == null)
                return null;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int width = srcBitmap.PixelWidth;
            int height = srcBitmap.PixelHeight;
            int srcStep = srcBitmap.BackBufferStride;
            int dstStep = srcStep;
            PixelFormat format = srcBitmap.Format;
            BitmapPalette palette = srcBitmap.Palette;
            double dDpiX = srcBitmap.DpiX;
            double dDpiY = srcBitmap.DpiY;
            WriteableBitmap dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, format, palette);
            byte* pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();
            byte* pDst = (byte*)dstBitmap.BackBuffer.ToPointer();
            IppiSize srcSize = new IppiSize(width, height);
            
            IppImaging.Rotate(pSrc, srcSize, srcStep, pDst, dstStep, angle, 0, 0, (int)IppInter.IPPI_INTER_NN, pixelFormat);

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();
            Rect croproi = new Rect(5, 2, width-22, height-5);
            return Crop(dstBitmap, croproi);
        }

        #region unused function
        #region public static bool SaveMetadata(string strFileName, ImageInfo imageInfo, int iFileType, bool compressed)
        /// <summary>
        /// Save image information to the comments section of the image metadata.
        /// </summary>
        /// <param name="strFileName"></param>
        /// <param name="metadata"></param>
        /// <param name="imgFormat"></param>
        /// <param name="compressed"></param>
        /// <returns></returns>
        /***public static bool SaveMetadata(string strFileName, ImageInfo imageInfo, int iFileType, bool compressed)
        {
            if (iFileType != JPG_FILE && iFileType != TIFF_FILE)
            {
                return false;
            }

            // serialize image info
            byte[] metadataByte = ObjectToByteArray(imageInfo);

            if (iFileType == JPG_FILE)
            {
                AddMetadataToJpeg(strFileName, metadataByte);
                return true;
            }

            // Get the source image stream
            using (FileStream imageFileStream = new FileStream(strFileName, FileMode.Open))
            {
                // Create new metadata first, here we are making an IPTC block in a JPG
                // NOTE: IPTC tags do not get parsed correctly on Windows 7
                BitmapMetadata bmpMetaData = new BitmapMetadata("tiff");

                bmpMetaData.SetQuery("/ifd/{ushort=40092}", metadataByte); //metadata is saved in comments section

                //bmpMetaData.SetQuery("/ifd/xmp:{ushort=262}", imageInfo.PhotometricInterpolation.ToString());

                // Load the image in the decoder

                TiffBitmapDecoder decoder = new TiffBitmapDecoder(imageFileStream,
                                                                    BitmapCreateOptions.PreservePixelFormat,
                                                                    BitmapCacheOption.Default);

                //public static BitmapFrame Create(BitmapSource source,
                //                                   BitmapSource thumbnail,
                //                                   BitmapMetadata metadata,
                //                                   ReadOnlyCollection<ColorContext> colorContexts);

                // Make a copy of the frame and also pass in the new metadata
                BitmapFrame frameCopy = BitmapFrame.Create(decoder.Frames[0],
                                                            null,
                                                            bmpMetaData,
                                                            decoder.ColorContexts);

                // Now we have the image frame that has a fresh IPTC metadata block

                // Create a new encoder and add the frame to it
                TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                encoder.Frames.Add(frameCopy);

                // Save the image file with metadata to a temporary file
                // then replaces the 'original' image file with the temporary image file.
                string tmpFilePath = Path.GetTempFileName();
                using (Stream streamTemp = File.Open(tmpFilePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    if (compressed)
                    {
                        encoder.Compression = TiffCompressOption.Lzw;
                    }
                    else
                    {
                        encoder.Compression = TiffCompressOption.None;
                    }

                    encoder.Save(streamTemp);

                    imageFileStream.Close();
                    imageFileStream.Dispose();
                    streamTemp.Close();
                    streamTemp.Dispose();
                    File.Copy(tmpFilePath, strFileName, true);
                    File.Delete(tmpFilePath);
                }

            }

            return true;
        }***/
        #endregion
        private static WriteableBitmap CreateWriteableBitmap(Stream stream)
        {
            BitmapImage bi = new BitmapImage();

            // Begin initialization.
            bi.BeginInit();
            // Set properties.
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.None | BitmapCreateOptions.PreservePixelFormat;
            bi.StreamSource = stream;
            // End initialization.
            bi.EndInit();
            bi.Freeze();

            PixelFormat pixelFormat;

            // Convert a BitmapSource to a Different PixelFormat
            if (bi.Format == PixelFormats.Bgr24 || bi.Format == PixelFormats.Bgr32 || bi.Format == PixelFormats.Bgra32)
                pixelFormat = PixelFormats.Rgb24;
            else
                pixelFormat = bi.Format;

            BitmapPalette palette = bi.Palette;

            BitmapSource source = new FormatConvertedBitmap(bi, pixelFormat, palette, 0);
            WriteableBitmap bmp = new WriteableBitmap(source);

            bi = null;

            return bmp;
        }
        /// <summary>
        /// load image file
        /// </summary>
        /// <param name="sFileName">file name</param>
        /// <returns></returns>
        public static WriteableBitmap Load(String @sFilePath)
        {
            WriteableBitmap wbBitmap = null;
            try
            {
                using (var fileStream = new FileStream(@sFilePath, FileMode.Open, FileAccess.Read))
                {
                    // read from file or write to file
                    wbBitmap = CreateWriteableBitmap(fileStream);
                    //if (wbBitmap.CanFreeze)
                    //    wbBitmap.Freeze();
                    fileStream.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }

            return wbBitmap;
        }
        private static WriteableBitmap CreateWriteableBitmapFromMultipages(Stream stream)
        {
            WriteableBitmap wbBitmap = null;
            MemoryStream memstream = new MemoryStream();

            memstream.SetLength(stream.Length);
            stream.Read(memstream.GetBuffer(), 0, (int)stream.Length);

            BitmapDecoder decoder = TiffBitmapDecoder.Create(memstream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            Int32 frameCount = decoder.Frames.Count;
            if (frameCount > 1)
            {
                List<WriteableBitmap> bmpList = new List<WriteableBitmap>();
                foreach (var image in decoder.Frames)
                {
                    bmpList.Add(new WriteableBitmap(image));
                }
                wbBitmap = SetChannel(bmpList.ToArray());
            }
            return wbBitmap;
        }
        /// <summary>
        /// Load image file (support multi-page TIFF file).
        /// </summary>
        /// <param name="sFilePath"></param>
        /// <returns></returns>
        public static WriteableBitmap LoadImage(String @sFilePath)
        {
            WriteableBitmap wbBitmap = null;
            try
            {

                using (var fileStream = new FileStream(@sFilePath, FileMode.Open, FileAccess.Read))
                {
                    int pageCount = GetPageCount(sFilePath);

                    if (pageCount > 1)
                    {
                        wbBitmap = CreateWriteableBitmapFromMultipages(fileStream);
                    }
                    else
                    {
                        wbBitmap = CreateWriteableBitmap(fileStream);
                    }
                    fileStream.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }

            return wbBitmap;
        }
        /// <summary>
        /// save to file
        /// </summary>
        /// <param name="stream">file stream</param>
        /// <param name="iType">file type</param>
        /// <param name="src">image data</param>
        //public static void Save(FileStream stream, int iType, BitmapSource src)
        //{
        //    if (iType == BMP_FILE)
        //    {
        //        BmpBitmapEncoder encoder = new BmpBitmapEncoder();
        //        BitmapFrame bit = BitmapFrame.Create(src);
        //        encoder.Frames.Add(bit);
        //        try
        //        {
        //            encoder.Save(stream);
        //            stream.Flush();
        //            stream.Close();
        //        }
        //        catch (System.Exception ee)
        //        {
        //            Xceed.Wpf.Toolkit.MessageBox.Show(ee.Message.ToString());
        //        }
        //    }
        //    else if (iType == JPG_FILE)
        //    {
        //        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
        //        BitmapFrame bit = BitmapFrame.Create(src);
        //        encoder.Frames.Add(bit);
        //        try
        //        {
        //            encoder.Save(stream);
        //            stream.Flush();
        //            stream.Close();
        //        }
        //        catch (System.Exception ee)
        //        {
        //            Xceed.Wpf.Toolkit.MessageBox.Show(ee.Message.ToString());
        //        }
        //    }
        //    else if (iType == TIFF_FILE)
        //    {
        //        TiffBitmapEncoder encoder = new TiffBitmapEncoder();
        //        encoder.Compression = TiffCompressOption.None;
        //        BitmapFrame bit = BitmapFrame.Create(src);
        //        encoder.Frames.Add(bit);
        //        try
        //        {
        //            encoder.Save(stream);
        //            stream.Flush();
        //            stream.Close();
        //        }
        //        catch (System.Exception ee)
        //        {
        //            Xceed.Wpf.Toolkit.MessageBox.Show(ee.Message.ToString());
        //        }
        //    }

        //}

        //public static void Save(FileStream stream, int iFileType, BitmapSource srcImage, bool bCompressed)
        //{
        //    if (iFileType == BMP_FILE)
        //    {
        //        BmpBitmapEncoder encoder = new BmpBitmapEncoder();
        //        BitmapFrame bitmapFrame = BitmapFrame.Create(srcImage);
        //        encoder.Frames.Add(bitmapFrame);
        //        try
        //        {
        //            encoder.Save(stream);
        //            stream.Flush();
        //            stream.Close();
        //        }
        //        catch (System.Exception ex)
        //        {
        //            Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message.ToString());
        //        }
        //    }
        //    else if (iFileType == JPG_FILE)
        //    {
        //        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
        //        BitmapFrame bitmapFrame = BitmapFrame.Create(srcImage);
        //        encoder.Frames.Add(bitmapFrame);
        //        try
        //        {
        //            encoder.Save(stream);
        //            stream.Flush();
        //            stream.Close();
        //        }
        //        catch (System.Exception ex)
        //        {
        //            Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message.ToString());
        //        }
        //    }
        //    else if (iFileType == TIFF_FILE)
        //    {
        //        TiffBitmapEncoder encoder = new TiffBitmapEncoder();

        //        if (bCompressed)
        //        {
        //            encoder.Compression = TiffCompressOption.Lzw;
        //        }
        //        else
        //        {
        //            encoder.Compression = TiffCompressOption.None;
        //        }

        //        BitmapFrame bitmapFrame = BitmapFrame.Create(srcImage);
        //        encoder.Frames.Add(bitmapFrame);
        //        try
        //        {
        //            encoder.Save(stream);
        //            stream.Flush();
        //            stream.Close();
        //        }
        //        catch (System.Exception ex)
        //        {
        //            Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message.ToString());
        //        }
        //    }

        //}

        //#region public static void Save(string strFilePath, WriteableBitmap srcImage, ImageInfo srcImageInfo, int iFileType, bool bCompressed, bool bOverrideDefaultDpi = false)
        ///// <summary>
        ///// Save image file and image info
        ///// </summary>
        ///// <param name="strFilePath"></param>
        ///// <param name="srcImage"></param>
        ///// <param name="srcImageInfo"></param>
        ///// <param name="iFileType"></param>
        ///// <param name="bCompressed"></param>
        ///// <param name="bOverrideDefaultDpi">Override the default 300 DPI</param>
        //public static void Save(string strFilePath, WriteableBitmap srcImage, ImageInfo srcImageInfo, int iFileType, bool bCompressed, bool bOverrideDefaultDpi = false)
        //{
        //    using (System.IO.FileStream fileStream = new System.IO.FileStream(strFilePath, System.IO.FileMode.Create, FileAccess.ReadWrite))
        //    {
        //        WriteableBitmap imageToSave = null;
        //        BitmapMetadata metadata = null;

        //        if (srcImageInfo == null)
        //        {
        //            srcImageInfo = new ImageInfo();
        //        }
        //        else
        //        {
        //            // Invert the source data if chemi

        //            // Chemi capture type:
        //            //Chemi: Normal
        //            //Chemi: Single Exposure
        //            //Chemi: Cumulative
        //            //Chemi: Multiple
        //            //Chemi with marker: Chemi
        //            //if (srcImageInfo.CaptureType.Contains("Chemi") &&
        //            //    srcImageInfo.LightSourceChan1 == (int)LightCode.None &&
        //            //    srcImageInfo.IsInvertChecked_Mix == true &&
        //            //    iFileType == TIFF_FILE)
        //            //{
        //            //    imageToSave = Invert(new WriteableBitmap(srcImage));
        //            //}
        //            //Invert chemi image.
        //            if (srcImageInfo.IsChemiImage &&
        //                srcImageInfo.MixChannel.IsInvertChecked == true && iFileType == TIFF_FILE)
        //            {
        //                imageToSave = Invert(srcImage);
        //                srcImageInfo.IsPixelInverted = true;
        //            }
        //        }

        //        if (imageToSave == null)
        //        {
        //            imageToSave = srcImage;  // image data not inverted
        //        }

        //        // Change DPI values (default: 300 dpi)
        //        // DPI <= 96; set to 300 dpi
        //        if (imageToSave.DpiX != 300)
        //        {
        //            double dDpiX = imageToSave.DpiX;
        //            Double dDpiY = imageToSave.DpiY;

        //            if (!bOverrideDefaultDpi)
        //            {
        //                if (dDpiX <= 96 || dDpiY <= 96)
        //                {
        //                    dDpiX = 300.0;
        //                    dDpiY = 300.0;
        //                }
        //            }

        //            int width = imageToSave.PixelWidth;
        //            int height = imageToSave.PixelHeight;
        //            int stride = imageToSave.BackBufferStride;
        //            PixelFormat format = imageToSave.Format;
        //            BitmapPalette palette = imageToSave.Palette;
        //            byte[] data = new byte[(long)stride * (long)height];

        //            imageToSave.CopyPixels(data, stride, 0);
        //            imageToSave = null;
        //            WriteableBitmap target = new WriteableBitmap(width, height,
        //                                                         dDpiX, dDpiY,
        //                                                         format, palette);
        //            target.WritePixels(new Int32Rect(0, 0, width, height), data, stride, 0);
        //            data = null;
        //            imageToSave = target;
        //        }

        //        // Get application version string
        //        string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
        //        System.Diagnostics.FileVersionInfo fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
        //        srcImageInfo.SoftwareVersion = fileVersionInfo.ProductVersion;

        //        if (iFileType == BMP_FILE)
        //        {
        //            BmpBitmapEncoder encoder = new BmpBitmapEncoder();
        //            BitmapFrame bitmapFrame = BitmapFrame.Create(imageToSave);
        //            encoder.Frames.Add(bitmapFrame);
        //            try
        //            {
        //                encoder.Save(fileStream);
        //            }
        //            catch (System.Exception ex)
        //            {
        //                Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message.ToString());
        //            }
        //        }
        //        else if (iFileType == JPG_FILE)
        //        {
        //            JpegBitmapEncoder encoder = new JpegBitmapEncoder();

        //            if (imageToSave.Metadata != null)
        //            {
        //                metadata = (BitmapMetadata)imageToSave.Metadata.Clone();
        //            }
        //            else
        //            {
        //                metadata = new BitmapMetadata("jpg");
        //            }

        //            metadata.ApplicationName = "Sapphire";
        //            //metadata.SetQuery("/app1/ifd/exif:{ushort=271}", "Azure Biosystems, Inc.");   // Make
        //            //metadata.DateTaken = srcImageInfo.DateTime;
        //            byte[] metadataByte = ObjectToByteArray(srcImageInfo);           // serialize image info
        //            metadata.SetQuery("/app1/ifd/exif:{uint=40092}", metadataByte);  // save image info to the metadata's comment tag

        //            BitmapFrame jpgFrame = BitmapFrame.Create(imageToSave, null, metadata, null);

        //            encoder.Frames.Add(jpgFrame);
        //            encoder.QualityLevel = 100; // highest quality
        //            try
        //            {
        //                encoder.Save(fileStream);
        //            }
        //            catch (System.Exception ex)
        //            {
        //                throw ex;
        //            }
        //            finally
        //            {
        //                jpgFrame = null;
        //                metadataByte = null;
        //            }
        //        }
        //        else if (iFileType == TIFF_FILE)
        //        {
        //            TiffBitmapEncoder encoder = new TiffBitmapEncoder();

        //            try
        //            {
        //                if (bCompressed)
        //                {
        //                    encoder.Compression = TiffCompressOption.Lzw;
        //                }
        //                else
        //                {
        //                    encoder.Compression = TiffCompressOption.None;
        //                }

        //                if (imageToSave.Metadata != null)
        //                {
        //                    metadata = (BitmapMetadata)imageToSave.Metadata.Clone();
        //                }
        //                else
        //                {
        //                    metadata = new BitmapMetadata("tiff");
        //                }

        //                //uint paddingAmount = 4096; // 4Kb padding
        //                //metadata.SetQuery("/ifd/PaddingSchema:Padding", paddingAmount);
        //                //metadata.SetQuery("/ifd/exif/PaddingSchema:Padding", paddingAmount);
        //                //metadata.SetQuery("/xmp/PaddingSchema:Padding", paddingAmount);

        //                ImageChannelType currentSelected = srcImageInfo.SelectedChannel;

        //                srcImageInfo.SelectedChannel = ImageChannelType.Mix;

        //                metadata.ApplicationName = "Sequlite ALF";
        //                //metadata.SetQuery("/ifd/exif:{ushort=271}", "Azure Biosystems, Inc.");   // Make
        //                //metadata.DateTaken = srcImageInfo.DateTime;
        //                byte[] metadataByte = ObjectToByteArray(srcImageInfo);   // serialize image info
        //                metadata.SetQuery("/ifd/{ushort=40092}", metadataByte);  // save image info in metadata's comments tag

        //                BitmapFrame tifFrame = null;
        //                WriteableBitmap[] allImageChannels = null;
        //                if (srcImage.Format == PixelFormats.Rgba64)
        //                {
        //                    allImageChannels = GetChannel(imageToSave);
        //                    for (int i = 0; i < allImageChannels.Length; i++)
        //                    {
        //                        tifFrame = BitmapFrame.Create((BitmapSource)allImageChannels[i], null, metadata, null);
        //                        encoder.Frames.Add(tifFrame);
        //                    }
        //                }
        //                else
        //                {
        //                    tifFrame = BitmapFrame.Create((BitmapSource)imageToSave, null, metadata, null);
        //                    encoder.Frames.Add(tifFrame);
        //                }

        //                encoder.Save(fileStream);

        //                srcImageInfo.SelectedChannel = currentSelected;

        //                metadataByte = null;
        //                tifFrame = null;
        //                allImageChannels = null;
        //            }
        //            catch (System.Exception ex)
        //            {
        //                throw ex;
        //            }
        //        }

        //        fileStream.Close();
        //        fileStream.Dispose();
        //    }
        //}
        //#endregion
        #region public static bool SaveMetadata(string strFileName, byte[] metadata, int iFileType, bool compressed)
        /// <summary>
        /// Save image information to the comments section of the image metadata.
        /// </summary>
        /// <param name="strFileName"></param>
        /// <param name="metadata"></param>
        /// <param name="imgFormat"></param>
        /// <param name="compressed"></param>
        /// <returns></returns>
        /*public static bool SaveMetadata(string strFileName, byte[] metadata, int iFileType, bool compressed)
        {
            if (iFileType != JPG_FILE && iFileType != TIFF_FILE)
            {
                return false;
            }

            if (iFileType == JPG_FILE)
            {
                AddMetadataToJpeg(strFileName, metadata);
                return true;
            }

            // Get the source image stream
            using (FileStream imageFileStream = new FileStream(strFileName, FileMode.Open))
            {
                // Create new metadata first, here we are making an IPTC block in a JPG
                // NOTE: IPTC tags do not get parsed correctly on Windows 7
                BitmapMetadata bmpMetaData = new BitmapMetadata("tiff");

                bmpMetaData.SetQuery("/ifd/{ushort=40092}", metadata); //metadata is saved in comments section

                //metadata.SetQuery("/ifd/exif:{ushort=262}", imageData.Info.ExposureTime.ToString());     // Exposure time
                //bmpMetaData.SetQuery("/ifd/{ushort=40092}", metadata); //metadata is saved in comments section
                //(ushort)metadata.GetQuery("/ifd/xmp:{uint=262}");

                // Load the image in the decoder

                TiffBitmapDecoder decoder = new TiffBitmapDecoder(imageFileStream,
                                                                    BitmapCreateOptions.PreservePixelFormat,
                                                                    BitmapCacheOption.Default);

                // Make a copy of the frame and also pass in the new metadata
                BitmapFrame frameCopy = BitmapFrame.Create(decoder.Frames[0],
                                                            null,
                                                            bmpMetaData,
                                                            decoder.ColorContexts);

                // Now we have the image frame that has a fresh IPTC metadata block

                // Create a new encoder and add the frame to it
                TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                encoder.Frames.Add(frameCopy);

                // Save the image file with metadata to a temporary file
                // then replaces the 'original' image file with the temporary image file.
                string tmpFilePath = Path.GetTempFileName();
                using (Stream streamTemp = File.Open(tmpFilePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    if (compressed)
                    {
                        encoder.Compression = TiffCompressOption.Lzw;
                    }
                    else
                    {
                        encoder.Compression = TiffCompressOption.None;
                    }

                    encoder.Save(streamTemp);

                    imageFileStream.Close();
                    imageFileStream.Dispose();
                    streamTemp.Close();
                    streamTemp.Dispose();
                    File.Copy(tmpFilePath, strFileName, true);
                    File.Delete(tmpFilePath);
                }

            }

            return true;
        }*/
        #endregion
        #region private static void AddMetadataToJpeg(string imageFilePath, byte[] comments)
        /// <summary>
        /// Save image information to the comments section of the image metadata.
        /// </summary>
        /// <param name="imageFilePath"></param>
        /// <param name="comments"></param>
        private static void AddMetadataToJpeg(string imageFilePath, byte[] comments)
        {
            BitmapDecoder decoder = null;
            BitmapFrame bitmapFrame = null;
            BitmapMetadata metadata = null;
            FileInfo originalImage = new FileInfo(imageFilePath);

            if (File.Exists(imageFilePath))
            {
                // load the jpg file with a JpegBitmapDecoder
                using (Stream jpegStreamIn = File.Open(imageFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    decoder = new JpegBitmapDecoder(jpegStreamIn, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                }

                bitmapFrame = decoder.Frames[0];
                metadata = (BitmapMetadata)bitmapFrame.Metadata;

                if (bitmapFrame != null)
                {
                    BitmapMetadata metaData = (BitmapMetadata)bitmapFrame.Metadata.Clone();

                    if (metaData != null)
                    {
                        // modify the metadata
                        metaData.SetQuery("/app1/ifd/exif:{uint=40092}", comments);

                        // get an encoder to create a new jpg file with the new metadata.      
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapFrame, bitmapFrame.Thumbnail, metaData, bitmapFrame.ColorContexts));

                        // Delete the original
                        originalImage.Delete();

                        // Save the new image 
                        using (Stream jpegStreamOut = File.Open(imageFilePath, FileMode.CreateNew, FileAccess.ReadWrite))
                        {
                            encoder.Save(jpegStreamOut);
                        }
                    }
                }
            }

        }
        #endregion

        #region public static byte[] ReadMetadata(string filename)
        /// <summary>
        /// Get image information from the comments section of the image metadata
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        /*public static byte[] ReadMetadata(string filename)
        {
            byte[] byteArray = null;
            System.Drawing.Imaging.ImageFormat imageFormat = GetImageFormat(filename);
            BitmapSource srcBitmap = null;
            BitmapMetadata metadata = null;

            try
            {
                srcBitmap = BitmapFrame.Create(new Uri(filename));
                if (srcBitmap != null)
                {
                    metadata = (BitmapMetadata)srcBitmap.Metadata;
                    if (metadata != null)
                    {
                        if (imageFormat == System.Drawing.Imaging.ImageFormat.Tiff)
                        {
                            byteArray = (byte[])metadata.GetQuery("/ifd/{ushort=40092}");
                        }
                        else if (imageFormat == System.Drawing.Imaging.ImageFormat.Jpeg)
                        {
                            byteArray = (byte[])metadata.GetQuery("/app1/ifd/exif:{uint=40092}");
                        }
                    }
                }
            }
            catch
            {
            }

            srcBitmap = null;
            metadata = null;

            return byteArray;
        }*/
        #endregion

        #region public static ImageInfo ReadMetadata(string filename)
        /// <summary>
        /// Get image information from the comments field of the image metadata
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        //public static ImageInfo ReadMetadata(string filename)
        //{
        //    byte[] byteArray = null;
        //    ImageInfo imageInfo = null;
        //    System.Drawing.Imaging.ImageFormat imageFormat = GetImageFormat(filename);
        //    BitmapSource srcBitmap = null;
        //    BitmapMetadata metadata = null;

        //    try
        //    {
        //        srcBitmap = BitmapFrame.Create(new Uri(filename));
        //        if (srcBitmap != null)
        //        {
        //            metadata = (BitmapMetadata)srcBitmap.Metadata;
        //            if (metadata != null)
        //            {
        //                if (imageFormat == System.Drawing.Imaging.ImageFormat.Tiff)
        //                {
        //                    byteArray = (byte[])metadata.GetQuery("/ifd/{ushort=40092}");
        //                    imageInfo = (ImageInfo)ByteArrayToObject(byteArray);

        //                    // get photometricInterpolation
        //                    //ushort photometricInt = (ushort)metadata.GetQuery("/ifd/exif:{ushort=262}");
        //                    //imageInfo.PhotometricInterpretation = photometricInt;
        //                }
        //                else if (imageFormat == System.Drawing.Imaging.ImageFormat.Jpeg)
        //                {
        //                    byteArray = (byte[])metadata.GetQuery("/app1/ifd/exif:{uint=40092}");
        //                    imageInfo = (ImageInfo)ByteArrayToObject(byteArray);
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        imageInfo = null;
        //        throw ex;
        //    }
        //    finally
        //    {
        //        srcBitmap = null;
        //        metadata = null;
        //        byteArray = null;
        //        //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        //        //GC.WaitForPendingFinalizers();
        //        //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        //        //GC.Collect();
        //    }

        //    return imageInfo;
        //}
        #endregion

        #region public static System.Drawing.Imaging.ImageFormat GetImageFormat(string strFileName)
        /// <summary>
        /// Get image format from file name
        /// </summary>
        /// <param name="strFileName"></param>
        /// <returns></returns>
        public static System.Drawing.Imaging.ImageFormat GetImageFormat(string strFileName)
        {
            FileInfo fileInfo = new FileInfo(strFileName);
            string strExtension = fileInfo.Extension;
            strExtension = strExtension.ToLower();

            switch (strExtension)
            {
                case ".bmp":
                    return System.Drawing.Imaging.ImageFormat.Bmp;
                case ".jpg":
                    return System.Drawing.Imaging.ImageFormat.Jpeg;
                case ".tif":
                case ".tiff":
                    return System.Drawing.Imaging.ImageFormat.Tiff;
            }

            return null;
        }
        #endregion

        #region public static byte[] ObjectToByteArray(Object obj)
        /// <summary>
        /// Serialize: Convert an object to a byte array
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static byte[] ObjectToByteArray(Object obj)
        {
            if (obj == null) { return null; }

            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            binFormatter.Serialize(ms, obj);

            return ms.ToArray();
        }
        #endregion
        public static byte[] GetBytesFromBitmapSource(BitmapSource bmp)
        {
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = width * ((bmp.Format.BitsPerPixel + 7) / 8);

            byte[] pixels = new byte[height * stride];

            bmp.CopyPixels(pixels, stride, 0);

            return pixels;
        }

        #region TIFF helper

        private static int GetPageCount(string fileName)
        {
            int pageCount = -1;

            try
            {
                System.Drawing.Image img = System.Drawing.Bitmap.FromFile(fileName);
                pageCount = img.GetFrameCount(System.Drawing.Imaging.FrameDimension.Page);
                img.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return pageCount;
        }

        /*private static List<WriteableBitmap> GetAllPages(string file)
        {
            List<WriteableBitmap> images = new List<WriteableBitmap>();
            WriteableBitmap bitmap = (Bitmap)Image.FromFile(file);
            int count = bitmap.GetFrameCount(FrameDimension.Page);
            for (int idx = 0; idx < count; idx++)
            {
                // save each frame to a bytestream
                bitmap.SelectActiveFrame(FrameDimension.Page, idx);
                MemoryStream byteStream = new MemoryStream();
                bitmap.Save(byteStream, ImageFormat.Tiff);

                // and then create a new Image from it
                images.Add(Image.FromStream(byteStream));
            }
            return images;
        }*/

        private static List<WriteableBitmap> GetAllPages(string file)
        {
            List<WriteableBitmap> bmpLst = new List<WriteableBitmap>();

            // Get the source image stream
            using (FileStream imageFileStream = new FileStream(file, FileMode.Open))
            {
                // Load the image in the decoder

                TiffBitmapDecoder decoder = new TiffBitmapDecoder(imageFileStream,
                                                                  BitmapCreateOptions.PreservePixelFormat,
                                                                  BitmapCacheOption.Default);
                // Get all the .TIFF frames
                int totFrames = decoder.Frames.Count;

                for (int i = 0; i < totFrames; ++i)
                {
                    // Create bitmap to hold the single frame
                    WriteableBitmap bmpSingleFrame = new WriteableBitmap(decoder.Frames[i]);
                    // add the frame (as a bitmap) to the bitmap list
                    bmpLst.Add(bmpSingleFrame);
                }
            }
            return bmpLst;
        }

        private System.Drawing.Imaging.ImageCodecInfo GetCodecInfo(string codec)
        {
            System.Drawing.Imaging.ImageCodecInfo codecInfo = null;
            foreach (System.Drawing.Imaging.ImageCodecInfo info in System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders())
            {
                if (info.MimeType == codec)
                {
                    codecInfo = info;
                    break;
                }
            }
            return codecInfo;
        }

        #endregion


        public static unsafe int Min(WriteableBitmap srcBitmap, Rect roiRect)
        {
            if (srcBitmap == null)
            {
                throw new Exception("Source image cannot be null");
            }

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            uint minValue = 0;
            int srcStep = ((srcBitmap.PixelWidth * srcBitmap.Format.BitsPerPixel) + 31) / 32 * 4;
            byte* pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();

            IppImaging.Min(pSrcData, srcStep, pixelFormat, roiRect, ref minValue);

            return (int)minValue;
        }

        public static unsafe int Max(WriteableBitmap srcBitmap, Rect roiRect)
        {
            if (srcBitmap == null)
            {
                throw new Exception("Source image cannot be null");
            }

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            uint maxValue = 0;
            //int srcStep = ((srcBitmap.PixelWidth * srcBitmap.Format.BitsPerPixel) + 31) / 32 * 4;
            int srcStep = srcBitmap.BackBufferStride;
            byte* pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();

            IppImaging.Max(pSrcData, srcStep, pixelFormat, roiRect, ref maxValue);

            return (int)maxValue;
        }

        public static unsafe void MinMax(WriteableBitmap srcBitmap, Rect roiRect, ref uint minValue, ref uint maxValue)
        {
            if (srcBitmap == null)
            {
                throw new Exception("Source image cannot be null");
            }

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int srcStep = ((srcBitmap.PixelWidth * srcBitmap.Format.BitsPerPixel) + 31) / 32 * 4;
            byte* pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();

            IppImaging.MinMax(pSrcData, srcStep, pixelFormat, roiRect, ref minValue, ref maxValue);
        }

        public static unsafe WriteableBitmap[] GetChannel(WriteableBitmap srcBitmap)
        {
            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);

            if (pixelFormat != PixelFormatType.P8u_C3 && pixelFormat != PixelFormatType.P16u_C3 &&
                pixelFormat != PixelFormatType.P8u_C4 && pixelFormat != PixelFormatType.P16u_C4)
            {
                throw new Exception("Image type currently not supported");
            }

            PixelFormat dstPixelFormat = PixelFormats.Gray16;

            if (pixelFormat == PixelFormatType.P8u_C3 || pixelFormat == PixelFormatType.P8u_C4)
                dstPixelFormat = PixelFormats.Gray8;
            else if (pixelFormat == PixelFormatType.P16u_C3 || pixelFormat == PixelFormatType.P16u_C4)
                dstPixelFormat = PixelFormats.Gray16;

            int width = srcBitmap.PixelWidth;
            int height = srcBitmap.PixelHeight;
            double dDpiX = srcBitmap.DpiX;
            double dDpiY = srcBitmap.DpiY;
            int srcStep = ((width * srcBitmap.Format.BitsPerPixel) + 31) / 32 * 4;
            int dstStep = ((width * dstPixelFormat.BitsPerPixel) + 31) / 32 * 4;

            WriteableBitmap rDstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, dstPixelFormat, null);
            WriteableBitmap gDstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, dstPixelFormat, null);
            WriteableBitmap bDstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, dstPixelFormat, null);
            WriteableBitmap aDstBitmap = null;  // alpha channel (storing gray channel on the alpha channel)
            if (pixelFormat == PixelFormatType.P8u_C4 || pixelFormat == PixelFormatType.P16u_C4)
            {
                aDstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, dstPixelFormat, null);
            }
            byte* pRDst = (byte*)rDstBitmap.BackBuffer.ToPointer();
            byte* pGDst = (byte*)gDstBitmap.BackBuffer.ToPointer();
            byte* pBDst = (byte*)bDstBitmap.BackBuffer.ToPointer();
            byte* pADst = null;
            if (pixelFormat == PixelFormatType.P8u_C4 || pixelFormat == PixelFormatType.P16u_C4)
            {
                pADst = (byte*)aDstBitmap.BackBuffer.ToPointer();
            }
            byte* pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();

            byte*[] pDst = null;
            if (pixelFormat == PixelFormatType.P8u_C4 || pixelFormat == PixelFormatType.P16u_C4)
            {
                // 4-channel image
                pDst = new byte*[4] { pRDst, pGDst, pBDst, pADst };
            }
            else
            {
                // 3-channel image
                pDst = new byte*[3] { pRDst, pGDst, pBDst };
            }

            IppiSize roiSize = new IppiSize(width, height);

            IppImaging.GetChannel(pSrc, srcStep, roiSize, pDst, dstStep, pixelFormat);

            WriteableBitmap[] dstBitmap = null;
            if (pixelFormat == PixelFormatType.P8u_C4 || pixelFormat == PixelFormatType.P16u_C4)
            {
                // 4-channel image
                dstBitmap = new WriteableBitmap[] { rDstBitmap, gDstBitmap, bDstBitmap, aDstBitmap };
            }
            else
            {
                // 3-channel image
                dstBitmap = new WriteableBitmap[] { rDstBitmap, gDstBitmap, bDstBitmap };
            }

            return dstBitmap;
        }

        public static unsafe WriteableBitmap SetChannel(WriteableBitmap srcBitmapR, WriteableBitmap srcBitmapG, WriteableBitmap srcBitmapB)
        {
            if (srcBitmapR == null && srcBitmapG == null && srcBitmapB == null)
                return null;

            int width = 0;
            int height = 0;
            int srcStep = 0;
            int dstStep = 0;
            int srcBitsPerPixel = 0;
            byte* pSrcR = null;
            byte* pSrcG = null;
            byte* pSrcB = null;
            WriteableBitmap dstBitmap = null;
            PixelFormatType srcIppPixelType = PixelFormatType.P16u_C1;  //Ipp pixel format type
            PixelFormat srcPixelFormat = PixelFormats.Gray16;           //Windows media (WPF) pixel format
            double dDpiX = 96;
            double dDpiY = 96;

            if (srcBitmapR != null)
            {
                // Reserve the back buffer for updates.
                //if (!srcBitmapR.IsFrozen)
                //    srcBitmapR.Lock();

                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmapR.Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source1 pixel format not supported");
                }

                srcPixelFormat = srcBitmapR.Format;

                width = srcBitmapR.PixelWidth;
                height = srcBitmapR.PixelHeight;
                dDpiX = srcBitmapR.DpiX;
                dDpiY = srcBitmapR.DpiY;
                srcBitsPerPixel = srcBitmapR.Format.BitsPerPixel;
                srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                pSrcR = (byte*)(void*)srcBitmapR.BackBuffer.ToPointer();
            }

            if (srcBitmapG != null)
            {
                // Reserve the back buffer for updates.
                //if (!srcBitmapG.IsFrozen)
                //    srcBitmapG.Lock();

                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmapG.Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source2 pixel format not supported");
                }

                srcPixelFormat = srcBitmapG.Format;

                width = srcBitmapG.PixelWidth;
                height = srcBitmapG.PixelHeight;
                dDpiX = srcBitmapG.DpiX;
                dDpiY = srcBitmapG.DpiY;
                srcBitsPerPixel = srcBitmapG.Format.BitsPerPixel;
                srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                pSrcG = (byte*)(void*)srcBitmapG.BackBuffer.ToPointer();
            }

            if (srcBitmapB != null)
            {
                // Reserve the back buffer for updates.
                //if (!srcBitmapB.IsFrozen)
                //    srcBitmapB.Lock();

                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmapB.Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source3 pixel format not supported");
                }

                srcPixelFormat = srcBitmapB.Format;

                width = srcBitmapB.PixelWidth;
                height = srcBitmapB.PixelHeight;
                dDpiX = srcBitmapB.DpiX;
                dDpiY = srcBitmapB.DpiY;
                srcBitsPerPixel = srcBitmapB.Format.BitsPerPixel;
                srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                pSrcB = (byte*)(void*)srcBitmapB.BackBuffer.ToPointer();
            }

            //Allocate memory for the destination bitmap
            if (srcIppPixelType == PixelFormatType.P8u_C1)
            {
                dstStep = ((width * PixelFormats.Bgr24.BitsPerPixel) + 31) / 32 * 4;
                dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Rgb24, null);
            }
            else
            {
                // Ipp ippiCopy_8u_P3C3R and ippiCopy_16u_P3C3R source images can't be null
                dstStep = ((width * PixelFormats.Rgb48.BitsPerPixel) + 31) / 32 * 4;
                dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Rgb48, null);
            }

            //
            //Ipp ippiCopy_8u_P3C3R and ippiCopy_16u_P3C3R source images can't be null
            // Create image buffer for the null image channel.
            //
            if (srcBitmapR == null)
            {
                srcBitmapR = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapR.Lock();   // Reserve the back buffer for updates.
                pSrcR = (byte*)(void*)srcBitmapR.BackBuffer.ToPointer();
            }
            if (srcBitmapG == null)
            {
                srcBitmapG = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapG.Lock();   // Reserve the back buffer for updates.
                pSrcG = (byte*)(void*)srcBitmapG.BackBuffer.ToPointer();
            }
            if (srcBitmapB == null)
            {
                srcBitmapB = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapB.Lock();   // Reserve the back buffer for updates.
                pSrcB = (byte*)(void*)srcBitmapB.BackBuffer.ToPointer();
            }

            byte* pDst = (byte*)dstBitmap.BackBuffer.ToPointer();
            IppiSize roiSize = new IppiSize(width, height);

            IppImaging.SetChannel(pSrcR, pSrcG, pSrcB, srcStep, roiSize, srcIppPixelType, pDst, dstStep);

            //if (!srcBitmapR.IsFrozen)
            //    srcBitmapR.Unlock();
            //if (!srcBitmapG.IsFrozen)
            //    srcBitmapG.Unlock();
            //if (!srcBitmapB.IsFrozen)
            //    srcBitmapB.Unlock();

            return dstBitmap;
        }

        public static unsafe WriteableBitmap SetChannel(WriteableBitmap srcBitmapR, WriteableBitmap srcBitmapG, WriteableBitmap srcBitmapB, WriteableBitmap srcBitmapK)
        {
            if (srcBitmapR == null && srcBitmapG == null &&
                srcBitmapB == null && srcBitmapK == null)
            {
                return null;
            }

            int width = 0;
            int height = 0;
            int srcStep = 0;
            int dstStep = 0;
            int srcBitsPerPixel = 0;
            byte* pSrcR = null;
            byte* pSrcG = null;
            byte* pSrcB = null;
            byte* pSrcK = null;
            WriteableBitmap dstBitmap = null;
            PixelFormatType srcIppPixelType = PixelFormatType.P16u_C1;  //Ipp pixel format type
            PixelFormat srcPixelFormat = PixelFormats.Gray16;           //Windows media (WPF) pixel format
            double dDpiX = 96;
            double dDpiY = 96;

            if (srcBitmapR != null)
            {
                // Reserve the back buffer for updates.
                //srcBitmapR.Lock();

                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmapR.Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source1 pixel format not supported");
                }

                srcPixelFormat = srcBitmapR.Format;

                width = srcBitmapR.PixelWidth;
                height = srcBitmapR.PixelHeight;
                srcBitsPerPixel = srcBitmapR.Format.BitsPerPixel;
                dDpiX = srcBitmapR.DpiX;
                dDpiY = srcBitmapR.DpiY;
                srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                pSrcR = (byte*)(void*)srcBitmapR.BackBuffer.ToPointer();
            }

            if (srcBitmapG != null)
            {
                // Reserve the back buffer for updates.
                //srcBitmapG.Lock();

                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmapG.Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source2 pixel format not supported");
                }

                srcPixelFormat = srcBitmapG.Format;

                width = srcBitmapG.PixelWidth;
                height = srcBitmapG.PixelHeight;
                dDpiX = srcBitmapG.DpiX;
                dDpiY = srcBitmapG.DpiY;
                srcBitsPerPixel = srcBitmapG.Format.BitsPerPixel;
                srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                pSrcG = (byte*)(void*)srcBitmapG.BackBuffer.ToPointer();
            }

            if (srcBitmapB != null)
            {
                // Reserve the back buffer for updates.
                //srcBitmapB.Lock();

                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmapB.Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source3 pixel format not supported");
                }

                srcPixelFormat = srcBitmapB.Format;

                width = srcBitmapB.PixelWidth;
                height = srcBitmapB.PixelHeight;
                dDpiX = srcBitmapB.DpiX;
                dDpiY = srcBitmapB.DpiY;
                srcBitsPerPixel = srcBitmapB.Format.BitsPerPixel;
                srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                pSrcB = (byte*)(void*)srcBitmapB.BackBuffer.ToPointer();
            }

            if (srcBitmapK != null)
            {
                // Reserve the back buffer for updates.
                //srcBitmapK.Lock();

                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmapK.Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source3 pixel format not supported");
                }

                srcPixelFormat = srcBitmapK.Format;

                width = srcBitmapK.PixelWidth;
                height = srcBitmapK.PixelHeight;
                dDpiX = srcBitmapK.DpiX;
                dDpiY = srcBitmapK.DpiY;
                srcBitsPerPixel = srcBitmapK.Format.BitsPerPixel;
                srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                pSrcK = (byte*)(void*)srcBitmapK.BackBuffer.ToPointer();
            }

            //Allocate memory for the destination bitmap
            if (srcIppPixelType == PixelFormatType.P8u_C1)
            {
                //dstStep = ((width * PixelFormats.Bgr24.BitsPerPixel) + 31) / 32 * 4;
                dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Bgra32, null);
                dstStep = dstBitmap.BackBufferStride;
            }
            else
            {
                //dstStep = ((width * PixelFormats.Rgba64.BitsPerPixel) + 31) / 32 * 4;
                dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Rgba64, null);
                dstStep = dstBitmap.BackBufferStride;
            }

            //
            //Ipp ippiCopy_8u_P3C3R and ippiCopy_16u_P3C3R source images can't be null
            //
            if (srcBitmapR == null)
            {
                srcBitmapR = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapR.Lock();   // Reserve the back buffer for updates.
                pSrcR = (byte*)(void*)srcBitmapR.BackBuffer.ToPointer();
            }
            if (srcBitmapG == null)
            {
                srcBitmapG = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapG.Lock();   // Reserve the back buffer for updates.
                pSrcG = (byte*)(void*)srcBitmapG.BackBuffer.ToPointer();
            }
            if (srcBitmapB == null)
            {
                srcBitmapB = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapB.Lock();   // Reserve the back buffer for updates.
                pSrcB = (byte*)(void*)srcBitmapB.BackBuffer.ToPointer();
            }
            if (srcBitmapK == null)
            {
                srcBitmapK = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapK.Lock();   // Reserve the back buffer for updates.
                pSrcK = (byte*)(void*)srcBitmapK.BackBuffer.ToPointer();
            }

            byte* pDst = (byte*)dstBitmap.BackBuffer.ToPointer();
            IppiSize roiSize = new IppiSize(width, height);

            byte*[] pSrcImages = { pSrcR, pSrcG, pSrcB, pSrcK };
            //Ipp set channel expects BGR color order
            IppImaging.SetChannel(pSrcImages, srcStep, roiSize, srcIppPixelType, pDst, dstStep);

            return dstBitmap;
        }

        /// <summary>
        /// Expect color order: Red/Green/Blue/Gray
        /// </summary>
        /// <param name="srcBitmaps"></param>
        /// <returns></returns>
        public static unsafe WriteableBitmap SetChannel(WriteableBitmap[] srcBitmaps)
        {
            if (srcBitmaps == null)
                return null;

            int width = 0;
            int height = 0;
            int srcStep = 0;
            int dstStep = 0;
            int srcBitsPerPixel = 0;
            byte* pSrcR = null;
            byte* pSrcG = null;
            byte* pSrcB = null;
            byte* pSrcK = null;
            WriteableBitmap dstBitmap = null;
            PixelFormatType srcIppPixelType = PixelFormatType.P16u_C1;  //Ipp pixel format type
            PixelFormat srcPixelFormat = PixelFormats.Gray16;           //Windows media (WPF) pixel format
            double dDpiX = 96;
            double dDpiY = 96;

            if (srcBitmaps[0] != null)  // red channel
            {
                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmaps[0].Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source1 pixel format not supported");
                }

                srcPixelFormat = srcBitmaps[0].Format;

                width = srcBitmaps[0].PixelWidth;
                height = srcBitmaps[0].PixelHeight;
                dDpiX = srcBitmaps[0].DpiX;
                dDpiY = srcBitmaps[0].DpiY;
                srcBitsPerPixel = srcBitmaps[0].Format.BitsPerPixel;
                //srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                srcStep = srcBitmaps[0].BackBufferStride;
                pSrcR = (byte*)(void*)srcBitmaps[0].BackBuffer.ToPointer();
            }

            if (srcBitmaps[1] != null)  // green channel
            {
                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmaps[1].Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source2 pixel format not supported");
                }

                srcPixelFormat = srcBitmaps[1].Format;

                width = srcBitmaps[1].PixelWidth;
                height = srcBitmaps[1].PixelHeight;
                dDpiX = srcBitmaps[1].DpiX;
                dDpiY = srcBitmaps[1].DpiY;
                srcBitsPerPixel = srcBitmaps[1].Format.BitsPerPixel;
                //srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                srcStep = srcBitmaps[1].BackBufferStride;
                pSrcG = (byte*)(void*)srcBitmaps[1].BackBuffer.ToPointer();
            }

            if (srcBitmaps[2] != null)  // blue channel
            {
                srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmaps[2].Format);

                if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                {
                    throw new Exception("SetChannel: source3 pixel format not supported");
                }

                srcPixelFormat = srcBitmaps[2].Format;

                width = srcBitmaps[2].PixelWidth;
                height = srcBitmaps[2].PixelHeight;
                dDpiX = srcBitmaps[2].DpiX;
                dDpiY = srcBitmaps[2].DpiY;
                srcBitsPerPixel = srcBitmaps[2].Format.BitsPerPixel;
                //srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                srcStep = srcBitmaps[2].BackBufferStride;
                pSrcB = (byte*)(void*)srcBitmaps[2].BackBuffer.ToPointer();
            }

            // 4-channel image
            if (srcBitmaps.Length > 3)
            {
                if (srcBitmaps[3] != null)  // gray channel
                {
                    srcIppPixelType = IppImaging.GetPixelFormatType(srcBitmaps[3].Format);

                    if (srcIppPixelType != PixelFormatType.P8u_C1 && srcIppPixelType != PixelFormatType.P16u_C1)
                    {
                        throw new Exception("SetChannel: source3 pixel format not supported");
                    }

                    srcPixelFormat = srcBitmaps[3].Format;

                    width = srcBitmaps[3].PixelWidth;
                    height = srcBitmaps[3].PixelHeight;
                    dDpiX = srcBitmaps[3].DpiX;
                    dDpiY = srcBitmaps[3].DpiY;
                    srcBitsPerPixel = srcBitmaps[3].Format.BitsPerPixel;
                    //srcStep = ((width * srcBitsPerPixel) + 31) / 32 * 4;
                    srcStep = srcBitmaps[3].BackBufferStride;
                    pSrcK = (byte*)(void*)srcBitmaps[3].BackBuffer.ToPointer();
                }
            }

            if (srcBitmaps.Length > 3)
            {
                // 4-channel image
                //
                // Allocate memory for the destination bitmap
                if (srcIppPixelType == PixelFormatType.P8u_C1)
                {
                    //dstStep = ((width * PixelFormats.Bgra32.BitsPerPixel) + 31) / 32 * 4;
                    dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Bgra32, null);
                    dstStep = dstBitmap.BackBufferStride;
                }
                else
                {
                    // Ipp ippiCopy_8u_P3C3R and ippiCopy_16u_P3C3R source images can't be null
                    //dstStep = ((width * PixelFormats.Rgba64.BitsPerPixel) + 31) / 32 * 4;
                    dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Rgba64, null);
                    dstStep = dstBitmap.BackBufferStride;
                }
            }
            else
            {
                // 3-channel image
                //
                // Allocate memory for the destination bitmap
                if (srcIppPixelType == PixelFormatType.P8u_C1)
                {
                    //dstStep = ((width * PixelFormats.Rgb24.BitsPerPixel) + 31) / 32 * 4;
                    dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Rgb24, null);
                    dstStep = dstBitmap.BackBufferStride;
                }
                else
                {
                    // Ipp ippiCopy_8u_P3C3R and ippiCopy_16u_P3C3R source images can't be null
                    //dstStep = ((width * PixelFormats.Rgb48.BitsPerPixel) + 31) / 32 * 4;
                    dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Rgb48, null);
                    dstStep = dstBitmap.BackBufferStride;
                }
            }

            //
            //Ipp ippiCopy_8u_P3C3R and ippiCopy_16u_P3C3R source images can't be null
            //Ipp ippiCopy_8u_P4C4R and ippiCopy_16u_P4C4R source images can't be null
            //
            // Create image buffer for the null image channel.
            //
            if (srcBitmaps[0] == null)   // red channel
            {
                srcBitmaps[0] = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapR.Lock();   // Reserve the back buffer for updates.
                pSrcR = (byte*)(void*)srcBitmaps[0].BackBuffer.ToPointer();
            }
            if (srcBitmaps[1] == null)     // green channel
            {
                srcBitmaps[1] = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapG.Lock();   // Reserve the back buffer for updates.
                pSrcG = (byte*)(void*)srcBitmaps[1].BackBuffer.ToPointer();
            }
            if (srcBitmaps[2] == null)     // blue channel
            {
                srcBitmaps[2] = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                //srcBitmapB.Lock();   // Reserve the back buffer for updates.
                pSrcB = (byte*)(void*)srcBitmaps[2].BackBuffer.ToPointer();
            }
            // 4-channel image
            if (srcBitmaps.Length == 4)
            {
                if (srcBitmaps[3] == null)     // gray channel
                {
                    srcBitmaps[3] = new WriteableBitmap(width, height, dDpiX, dDpiY, srcPixelFormat, null);
                    //srcBitmapB.Lock();   // Reserve the back buffer for updates.
                    pSrcK = (byte*)(void*)srcBitmaps[3].BackBuffer.ToPointer();
                }
            }

            byte* pDst = (byte*)dstBitmap.BackBuffer.ToPointer();
            IppiSize roiSize = new IppiSize(width, height);

            byte*[] pSrcImages = null;
            if (srcBitmaps.Length > 3)
                pSrcImages = new byte*[] { pSrcR, pSrcG, pSrcB, pSrcK }; // merge 4-channel
            else
                pSrcImages = new byte*[] { pSrcR, pSrcG, pSrcB };        // merge 3-channel

            IppImaging.SetChannel(pSrcImages, srcStep, roiSize, srcIppPixelType, pDst, dstStep);

            return dstBitmap;
        }

        public static unsafe BitmapSource Crop(BitmapSource srcBitmap, Rect roiRect)
        {
            if (srcBitmap == null)
                return null;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            WriteableBitmap srcImage = new WriteableBitmap(srcBitmap);  //BitmapSource to WriteableBitmap to access the BackBuffer
            double dDpiX = srcBitmap.DpiX;
            double dDpiY = srcBitmap.DpiY;
            WriteableBitmap dstImage = new WriteableBitmap((int)roiRect.Width, (int)roiRect.Height, dDpiX, dDpiY, srcImage.Format, null);

            int srcStep = srcImage.BackBufferStride;
            int dstStep = dstImage.BackBufferStride;

            byte* pSrcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* pDstData = (byte*)dstImage.BackBuffer.ToPointer();

            IppImaging.Crop(pSrcData, srcStep, pDstData, dstStep, roiRect, pixelFormat);

            // Forces a garbage collection
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            // Force garbage collection.
            //GC.Collect();
            // Wait for all finalizers to complete before continuing.
            //GC.WaitForPendingFinalizers();

            return (BitmapSource)dstImage;
        }
        public static unsafe WriteableBitmap Flip(WriteableBitmap srcBitmap, IppiAxis flip)
        {
            if (srcBitmap == null)
                return null;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int width = srcBitmap.PixelWidth;
            int height = srcBitmap.PixelHeight;
            int srcStep = srcBitmap.BackBufferStride;
            int dstStep = srcStep;
            PixelFormat format = srcBitmap.Format;
            BitmapPalette palette = srcBitmap.Palette;
            double dDpiX = srcBitmap.DpiX;
            double dDpiY = srcBitmap.DpiY;
            WriteableBitmap dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, format, palette);
            byte* pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();
            byte* pDst = (byte*)dstBitmap.BackBuffer.ToPointer();
            IppiSize roiSize = new IppiSize(width, height);

            IppImaging.Flip(pSrc, srcStep, pixelFormat, pDst, dstStep, roiSize, flip);

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return dstBitmap;
        }
        public static WriteableBitmap WpfRotate(WriteableBitmap srcBitmap, double angle)
        {
            if (srcBitmap == null)
                return null;

            RotateTransform transform = new RotateTransform(angle);
            TransformedBitmap tb = new TransformedBitmap();
            tb.BeginInit();
            tb.Source = srcBitmap;
            tb.Transform = transform;
            tb.EndInit();
            return new WriteableBitmap((BitmapSource)tb);
        }

        public static WriteableBitmap WpfFlip(WriteableBitmap srcBitmap, FlipAxis flip)
        {
            if (srcBitmap == null)
                return null;

            double scaleX = 1;
            double scaleY = 1;
            if (flip == FlipAxis.Horizontal)
            {
                scaleX = -1;
                scaleY = 1;
            }
            else if (flip == FlipAxis.Vertical)
            {
                scaleX = 1;
                scaleY = -1;
            }

            //TransformedBitmap tb = new TransformedBitmap();
            //tb.BeginInit();
            //tb.Source = srcBitmap;
            //RotateTransform transform = new RotateTransform(angle);
            //tb.Transform = transform;
            //tb.EndInit();
            //return new WriteableBitmap((BitmapSource)tb);

            System.Windows.Media.Transform tr = new System.Windows.Media.ScaleTransform(scaleX, scaleY);
            TransformedBitmap transformedBmp = new TransformedBitmap();
            transformedBmp.BeginInit();
            transformedBmp.Source = srcBitmap;
            transformedBmp.Transform = tr;
            transformedBmp.EndInit();
            return new WriteableBitmap((BitmapSource)transformedBmp);
        }

        public static unsafe WriteableBitmap Resize(WriteableBitmap srcBitmap, Size dstSize)
        {
            if (srcBitmap == null)
            {
                throw new Exception("Source image cannot be null");
            }

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int srcWidth = srcBitmap.PixelWidth;
            int srcHeight = srcBitmap.PixelHeight;
            int srcStep = srcBitmap.BackBufferStride;

            int dstWidth = (int)dstSize.Width;
            int dstHeight = (int)dstSize.Height;
            double dDpiX = srcBitmap.DpiX;
            double dDpiY = srcBitmap.DpiY;

            WriteableBitmap dstBitmap = new WriteableBitmap(dstWidth, dstHeight, dDpiX, dDpiY, srcBitmap.Format, srcBitmap.Palette);
            int dstStep = dstBitmap.BackBufferStride;

            IppiSize ippSrcSize = new IppiSize(srcWidth, srcHeight);
            IppiSize ippDstSize = new IppiSize(dstWidth, dstHeight);

            byte* pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();
            byte* pDst = (byte*)dstBitmap.BackBuffer.ToPointer();

            IppImaging.Resize(pSrc, ippSrcSize, srcStep, pixelFormat, pDst, dstStep, ippDstSize);

            return dstBitmap;
        }

        public static unsafe WriteableBitmap Resize(WriteableBitmap srcBitmap, Size dstSize, double dDpiX, double dDpiY)
        {
            if (srcBitmap == null)
            {
                throw new Exception("Source image cannot be null");
            }

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int srcWidth = srcBitmap.PixelWidth;
            int srcHeight = srcBitmap.PixelHeight;
            int srcStep = srcBitmap.BackBufferStride;

            int dstWidth = (int)dstSize.Width;
            int dstHeight = (int)dstSize.Height;
            if (dDpiX < 96) { dDpiX = 96.0; }
            if (dDpiY < 96) { dDpiY = 96.0; }
            WriteableBitmap dstBitmap = new WriteableBitmap(dstWidth, dstHeight, dDpiX, dDpiY, srcBitmap.Format, srcBitmap.Palette);
            int dstStep = dstBitmap.BackBufferStride;

            IppiSize ippSrcSize = new IppiSize(srcWidth, srcHeight);
            IppiSize ippDstSize = new IppiSize(dstWidth, dstHeight);

            byte* pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();
            byte* pDst = (byte*)dstBitmap.BackBuffer.ToPointer();

            IppImaging.Resize(pSrc, ippSrcSize, srcStep, pixelFormat, pDst, dstStep, ippDstSize);

            return dstBitmap;
        }

        //Filters an image using a median filter
        public static unsafe WriteableBitmap MedianFilter(WriteableBitmap srcBitmap)
        {
            if (srcBitmap == null)
            {
                throw new Exception("Source image cannot be null");
            }

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int width = srcBitmap.PixelWidth;
            int height = srcBitmap.PixelHeight;
            int srcStep = srcBitmap.BackBufferStride;
            double dDpiX = srcBitmap.DpiX;
            double dDpiY = srcBitmap.DpiY;
            WriteableBitmap dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, srcBitmap.Format, srcBitmap.Palette);
            int dstStep = dstBitmap.BackBufferStride;
            IppiSize roiSize = new IppiSize(width, height);

            byte* pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();
            byte* pDst = (byte*)dstBitmap.BackBuffer.ToPointer();

            //3x3 kernel median filter
            IppImaging.MedianFilter(pSrc, srcStep, roiSize, pixelFormat, pDst, dstStep);

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return dstBitmap;
        }

        //coi: Channel of interest (for color images only); can be 1, 2, or 3.
        public static unsafe void MeanStdDev(WriteableBitmap srcBitmap, ref double mean, ref double stdDev, int coi = 1)
        {
            if (srcBitmap == null) { return; }

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            mean = 0.0;
            stdDev = 0.0;

            int width = srcBitmap.PixelWidth;
            int height = srcBitmap.PixelHeight;

            //int srcStep = ((srcBitmap.PixelWidth * srcBitmap.Format.BitsPerPixel) + 31) / 32 * 4;
            int srcStep = srcBitmap.BackBufferStride;
            byte* pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();
            IppiSize roiSize = new IppiSize(width, height);
            //double* pMean;
            //double* pStdDev;
            //fixed (double* p = &mean) pMean = p;
            //fixed (double* p = &stdDev) pStdDev = p;

            fixed (double* pMean = &mean, pStdDev = &stdDev)
            {
                IppImaging.MeanStdDev(pSrc, srcStep, roiSize, pixelFormat, coi, pMean, pStdDev);
            }
        }


        #region === Image Scaling ===

        internal static byte[] CreateLUT(int bpp, double dMin, double dMax, double dGamma, bool bIsInvert)
        {
            int maxInput = 1 << bpp;

            byte[] lut = new byte[maxInput];

            for (int i = 0; i < maxInput; ++i)
            {
                if (i <= dMin)
                {
                    lut[i] = (byte)((bIsInvert) ? 255 : 0);
                }
                else if (i >= dMax)
                {
                    lut[i] = (byte)((bIsInvert) ? 0 : 255);
                }
                else
                {
                    if (bIsInvert)
                    {
                        lut[i] = (byte)(255 - (255 * Math.Pow(((double)i - dMin) / (dMax - dMin), (double)1.0 / dGamma)));
                    }
                    else
                    {
                        lut[i] = (byte)(255 * Math.Pow(((double)i - dMin) / (dMax - dMin), (double)1.0 / dGamma));
                    }
                    if (lut[i] > 255)
                    {
                        lut[i] = 255;
                    }
                    else if (lut[i] < 0)
                    {
                        lut[i] = 0;
                    }
                }
            }
            return lut;
        }
        /// <summary>
        /// Create a LUT that maps a possibly the input range [dMin, dMax] to bytes [0, maxPalEntries].
        /// Usually maxPalEntries is 255, however you can use a smaller range.
        /// This way you lose some shades of gray but you have slot avaialble to encode other information 
        /// in the dislay palette.
        /// </summary>
        /// <param name="bpp"></param>
        /// <param name="dMin"></param>
        /// <param name="dMax"></param>
        /// <param name="dGamma"></param>
        /// <param name="bIsInvert"></param>
        /// <param name="maxPalEntries"></param>
        /// <returns></returns>
        internal static byte[] CreateLUT(int bpp, double dMin, double dMax, double dGamma, bool bIsInvert,
            byte maxPalEntries)
        {
            int maxInput = 1 << bpp;

            byte[] lut = new byte[maxInput];

            for (int i = 0; i < maxInput; ++i)
            {
                if (i <= dMin)
                {
                    lut[i] = (byte)((bIsInvert) ? maxPalEntries : 0);
                }
                else if (i >= dMax)
                {
                    lut[i] = (byte)((bIsInvert) ? 0 : maxPalEntries);
                }
                else
                {
                    if (bIsInvert)
                    {
                        lut[i] = (byte)(maxPalEntries -
                                         (maxPalEntries * Math.Pow(((double)i - dMin) / (dMax - dMin),
                                              (double)1.0 / dGamma)));
                    }
                    else
                    {
                        lut[i] = (byte)(maxPalEntries *
                                         Math.Pow(((double)i - dMin) / (dMax - dMin), (double)1.0 / dGamma));
                    }
                    if (lut[i] > maxPalEntries)
                    {
                        lut[i] = maxPalEntries;
                    }
                }
            }
            return lut;
        }


        /*public static unsafe WriteableBitmap Scale(WriteableBitmap srcBitmap, int iMin, int iMax, double dGamma,
                                                   bool bIsInverted, bool bIsSaturation, int iSaturationValue, int iColorGradation)
        {
            if (srcBitmap == null)
            {
                return null;
            }

            PixelFormat srcPixelFormat = srcBitmap.Format;
            if (srcPixelFormat != PixelFormats.Gray8 && srcPixelFormat != PixelFormats.Indexed8 &&
                srcPixelFormat != PixelFormats.Bgr24 && srcPixelFormat != PixelFormats.Rgb24 &&
                srcPixelFormat != PixelFormats.Gray16 && srcPixelFormat != PixelFormats.Rgb48)
            {
                return srcBitmap;   // returns the original image (currently does not support contrasting image type other than 16 and 48-bit image)
            }

            int iWidth = srcBitmap.PixelWidth;
            int iHeight = srcBitmap.PixelHeight;
            WriteableBitmap dstBitmap = null;

            if (srcPixelFormat.BitsPerPixel == 8 || srcPixelFormat.BitsPerPixel == 16)
            {
                dstBitmap = Scale_16u8u_C1(srcBitmap, iMin, iMax, dGamma, bIsSaturation, iSaturationValue, bIsInverted);
            }
            else if (srcPixelFormat.BitsPerPixel == 24 || srcPixelFormat.BitsPerPixel == 48)
            {
                dstBitmap = Scale_16u8u_C3(srcBitmap, iMin, iMax, dGamma, bIsSaturation, iSaturationValue, bIsInverted, iColorGradation);
            }

            return dstBitmap;
        }*/

        /*public static unsafe void Scale(ref WriteableBitmap srcImage,
                                        ref WriteableBitmap dstImage,
                                        ImageInfo imageInfo, int nColorGradation,
                                        bool bIsMergeChannels = false)
        {
            if (srcImage == null || dstImage == null) { return; }

            PixelFormatType srcPixelFormat = IppImaging.GetPixelFormatType(srcImage.Format);
            if (!IsSupportedPixelFormat(srcPixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            if (srcPixelFormat == PixelFormatType.P8u_C1 ||
                srcPixelFormat == PixelFormatType.P16u_C1)
            {
                //Scale_16u8u_C1(ref srcBitmap, ref dstBitmap, iMin, iMax, dGamma, bIsSaturation, iSaturationValue, bIsInverted, bIsIndexedDisplay);
            }
            else if (srcPixelFormat == PixelFormatType.P8u_C3 ||
                     srcPixelFormat == PixelFormatType.P16u_C3)
            {
                ImageProcessing.Scale_16u8u_C3C3(ref srcImage, ref dstImage, imageInfo, nColorGradation);
            }
            else if (srcPixelFormat == PixelFormatType.P8u_C4 ||
                     srcPixelFormat == PixelFormatType.P16u_C4)
            {
                ImageProcessing.Scale_16u8u_C4C3(ref srcImage, ref dstImage, imageInfo, nColorGradation);
            }
        }*/


        // Scale 16-bit to 8-bit bitmap
        // Handle inversion but not saturation (also see: Scale_16u8u_C1).
        public static unsafe WriteableBitmap Scale_C1(WriteableBitmap srcBitmap, int iMin, int iMax, double dGamma, bool bIsInverted)
        {
            if (srcBitmap == null)
            {
                return null;
            }

            PixelFormat srcPixelFormat = srcBitmap.Format;
            PixelFormatType pixelType = IppImaging.GetPixelFormatType(srcPixelFormat);
            if (pixelType != PixelFormatType.P8u_C1 && pixelType != PixelFormatType.P16u_C1)
            {
                return srcBitmap;
            }

            int iWidth = srcBitmap.PixelWidth;
            int iHeight = srcBitmap.PixelHeight;
            int iBitsPerPixel = srcBitmap.Format.BitsPerPixel;
            int iSrcStep = srcBitmap.BackBufferStride;

            WriteableBitmap dstBitmap = new WriteableBitmap(iWidth, iHeight, 96, 96, PixelFormats.Gray8, null);
            int iDstStep = dstBitmap.BackBufferStride;

            //srcBitmap.Lock();
            //dstBitmap.Lock();

            byte* srcData = (byte*)(void*)srcBitmap.BackBuffer.ToPointer();
            byte* dstData = (byte*)(void*)dstBitmap.BackBuffer.ToPointer();

            byte[] lut = CreateLUT(iBitsPerPixel, iMin, iMax, dGamma, bIsInverted);

            fixed (byte* pLutData = lut)
            {
                byte* pLut = pLutData;

                if (pixelType == PixelFormatType.P8u_C1)
                {
                    byte* pSrcData = null;
                    byte* pDstData = null;

                    for (int i = 0; i < iHeight; i++)
                    {
                        pSrcData = (byte*)(srcData + (i * iSrcStep));
                        pDstData = (byte*)(dstData + (i * iDstStep));

                        for (int j = 0; j < iWidth; j++)
                        {
                            *pDstData++ = *(pLut + *pSrcData++);
                        }
                    }
                }
                else if (pixelType == PixelFormatType.P16u_C1)
                {
                    ushort* pSrcData = null;
                    byte* pDstData = null;

                    for (int i = 0; i < iHeight; i++)
                    {
                        pSrcData = (ushort*)(srcData + (i * iSrcStep));
                        pDstData = (byte*)(dstData + (i * iDstStep));

                        for (int j = 0; j < iWidth; j++)
                        {
                            *pDstData++ = *(pLut + *pSrcData++);
                        }
                    }
                }
            }

            //srcBitmap.Unlock();
            //dstBitmap.Unlock();

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return dstBitmap;
        }

        // Scale 16-bit to 24-bit bitmap.
        // Handle saturation and inversion.
        internal static unsafe WriteableBitmap Scale_16u8u_C1(WriteableBitmap srcImage, int iMin, int iMax, double dGamma,
                                                              bool bIsSaturation, int iSaturatonValue, bool bIsInverted)
        {
            if (srcImage.Format == PixelFormats.Gray16 || srcImage.Format == PixelFormats.Gray8)
            {
                // Use an indexed bitmpa format.
                return Scale_16u8u_C1_Indexed(srcImage, iMin, iMax, dGamma, bIsSaturation, iSaturatonValue, bIsInverted);
            }

            byte* pSrcData = null;
            byte* pDstData = null;
            //byte* pSatData = null;
            int tmp = 0;
            ushort tmpValue;
            int iWidth = srcImage.PixelWidth;
            int iHeight = srcImage.PixelHeight;
            int bitsPerPixel = srcImage.Format.BitsPerPixel;
            int iSrcStep = srcImage.BackBufferStride;

            PixelFormat pixelFormat = srcImage.Format;
            PixelFormat dstPixelFormat;
            if (pixelFormat == PixelFormats.Gray16 || pixelFormat == PixelFormats.Rgb48 ||
                pixelFormat == PixelFormats.Bgr24 || pixelFormat == PixelFormats.Rgb24 ||
                pixelFormat == PixelFormats.Gray8 || pixelFormat == PixelFormats.Indexed8)
            {
                dstPixelFormat = PixelFormats.Rgb24;
            }
            else
            {
                return null;
            }

            WriteableBitmap dstImage = new WriteableBitmap(iWidth, iHeight, 96, 96, dstPixelFormat, null);
            int iDstStep = dstImage.BackBufferStride;

            byte* srcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();
            //byte* satData = (byte*)srcImage.BackBuffer.ToPointer();

            byte[] lut = CreateLUT(bitsPerPixel, iMin, iMax, dGamma, bIsInverted);

            for (int i = 0; i < iHeight; i++)
            {
                pSrcData = srcData + i * iSrcStep;
                pDstData = dstData + i * iDstStep;
                //pSrcData = (byte*)srcImage.BackBuffer.ToPointer() + i * iSrcStep;
                //pDstData = (byte*)dstImage.BackBuffer.ToPointer() + i * iDstStep;

                for (int j = 0; j < iWidth; ++j)
                {
                    tmp = j * 3;

                    if (bitsPerPixel == 8)
                    {
                        tmpValue = *(pSrcData + j);
                    }
                    else
                    {
                        tmpValue = *(((ushort*)pSrcData) + j);
                    }

                    if (lut[tmpValue] > 255)
                    {
                        *(pDstData + tmp) = 255;
                        *(pDstData + tmp + 1) = 255;
                        *(pDstData + tmp + 2) = 255;
                    }
                    else if (lut[tmpValue] < 0)
                    {
                        *(pDstData + tmp) = 0;
                        *(pDstData + tmp + 1) = 0;
                        *(pDstData + tmp + 2) = 0;
                    }
                    else
                    {
                        *(pDstData + tmp) = lut[tmpValue];
                        *(pDstData + tmp + 1) = lut[tmpValue];
                        *(pDstData + tmp + 2) = lut[tmpValue];
                    }

                    if (bIsSaturation)
                    {
                        //pSatData = satData + i * iSrcStep;
                        if (bitsPerPixel == 8)
                        {
                            if (*(pSrcData + j) > iSaturatonValue)
                            {
                                *(pDstData + tmp) = 255;
                                *(pDstData + tmp + 1) = 0;
                                *(pDstData + tmp + 2) = 0;
                            }
                        }
                        else
                        {
                            if (*(((ushort*)pSrcData) + j) > iSaturatonValue)
                            {
                                *(pDstData + tmp) = 255;
                                *(pDstData + tmp + 1) = 0;
                                *(pDstData + tmp + 2) = 0;
                            }
                        }
                    }
                } // for width
            } //for height

            return dstImage;
        }

        /// <summary>
        /// Create an Indexed8 grayscale bitmap. If bIsSaturation is true we will only get 254 shades of gray, instead of 255.
        /// </summary>
        /// <param name="srcImage"></param>
        /// <param name="iMin"></param>
        /// <param name="iMax"></param>
        /// <param name="dGamma"></param>
        /// <param name="bIsSaturation"></param>
        /// <param name="iSaturatonValue"></param>
        /// <param name="bIsInverted"></param>
        /// <returns></returns>
        internal static unsafe WriteableBitmap Scale_16u8u_C1_Indexed(WriteableBitmap srcImage,
                                                                      int iMin, int iMax, double dGamma,
                                                                      bool bIsSaturation, int iSaturatonValue, bool bIsInverted)
        {
            int iWidth = srcImage.PixelWidth;
            int iHeight = srcImage.PixelHeight;
            int bitsPerPixel = srcImage.Format.BitsPerPixel;
            int iSrcStep = srcImage.BackBufferStride;

            PixelFormat pixelFormat = srcImage.Format;

            PixelFormat dstPixelFormat;
            if (pixelFormat == PixelFormats.Gray16 || pixelFormat == PixelFormats.Gray8)
            {
                dstPixelFormat = PixelFormats.Indexed8;
            }
            else
            {
                return null;
            }

            BitmapPalette pal = new BitmapPalette(GetColorTableIndexed(bIsSaturation));

            WriteableBitmap dstImage = new WriteableBitmap(iWidth, iHeight, 96, 96, dstPixelFormat, pal);
            int iDstStep = dstImage.BackBufferStride;

            byte* srcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();

            // If we want to display saturation we need to map to [0,254] so that index 255 is available for saturation.
            byte[] lut = CreateLUT(bitsPerPixel, iMin, iMax, dGamma, bIsInverted,
                (byte)(bIsSaturation ? 254 : 255));

            for (int i = 0; i < iHeight; i++)
            {
                var pSrcData = srcData + i * iSrcStep;
                var pDstData = dstData + i * iDstStep;

                for (int j = 0; j < iWidth; ++j)
                {
                    ushort tmpValue;
                    if (bitsPerPixel == 8)
                    {
                        tmpValue = *(pSrcData + j);
                    }
                    else
                    {
                        tmpValue = *(((ushort*)pSrcData) + j);
                    }

                    if (bIsSaturation && tmpValue > iSaturatonValue)
                        *(pDstData + j) = 255;
                    else
                        *(pDstData + j) = lut[tmpValue];
                } // for width
            } //for height

            return dstImage;
        }

        private static IList<Color> _grayColors;
        private static IList<Color> _grayColorsWithSaturation;
        public static IList<Color> GetColorTableIndexed(bool bIsSaturation)
        {
            if (bIsSaturation)
            {
                if (_grayColorsWithSaturation == null)
                {
                    _grayColorsWithSaturation = new List<Color>(256);
                    for (int i = 0; i < 255; i++)
                        _grayColorsWithSaturation.Add(new Color() { A = 255, B = (byte)i, G = (byte)i, R = (byte)i });

                    // The last entry [255] is reserved for a special color.
                    _grayColorsWithSaturation.Add(new Color() { A = 255, B = (byte)0, G = (byte)0, R = (byte)255 });
                }
                return (_grayColorsWithSaturation);
            }
            else
            {
                if (_grayColors == null)
                {
                    _grayColors = new List<Color>(256);
                    for (int i = 0; i < 256; i++)
                        _grayColors.Add(new Color() { A = 255, B = (byte)i, G = (byte)i, R = (byte)i });
                }
                return (_grayColors);
            }
        }

        internal static unsafe WriteableBitmap Scale_16u8u_C3(WriteableBitmap srcImage,
                                                              int iMin, int iMax, double dGamma,
                                                              bool bIsSaturation, int iSaturatonValue, bool bIsInverted, int iColorGradation)
        {
            if (srcImage == null) { return null; }

            PixelFormatType pixelType = IppImaging.GetPixelFormatType(srcImage.Format);

            if (pixelType != PixelFormatType.P8u_C1 && pixelType != PixelFormatType.P8u_C3 &&
                pixelType != PixelFormatType.P16u_C1 && pixelType != PixelFormatType.P16u_C3)
            {
                throw new Exception("Image type currently not supported");
            }

            PixelFormat srcPixelFormat = srcImage.Format;
            PixelFormat dstPixelFormat;
            if (srcPixelFormat == PixelFormats.Gray16 || srcPixelFormat == PixelFormats.Rgb48 ||
                srcPixelFormat == PixelFormats.Bgr24 || srcPixelFormat == PixelFormats.Rgb24 ||
                srcPixelFormat == PixelFormats.Gray8 || srcPixelFormat == PixelFormats.Indexed8)
            {
                dstPixelFormat = PixelFormats.Rgb24;
            }
            else
            {
                return null;
            }

            int iWidth = srcImage.PixelWidth;
            int iHeight = srcImage.PixelHeight;
            WriteableBitmap dstImage = new WriteableBitmap(iWidth, iHeight, 96, 96, dstPixelFormat, null);

            //
            //Process each image channel on its own thread
            //

            threadProcChan1Event.Set();
            threadProcChan2Event.Set();
            threadProcChan3Event.Set();

            int bitsPerPixel = srcImage.Format.BitsPerPixel;
            int iSrcStep = srcImage.BackBufferStride;
            int iDstStep = dstImage.BackBufferStride;
            //int iPixelType = GetPixelType(srcImage.Format);

            byte[] lut = CreateLUT(bitsPerPixel, iMin, iMax, dGamma, bIsInverted);

            byte* pSrcImageData = null;
            byte* pDstImageData = null;
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                // lock bitmap in UI thread
                srcImage.Lock();
                dstImage.Lock();

                pSrcImageData = (byte*)srcImage.BackBuffer.ToPointer();
                pDstImageData = (byte*)dstImage.BackBuffer.ToPointer();
            });


            Thread threadChannel1 = new Thread(() => ThreadProcChannel1(pSrcImageData, iSrcStep, pDstImageData, iDstStep,
                                                                        lut, iWidth, iHeight, pixelType,
                                                                        bIsSaturation, iSaturatonValue, iColorGradation));
            Thread threadChannel2 = new Thread(() => ThreadProcChannel2(pSrcImageData, iSrcStep, pDstImageData, iDstStep,
                                                                        lut, iWidth, iHeight, pixelType,
                                                                        bIsSaturation, iSaturatonValue, iColorGradation));
            Thread threadChannel3 = new Thread(() => ThreadProcChannel3(pSrcImageData, iSrcStep, pDstImageData, iDstStep,
                                                                        lut, iWidth, iHeight, pixelType,
                                                                        bIsSaturation, iSaturatonValue, iColorGradation));

            threadChannel1.Start();
            threadChannel2.Start();
            threadChannel3.Start();

            //block main thread until all other threads have ran to completion.
            threadChannel1.Join();
            threadChannel2.Join();
            threadChannel3.Join();

            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                //UI thread does post update operations
                srcImage.Unlock();
                dstImage.Unlock();
            });

            return dstImage;
        }
        internal static unsafe void Scale_16u8u_C1(ref WriteableBitmap srcImage, ref WriteableBitmap dstImage,
                                                   int iMin, int iMax, double dGamma,
                                                   bool bIsSaturation, int iSaturatonValue, bool bIsInverted, bool bIsIndexedDisplay)
        {
            if (bIsIndexedDisplay)
            {
                if (srcImage.Format == PixelFormats.Gray16 || srcImage.Format == PixelFormats.Gray8)
                {
                    // Use an indexed bitmpa format.
                    Scale_16u8u_C1_Indexed(ref srcImage, ref dstImage, iMin, iMax, dGamma, bIsSaturation, iSaturatonValue, bIsInverted);
                }
            }

            byte* pSrcData = null;
            byte* pDstData = null;
            //byte* pSatData = null;
            int tmp = 0;
            ushort tmpValue;
            int nWidth = srcImage.PixelWidth;
            int nHeight = srcImage.PixelHeight;
            int nBitsPerPixel = srcImage.Format.BitsPerPixel;
            int nSrcStep = srcImage.BackBufferStride;

            if (dstImage == null || dstImage.Format != PixelFormats.Rgb24)
            {
                dstImage = new WriteableBitmap(nWidth, nHeight, 96, 96, PixelFormats.Rgb24, null);
            }
            int nDstStep = dstImage.BackBufferStride;

            byte* srcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();
            //byte* satData = (byte*)srcImage.BackBuffer.ToPointer();

            byte[] lut = CreateLUT(nBitsPerPixel, iMin, iMax, dGamma, bIsInverted);

            for (int i = 0; i < nHeight; i++)
            {
                pSrcData = srcData + i * nSrcStep;
                pDstData = dstData + i * nDstStep;
                //pSrcData = (byte*)srcImage.BackBuffer.ToPointer() + i * iSrcStep;
                //pDstData = (byte*)dstImage.BackBuffer.ToPointer() + i * iDstStep;

                for (int j = 0; j < nWidth; ++j)
                {
                    tmp = j * 3;

                    if (nBitsPerPixel == 8)
                    {
                        tmpValue = *(pSrcData + j);
                    }
                    else
                    {
                        tmpValue = *(((ushort*)pSrcData) + j);
                    }

                    if (lut[tmpValue] > 255)
                    {
                        *(pDstData + tmp) = 255;
                        *(pDstData + tmp + 1) = 255;
                        *(pDstData + tmp + 2) = 255;
                    }
                    else if (lut[tmpValue] < 0)
                    {
                        *(pDstData + tmp) = 0;
                        *(pDstData + tmp + 1) = 0;
                        *(pDstData + tmp + 2) = 0;
                    }
                    else
                    {
                        *(pDstData + tmp) = lut[tmpValue];
                        *(pDstData + tmp + 1) = lut[tmpValue];
                        *(pDstData + tmp + 2) = lut[tmpValue];
                    }

                    if (bIsSaturation)
                    {
                        //pSatData = satData + i * iSrcStep;
                        if (nBitsPerPixel == 8)
                        {
                            if (*(pSrcData + j) > iSaturatonValue)
                            {
                                *(pDstData + tmp) = 255;
                                *(pDstData + tmp + 1) = 0;
                                *(pDstData + tmp + 2) = 0;
                            }
                        }
                        else
                        {
                            if (*(((ushort*)pSrcData) + j) > iSaturatonValue)
                            {
                                *(pDstData + tmp) = 255;
                                *(pDstData + tmp + 1) = 0;
                                *(pDstData + tmp + 2) = 0;
                            }
                        }
                    }
                } // for width
            } //for height
        }

        /// <summary>
        /// Create an Indexed8 grayscale bitmap. If bIsSaturation is true we will only get 254 shades of gray, instead of 255.
        /// </summary>
        /// <param name="srcImage"></param>
        /// <param name="nMin"></param>
        /// <param name="nMax"></param>
        /// <param name="dGamma"></param>
        /// <param name="bIsSaturation"></param>
        /// <param name="iSaturatonValue"></param>
        /// <param name="bIsInverted"></param>
        /// <returns></returns>
        internal static unsafe void Scale_16u8u_C1_Indexed(ref WriteableBitmap srcImage, ref WriteableBitmap dstImage,
                                                           int nMin, int nMax, double dGamma,
                                                           bool bIsSaturation, int iSaturatonValue, bool bIsInverted)
        {
            int nWidth = srcImage.PixelWidth;
            int nHeight = srcImage.PixelHeight;
            int nBitsPerPixel = srcImage.Format.BitsPerPixel;
            int nSrcStep = srcImage.BackBufferStride;

            if (dstImage == null || dstImage.Format != PixelFormats.Indexed8)
            {
                PixelFormat srcPixelFormat = srcImage.Format;
                PixelFormat dstPixelFormat;
                if (srcPixelFormat == PixelFormats.Gray16 || srcPixelFormat == PixelFormats.Gray8)
                {
                    dstPixelFormat = PixelFormats.Indexed8;
                }
                else
                {
                    return;
                }
                BitmapPalette pal = new BitmapPalette(GetColorTableIndexed(bIsSaturation));
                dstImage = new WriteableBitmap(nWidth, nHeight, 96, 96, dstPixelFormat, pal);
                dstImage.Lock();
            }
            int nDstStep = dstImage.BackBufferStride;

            byte* srcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();

            // If we want to display saturation we need to map to [0,254] so that index 255 is available for saturation.
            byte[] lut = CreateLUT(nBitsPerPixel, nMin, nMax, dGamma, bIsInverted,
                (byte)(bIsSaturation ? 254 : 255));

            for (int i = 0; i < nHeight; i++)
            {
                var pSrcData = srcData + i * nSrcStep;
                var pDstData = dstData + i * nDstStep;

                for (int j = 0; j < nWidth; ++j)
                {
                    ushort tmpValue;
                    if (nBitsPerPixel == 8)
                    {
                        tmpValue = *(pSrcData + j);
                    }
                    else
                    {
                        tmpValue = *(((ushort*)pSrcData) + j);
                    }

                    if (bIsSaturation && tmpValue > iSaturatonValue)
                        *(pDstData + j) = 255;
                    else
                        *(pDstData + j) = lut[tmpValue];
                } // for width
            } //for height
        }

        internal static unsafe void Scale_16u8u_C3(ref WriteableBitmap srcImage, ref WriteableBitmap dstImage,
                                                   int iMin, int iMax, double dGamma,
                                                   bool bIsSaturation, int iSaturatonValue, bool bIsInverted, int iColorGradation)
        {
            if (srcImage == null || dstImage == null) { return; }

            PixelFormatType pixelType = IppImaging.GetPixelFormatType(srcImage.Format);

            if (pixelType != PixelFormatType.P8u_C1 && pixelType != PixelFormatType.P8u_C3 &&
                pixelType != PixelFormatType.P16u_C1 && pixelType != PixelFormatType.P16u_C3)
            {
                throw new Exception("Image type currently not supported");
            }

            int iWidth = srcImage.PixelWidth;
            int iHeight = srcImage.PixelHeight;

            //
            // Process each image channel on its own thread
            //
            threadProcChan1Event.Set();
            threadProcChan2Event.Set();
            threadProcChan3Event.Set();

            int bitsPerPixel = srcImage.Format.BitsPerPixel;
            int iSrcStep = srcImage.BackBufferStride;
            int iDstStep = dstImage.BackBufferStride;

            byte[] lut = CreateLUT(bitsPerPixel, iMin, iMax, dGamma, bIsInverted);

            byte* pSrcImageData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* pDstImageData = (byte*)dstImage.BackBuffer.ToPointer();

            Thread threadChannel1 = new Thread(() => ThreadProcChannel1(pSrcImageData, iSrcStep, pDstImageData, iDstStep,
                                                                        lut, iWidth, iHeight, pixelType,
                                                                        bIsSaturation, iSaturatonValue, iColorGradation));
            Thread threadChannel2 = new Thread(() => ThreadProcChannel2(pSrcImageData, iSrcStep, pDstImageData, iDstStep,
                                                                        lut, iWidth, iHeight, pixelType,
                                                                        bIsSaturation, iSaturatonValue, iColorGradation));
            Thread threadChannel3 = new Thread(() => ThreadProcChannel3(pSrcImageData, iSrcStep, pDstImageData, iDstStep,
                                                                        lut, iWidth, iHeight, pixelType,
                                                                        bIsSaturation, iSaturatonValue, iColorGradation));

            threadChannel1.Start();
            threadChannel2.Start();
            threadChannel3.Start();

            //block main thread until all other threads have ran to completion.
            threadChannel1.Join();
            threadChannel2.Join();
            threadChannel3.Join();
        }

        /*internal static unsafe void Scale_16u8u_C3P3(ref WriteableBitmap srcImage, ref WriteableBitmap dstImage,
                                                     int nMin, int nMax, double dGamma,
                                                     bool bIsInverted, bool bIsSaturation,
                                                     int nSaturatonValue, int nColorChannel)
        {
            if (srcImage == null || dstImage == null)
                return;

            int nWidth = srcImage.PixelWidth;
            int nHeight = srcImage.PixelHeight;

            if (nWidth == 0 || nHeight == 0) { return; }

            byte[] lutRch = null;    //To avoid null access violation
            byte[] lutGch = null;
            byte[] lutBch = null;
            PixelFormat srcPixelFormat = srcImage.Format;
            int nSrcStride = srcImage.BackBufferStride;
            int nDstStride = dstImage.BackBufferStride;

            byte* srcRchData = null;
            byte* srcGchData = null;
            byte* srcBchData = null;
            int bpp = 16;

            if (srcPixelFormat == PixelFormats.Rgb48)
            {
                bpp = 16;
            }
            else if (srcPixelFormat == PixelFormats.Rgb24)
            {
                bpp = 8;
            }

            if (nColorChannel == 0)
            {
                // Red channel selected
                lutRch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 1)
            {
                // Green channel selected
                lutGch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 2)
            {
                // Blue channel selected
                lutBch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 3)
            {
                // Mix/RGB
                lutRch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
                lutGch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
                lutBch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }

            //if (dstImage == null)
            //{
            //    dstImage = new WriteableBitmap(nWidth, nHeight, 96, 96, PixelFormats.Rgb24, null);
            //}

            //int nSrcStride = srcImage.BackBufferStride;
            //int nDstStride = dstImage.BackBufferStride;

            //byte* pSrcImageData = (byte*)srcImage.BackBuffer.ToPointer();
            //byte* pDstImageData = (byte*)dstImage.BackBuffer.ToPointer();


            //if (srcRchBitmap != null)
            //    srcRchData = (byte*)srcRchBitmap.BackBuffer.ToPointer();
            //if (srcGchBitmap != null)
            //    srcGchData = (byte*)srcGchBitmap.BackBuffer.ToPointer();
            //if (srcBchBitmap != null)
            //    srcBchData = (byte*)srcBchBitmap.BackBuffer.ToPointer();

            //WriteableBitmap dstImage = new WriteableBitmap(width, height, 96, 96, dstPixelFormat, null);
            //int dstStride = dstImage.BackBufferStride;
            //int dstStride = destBitmap.BackBufferStride;

            // Reserve the back buffer for updates.
            //destBitmap.Lock();

            //byte* dstData = (byte*)destBitmap.BackBuffer.ToPointer();
            //byte* dstData = pDstImageData;


            //if (srcRchData != null)
            //    lutRch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            //if (srcGchData != null)
            //    lutGch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            //if (srcBchData != null)
            //    lutBch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            //byte[] lut = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);

            //byte* dstData = pDstImageData;

            byte* srcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();

            fixed (byte* pLutRchData = lutRch, pLutGchData = lutGch, pLutBchData = lutBch)
            {
                byte* pLutRch = pLutRchData;
                byte* pLutGch = pLutGchData;
                byte* pLutBch = pLutBchData;
                byte* pSrcRchData = null;
                byte* pSrcGchData = null;
                byte* pSrcBchData = null;

                byte* pDstData = null;

                int tmp = 0;
                ushort tmpRchValue = 0;
                ushort tmpGchValue = 0;
                ushort tmpBchValue = 0;

                for (int i = 0; i < nHeight; i++)
                {
                    if (nColorChannel == 0)
                    {
                        pSrcRchData = srcData + i * nSrcStride;
                        pSrcGchData = null;
                        pSrcBchData = null;
                    }
                    else if (nColorChannel == 1)
                    {
                        pSrcRchData = null;
                        pSrcGchData = srcData + i * nSrcStride;
                        pSrcBchData = null;
                    }
                    else if (nColorChannel == 2)
                    {
                        pSrcRchData = null;
                        pSrcGchData = null;
                        pSrcBchData = srcData + i * nSrcStride;
                    }
                    else if (nColorChannel == 3)
                    {
                        pSrcRchData = srcData + i * nSrcStride;
                        pSrcGchData = srcData + i * nSrcStride;
                        pSrcBchData = srcData + i * nSrcStride;
                    }
                    pDstData = dstData + i * nDstStride;

                    for (int j = 0; j < nWidth; ++j)
                    {
                        tmp = j * 3;    // 3 bytes

                        if (bpp == 8)
                        {
                            tmpRchValue = (pSrcRchData != null) ? *(pSrcRchData + tmp) : (byte)0;
                            tmpGchValue = (pSrcGchData != null) ? *(pSrcGchData + tmp + 1) : (byte)0;
                            tmpBchValue = (pSrcBchData != null) ? *(pSrcBchData + tmp + 2) : (byte)0;
                        }
                        else
                        {
                            tmpRchValue = (pSrcRchData != null) ? *(((ushort*)pSrcRchData) + tmp) : (ushort)0;
                            tmpGchValue = (pSrcGchData != null) ? *(((ushort*)pSrcGchData) + tmp + 1) : (ushort)0;
                            tmpBchValue = (pSrcBchData != null) ? *(((ushort*)pSrcBchData) + tmp + 2) : (ushort)0;
                        }

                        uint scaledRchValue = (pSrcRchData != null) ? *(pLutRch + tmpRchValue) : (uint)0;
                        uint scaledGchValue = (pSrcGchData != null) ? *(pLutGch + tmpGchValue) : (uint)0;
                        uint scaledBchValue = (pSrcBchData != null) ? *(pLutBch + tmpBchValue) : (uint)0;

                        if (bIsSaturation && ((tmpRchValue > nSaturatonValue) ||
                                              (tmpGchValue > nSaturatonValue) ||
                                              (tmpBchValue > nSaturatonValue)))
                        {
                            *(pDstData + tmp) = 255;
                            *(pDstData + tmp + 1) = 255;
                            *(pDstData + tmp + 2) = 255;
                        }
                        else
                        {
                            *(pDstData + tmp) = (byte)scaledRchValue;
                            *(pDstData + tmp + 1) = (byte)scaledGchValue;
                            *(pDstData + tmp + 2) = (byte)scaledBchValue;
                        }
                    } // for width
                } //for height
            }

            lutRch = null;
            lutGch = null;
            lutBch = null;

            // Release the back buffer and make it available for display.
            //destBitmap.Unlock();
        }*/

        /// <summary>
        /// Scale 3-channel image (16/8-bit per channel) to 3-channel image (24-bit RGB).
        /// </summary>
        /// <param name="srcImage">source image</param>
        /// <param name="dstImage">destination image</param>
        /// <param name="imageInfo">image information</param>
        /// <param name="nColorChannel">selected color channel</param>
        /// <param name="bIsMergeChannels">true: merge contrasted individual channels, false: contrast composite image</param>
        public static unsafe void Scale_16u8u_C3C3(ref WriteableBitmap srcImage,
                                                   ref WriteableBitmap dstImage,
                                                   ImageInfo imageInfo, int nColorChannel,
                                                   bool bIsMergeChannels = false)
        {
            if (srcImage == null || dstImage == null) { return; }

            int nWidth = srcImage.PixelWidth;
            int nHeight = srcImage.PixelHeight;

            if (nWidth == 0 || nHeight == 0) { return; }

            byte[] lutRch = null;    // To avoid null access violation
            byte[] lutGch = null;
            byte[] lutBch = null;
            int nSrcStride = srcImage.BackBufferStride;
            int nDstStride = dstImage.BackBufferStride;

            byte* srcRchData = null;
            byte* srcGchData = null;
            byte* srcBchData = null;
            int bpp = 16;

            PixelFormatType srcFormat = IppImaging.GetPixelFormatType(srcImage.Format);
            if (srcFormat != PixelFormatType.P16u_C3 && srcFormat != PixelFormatType.P8u_C3)
            {
                throw new Exception("Image pixel format is not supported.");
            }

            if (srcFormat == PixelFormatType.P16u_C3)
            {
                bpp = 16;
            }
            else if (srcFormat == PixelFormatType.P8u_C3)
            {
                bpp = 8;
            }

            int nMin = 0;
            int nMax = 0;
            double dGamma = 0;
            bool bIsInverted = false;
            bool bIsSaturation = false;
            int nSatThreshold = imageInfo.SaturationThreshold;

            if (nColorChannel == 0)
            {
                // Red channel selected
                nMin = imageInfo.RedChannel.BlackValue;
                nMax = imageInfo.RedChannel.WhiteValue;
                dGamma = imageInfo.RedChannel.GammaValue;
                bIsInverted = imageInfo.RedChannel.IsInvertChecked;
                bIsSaturation = imageInfo.RedChannel.IsSaturationChecked;

                lutRch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 1)
            {
                // Green channel selected
                nMin = imageInfo.GreenChannel.BlackValue;
                nMax = imageInfo.GreenChannel.WhiteValue;
                dGamma = imageInfo.GreenChannel.GammaValue;
                bIsInverted = imageInfo.GreenChannel.IsInvertChecked;
                bIsSaturation = imageInfo.GreenChannel.IsSaturationChecked;

                lutGch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 2)
            {
                // Blue channel selected
                nMin = imageInfo.BlueChannel.BlackValue;
                nMax = imageInfo.BlueChannel.WhiteValue;
                dGamma = imageInfo.BlueChannel.GammaValue;
                bIsInverted = imageInfo.BlueChannel.IsInvertChecked;
                bIsSaturation = imageInfo.BlueChannel.IsSaturationChecked;

                lutBch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 3)
            {
                if (bIsMergeChannels)
                {
                    // Contrast individual channel then merge them.
                    // Create individual lookup table for each channel.
                    //
                    nMin = imageInfo.RedChannel.BlackValue;
                    nMax = imageInfo.RedChannel.WhiteValue;
                    dGamma = imageInfo.RedChannel.GammaValue;
                    bIsInverted = imageInfo.RedChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.RedChannel.IsSaturationChecked;
                    lutRch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);

                    nMin = imageInfo.GreenChannel.BlackValue;
                    nMax = imageInfo.GreenChannel.WhiteValue;
                    dGamma = imageInfo.GreenChannel.GammaValue;
                    bIsInverted = imageInfo.GreenChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.GreenChannel.IsSaturationChecked;
                    lutGch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);

                    nMin = imageInfo.BlueChannel.BlackValue;
                    nMax = imageInfo.BlueChannel.WhiteValue;
                    dGamma = imageInfo.BlueChannel.GammaValue;
                    bIsInverted = imageInfo.BlueChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.BlueChannel.IsSaturationChecked;
                    lutBch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
                }
                else
                {
                    // Contrast 3-channel composite image (same lookup table for all 3 channels).
                    //
                    nMin = imageInfo.MixChannel.BlackValue;
                    nMax = imageInfo.MixChannel.WhiteValue;
                    dGamma = imageInfo.MixChannel.GammaValue;
                    bIsInverted = imageInfo.MixChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.MixChannel.IsSaturationChecked;
                    lutRch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
                    lutGch = lutRch;
                    lutBch = lutRch;
                }
            }

            byte* srcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();

            fixed (byte* pLutRchData = lutRch, pLutGchData = lutGch, pLutBchData = lutBch)
            {
                byte* pLutRch = pLutRchData;
                byte* pLutGch = pLutGchData;
                byte* pLutBch = pLutBchData;
                byte* pSrcRchData = null;
                byte* pSrcGchData = null;
                byte* pSrcBchData = null;

                byte* pDstData = null;

                int tmp = 0;
                ushort tmpRchValue = 0;
                ushort tmpGchValue = 0;
                ushort tmpBchValue = 0;

                for (int i = 0; i < nHeight; i++)
                {
                    if (nColorChannel == 0)
                    {
                        // cast to long to avoid integer overflow on very large image
                        pSrcRchData = srcData + (long)i * (long)nSrcStride;
                        pSrcGchData = null;
                        pSrcBchData = null;
                    }
                    else if (nColorChannel == 1)
                    {
                        pSrcRchData = null;
                        // cast to long to avoid integer overflow on very large image
                        pSrcGchData = srcData + (long)i * (long)nSrcStride;
                        pSrcBchData = null;
                    }
                    else if (nColorChannel == 2)
                    {
                        pSrcRchData = null;
                        pSrcGchData = null;
                        // cast to long to avoid integer overflow on very large image
                        pSrcBchData = srcData + (long)i * (long)nSrcStride;
                    }
                    else if (nColorChannel == 3)
                    {
                        // cast to long to avoid integer overflow on very large image
                        pSrcRchData = srcData + (long)i * (long)nSrcStride;
                        pSrcGchData = srcData + (long)i * (long)nSrcStride;
                        pSrcBchData = srcData + (long)i * (long)nSrcStride;
                    }
                    pDstData = dstData + i * nDstStride;

                    for (int j = 0; j < nWidth; ++j)
                    {
                        tmp = j * 3;    // 3 bytes

                        if (bpp == 8)
                        {
                            tmpRchValue = (pSrcRchData != null) ? *(pSrcRchData + tmp) : (byte)0;
                            tmpGchValue = (pSrcGchData != null) ? *(pSrcGchData + tmp + 1) : (byte)0;
                            tmpBchValue = (pSrcBchData != null) ? *(pSrcBchData + tmp + 2) : (byte)0;
                        }
                        else
                        {
                            tmpRchValue = (pSrcRchData != null) ? *(((ushort*)pSrcRchData) + tmp) : (ushort)0;
                            tmpGchValue = (pSrcGchData != null) ? *(((ushort*)pSrcGchData) + tmp + 1) : (ushort)0;
                            tmpBchValue = (pSrcBchData != null) ? *(((ushort*)pSrcBchData) + tmp + 2) : (ushort)0;
                        }

                        uint scaledRchValue = (pSrcRchData != null) ? *(pLutRch + tmpRchValue) : (uint)0;
                        uint scaledGchValue = (pSrcGchData != null) ? *(pLutGch + tmpGchValue) : (uint)0;
                        uint scaledBchValue = (pSrcBchData != null) ? *(pLutBch + tmpBchValue) : (uint)0;

                        if (bIsSaturation && ((tmpRchValue > nSatThreshold) ||
                                              (tmpGchValue > nSatThreshold) ||
                                              (tmpBchValue > nSatThreshold)))
                        {
                            //*(pDstData + tmp) = 255;
                            //*(pDstData + tmp + 1) = 255;
                            //*(pDstData + tmp + 2) = 255;

                            // Pink-ish purple as saturated pixel
                            *(pDstData + tmp) = 255;
                            *(pDstData + tmp + 1) = 159;
                            *(pDstData + tmp + 2) = 217;
                        }
                        else
                        {
                            *(pDstData + tmp) = (byte)scaledRchValue;
                            *(pDstData + tmp + 1) = (byte)scaledGchValue;
                            *(pDstData + tmp + 2) = (byte)scaledBchValue;
                        }
                    } // for width
                } //for height
            }

            lutRch = null;
            lutGch = null;
            lutBch = null;
        }
        /// <summary>
        /// Scale 4-channel image (16/8-bit per channel) to 3-channel image (24-bit RGB).
        /// </summary>
        /// <param name="srcImage">source image</param>
        /// <param name="dstImage">destination image</param>
        /// <param name="imageInfo">image information</param>
        /// <param name="nColorChannel">selected color channel</param>
        /// <param name="bIsMergeChannels">true: merge contrasted individual channels, false: contrast composite image</param>
        public static unsafe void Scale_16u8u_C4C3(ref WriteableBitmap srcImage,
                                                   ref WriteableBitmap dstImage,
                                                   ImageInfo imageInfo, int nColorChannel,
                                                   bool bIsMergeChannels = false)
        {
            if (srcImage == null || dstImage == null) { return; }

            int nWidth = srcImage.PixelWidth;
            int nHeight = srcImage.PixelHeight;

            if (nWidth == 0 || nHeight == 0) { return; }

            // To avoid null access violation
            byte[] lutRch = null;
            byte[] lutGch = null;
            byte[] lutBch = null;
            byte[] lutKch = null;       // gray
            int nSrcStride = srcImage.BackBufferStride;
            int nDstStride = dstImage.BackBufferStride;

            byte* srcRchData = null;
            byte* srcGchData = null;
            byte* srcBchData = null;
            byte* srcKchData = null;    // gray
            int bpp = 16;

            PixelFormatType srcFormat = IppImaging.GetPixelFormatType(srcImage.Format);
            if (srcFormat != PixelFormatType.P16u_C4 && srcFormat != PixelFormatType.P8u_C4)
            {
                throw new Exception("Image pixel format is not supported.");
            }

            if (srcFormat == PixelFormatType.P16u_C4)
            {
                bpp = 16;
            }
            else if (srcFormat == PixelFormatType.P8u_C4)
            {
                bpp = 8;
            }

            int nMin = 0;
            int nMax = 0;
            double dGamma = 0;
            bool bIsInverted = false;
            bool bIsSaturation = false;
            int nSatThreshold = imageInfo.SaturationThreshold;

            if (nColorChannel == 0)         // red channel contrast
            {
                nMin = imageInfo.RedChannel.BlackValue;
                nMax = imageInfo.RedChannel.WhiteValue;
                dGamma = imageInfo.RedChannel.GammaValue;
                bIsInverted = imageInfo.RedChannel.IsInvertChecked;
                bIsSaturation = imageInfo.RedChannel.IsSaturationChecked;

                lutRch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 1)    // green channel contrast
            {
                nMin = imageInfo.GreenChannel.BlackValue;
                nMax = imageInfo.GreenChannel.WhiteValue;
                dGamma = imageInfo.GreenChannel.GammaValue;
                bIsInverted = imageInfo.GreenChannel.IsInvertChecked;
                bIsSaturation = imageInfo.GreenChannel.IsSaturationChecked;

                lutGch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 2)    // blue channel contrast
            {
                nMin = imageInfo.BlueChannel.BlackValue;
                nMax = imageInfo.BlueChannel.WhiteValue;
                dGamma = imageInfo.BlueChannel.GammaValue;
                bIsInverted = imageInfo.BlueChannel.IsInvertChecked;
                bIsSaturation = imageInfo.BlueChannel.IsSaturationChecked;

                lutBch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 3)    // gray channel contrast
            {
                nMin = imageInfo.GrayChannel.BlackValue;
                nMax = imageInfo.GrayChannel.WhiteValue;
                dGamma = imageInfo.GrayChannel.GammaValue;
                bIsInverted = imageInfo.GrayChannel.IsInvertChecked;
                bIsSaturation = imageInfo.GrayChannel.IsSaturationChecked;

                lutKch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
            }
            else if (nColorChannel == 4)    // mix channel (rgb+gray) contrast
            {
                #region === 4-channel image contrast ===

                if (bIsMergeChannels)
                {
                    // Contrast individual channel then merge them.
                    // Create individual lookup table for each channel.
                    //
                    // red channel
                    nMin = imageInfo.RedChannel.BlackValue;
                    nMax = imageInfo.RedChannel.WhiteValue;
                    dGamma = imageInfo.RedChannel.GammaValue;
                    bIsInverted = imageInfo.RedChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.RedChannel.IsSaturationChecked;
                    lutRch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);

                    // green channel
                    nMin = imageInfo.GreenChannel.BlackValue;
                    nMax = imageInfo.GreenChannel.WhiteValue;
                    dGamma = imageInfo.GreenChannel.GammaValue;
                    bIsInverted = imageInfo.GreenChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.GreenChannel.IsSaturationChecked;
                    lutGch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);

                    // blue channel
                    nMin = imageInfo.BlueChannel.BlackValue;
                    nMax = imageInfo.BlueChannel.WhiteValue;
                    dGamma = imageInfo.BlueChannel.GammaValue;
                    bIsInverted = imageInfo.BlueChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.BlueChannel.IsSaturationChecked;
                    lutBch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);

                    // gray channel
                    nMin = imageInfo.GrayChannel.BlackValue;
                    nMax = imageInfo.GrayChannel.WhiteValue;
                    dGamma = imageInfo.GrayChannel.GammaValue;
                    bIsInverted = imageInfo.GrayChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.GrayChannel.IsSaturationChecked;
                    lutKch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
                }
                else
                {
                    // Contrast 4-channel composite image (same lookup table for all 4 channels).
                    //
                    nMin = imageInfo.MixChannel.BlackValue;
                    nMax = imageInfo.MixChannel.WhiteValue;
                    dGamma = imageInfo.MixChannel.GammaValue;
                    bIsInverted = imageInfo.MixChannel.IsInvertChecked;
                    bIsSaturation = imageInfo.MixChannel.IsSaturationChecked;
                    lutRch = CreateLUT(bpp, nMin, nMax, dGamma, bIsInverted);
                    lutGch = lutRch;
                    lutBch = lutRch;
                    lutKch = lutRch;
                }

                #endregion
            }

            byte* srcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();
            RGB rgb;

            fixed (byte* pLutRchData = lutRch, pLutGchData = lutGch, pLutBchData = lutBch, pLutKchData = lutKch)
            {
                byte* pLutRch = pLutRchData;
                byte* pLutGch = pLutGchData;
                byte* pLutBch = pLutBchData;
                byte* pLutKch = pLutKchData;
                byte* pSrcRchData = null;
                byte* pSrcGchData = null;
                byte* pSrcBchData = null;
                byte* pSrcKchData = null;

                byte* pDstData = null;

                int tmpSrc = 0;
                int tmpDst = 0;
                ushort tmpRchValue = 0;
                ushort tmpGchValue = 0;
                ushort tmpBchValue = 0;
                ushort tmpKchValue = 0;

                for (int i = 0; i < nHeight; i++)
                {
                    if (nColorChannel == 0)         //red channel
                    {
                        // cast to long to avoid integer overflow on very large image
                        pSrcRchData = srcData + (long)i * (long)nSrcStride;
                        pSrcGchData = null;
                        pSrcBchData = null;
                        pSrcKchData = null;
                    }
                    else if (nColorChannel == 1)    //green channel
                    {
                        pSrcRchData = null;
                        // cast to long to avoid integer overflow on very large image
                        pSrcGchData = srcData + (long)i * (long)nSrcStride;
                        pSrcBchData = null;
                        pSrcKchData = null;
                    }
                    else if (nColorChannel == 2)    //blue channel
                    {
                        pSrcRchData = null;
                        pSrcGchData = null;
                        // cast to long to avoid integer overflow on very large image
                        pSrcBchData = srcData + (long)i * (long)nSrcStride;
                        pSrcKchData = null;
                    }
                    else if (nColorChannel == 3)    //gray channel
                    {
                        pSrcRchData = null;
                        pSrcGchData = null;
                        pSrcBchData = null;
                        // cast to long to avoid integer overflow on very large image
                        pSrcKchData = srcData + (long)i * (long)nSrcStride;
                    }
                    else if (nColorChannel == 4)    //all 4 channel
                    {
                        // cast to long to avoid integer overflow on very large image
                        pSrcRchData = srcData + (long)i * (long)nSrcStride;
                        pSrcGchData = srcData + (long)i * (long)nSrcStride;
                        pSrcBchData = srcData + (long)i * (long)nSrcStride;
                        pSrcKchData = srcData + (long)i * (long)nSrcStride;
                    }
                    pDstData = dstData + i * nDstStride;

                    for (int j = 0; j < nWidth; ++j)
                    {
                        tmpSrc = j * 4; // 4 bytes (Rgba64)
                        tmpDst = j * 3; //3 bytes   (Rgb24)

                        if (bpp == 8)
                        {
                            tmpRchValue = (pSrcRchData != null) ? *(pSrcRchData + tmpSrc) : (byte)0;
                            tmpGchValue = (pSrcGchData != null) ? *(pSrcGchData + tmpSrc + 1) : (byte)0;
                            tmpBchValue = (pSrcBchData != null) ? *(pSrcBchData + tmpSrc + 2) : (byte)0;
                            tmpKchValue = (pSrcKchData != null) ? *(pSrcKchData + tmpSrc + 3) : (byte)0;
                        }
                        else
                        {
                            tmpRchValue = (pSrcRchData != null) ? *(((ushort*)pSrcRchData) + tmpSrc) : (ushort)0;
                            tmpGchValue = (pSrcGchData != null) ? *(((ushort*)pSrcGchData) + tmpSrc + 1) : (ushort)0;
                            tmpBchValue = (pSrcBchData != null) ? *(((ushort*)pSrcBchData) + tmpSrc + 2) : (ushort)0;
                            tmpKchValue = (pSrcKchData != null) ? *(((ushort*)pSrcKchData) + tmpSrc + 3) : (ushort)0;
                        }

                        uint scaledRchValue = (pSrcRchData != null) ? *(pLutRch + tmpRchValue) : (uint)0;
                        uint scaledGchValue = (pSrcGchData != null) ? *(pLutGch + tmpGchValue) : (uint)0;
                        uint scaledBchValue = (pSrcBchData != null) ? *(pLutBch + tmpBchValue) : (uint)0;
                        uint scaledKchValue = (pSrcKchData != null) ? *(pLutKch + tmpKchValue) : (uint)0;

                        if (bIsSaturation && ((tmpRchValue > nSatThreshold) ||
                                              (tmpGchValue > nSatThreshold) ||
                                              (tmpBchValue > nSatThreshold) ||
                                              (tmpKchValue > nSatThreshold)))
                        {
                            //*(pDstData + tmp) = 255;
                            //*(pDstData + tmp + 1) = 255;
                            //*(pDstData + tmp + 2) = 255;

                            // Pink-ish purple as saturated pixel
                            *(pDstData + tmpDst) = 255;
                            *(pDstData + tmpDst + 1) = 159;
                            *(pDstData + tmpDst + 2) = 217;
                        }
                        else
                        {
                            // Displaying the 4th channel as gray
                            //rgb = ColorSpaceHelper.Convert4ChanneltoRGB((int)scaledRchValue, (int)scaledGchValue, (int)scaledBchValue, (int)scaledKchValue);
                            rgb = ColorSpaceHelper.Convert4ChanneltoRGB((int)scaledRchValue, (int)scaledGchValue, (int)scaledBchValue, (int)scaledKchValue, nColorChannel, imageInfo.GrayChannel.IsInvertChecked);
                            *(pDstData + tmpDst) = (byte)rgb.Red;
                            *(pDstData + tmpDst + 1) = (byte)rgb.Green;
                            *(pDstData + tmpDst + 2) = (byte)rgb.Blue;
                        }
                    } // for width
                } //for height
            }

            lutRch = null;
            lutGch = null;
            lutBch = null;
            lutKch = null;
        }


        /*internal static unsafe WriteableBitmap Scale(WriteableBitmap[] srcImages,
                                                     int iMin, int iMax, double dGamma,
                                                     bool bIsSaturation, int iSaturatonValue,
                                                     bool bIsInverted, int iColorGradation)
        {
            Debug.Assert(srcImages != null, "Source image cannot be null");

            if (srcImages == null || srcImages.Length == 0) { return null; }

            int width = 0;
            int height = 0;
            int bpp = 16;
            int srcStride = 0;
            PixelFormatType srcPixelType = PixelFormatType.P16u_C1;
            PixelFormat dstPixelFormat = PixelFormats.Rgb24;

            foreach (var srcImage in srcImages)
            {
                if (srcImage != null)
                {
                    width = srcImage.PixelWidth;
                    height = srcImage.PixelHeight;
                    break;
                }
            }

            if (width == 0 || height == 0) { return null; }

            int nChannels = 3;
            if (iColorGradation == 2 || iColorGradation == 3)
            {
                nChannels = 3;
                dstPixelFormat = PixelFormats.Rgb24;
            }
            else if (iColorGradation == 4)
            {
                nChannels = iColorGradation;
                dstPixelFormat = PixelFormats.Cmyk32;
            }

            WriteableBitmap dstImage = new WriteableBitmap(width, height, 96, 96, dstPixelFormat, null);
            int dstStride = dstImage.BackBufferStride;
            IppiSize roiSize = new IppiSize(width, height);
            //byte[] lut = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);

            byte*[] pSrcImageData = new byte*[nChannels];
            WriteableBitmap[] scaledBitmap = new WriteableBitmap[nChannels];

            for (int i = 0; i < nChannels; i++)
            {
                if (srcImages[i] != null)
                {
                    // returns 8-bit
                    scaledBitmap[i] = Scale_C1(srcImages[i], iMin, iMax, dGamma, bIsInverted);
                }
                else
                {
                    //Ipp SetChannel does not allow null parameter
                    scaledBitmap[i] = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
                }

                if (i == 0)
                {
                    bpp = scaledBitmap[i].Format.BitsPerPixel;
                    srcStride = scaledBitmap[i].BackBufferStride;
                    srcPixelType = IppImaging.GetPixelFormatType(scaledBitmap[i].Format);
                }

                pSrcImageData[i] = (byte*)scaledBitmap[i].BackBuffer.ToPointer();
            }

            byte* pDstImage = (byte*)dstImage.BackBuffer.ToPointer();


            if (nChannels < 4)
            {
                // SetChannel expects BGR color order
                IppImaging.SetChannel(pSrcImageData[0], pSrcImageData[1], pSrcImageData[2], srcStride, roiSize, srcPixelType, pDstImage, dstStride);
            }
            else
            {
                //Ipp SetChannel4C expects BGRA color order
                IppImaging.SetChannel4C(pSrcImageData, srcStride, roiSize, srcPixelType, pDstImage, dstStride);
            }

            //MergeColorChannel(pSrcImageData, srcStride, width, height, pDstImage, dstStride);

            return dstImage;
        }*/

        /// <summary>
        /// Scale 16-bit or 8-bit per channel images to 24-bit RGBB image.
        /// </summary>
        /// <param name="srcImages"></param>
        /// <param name="iMin"></param>
        /// <param name="iMax"></param>
        /// <param name="dGamma"></param>
        /// <param name="bIsSaturation"></param>
        /// <param name="iSaturatonValue"></param>
        /// <param name="bIsInverted"></param>
        /// <returns></returns>
        internal static unsafe WriteableBitmap Scale_16u8uC1_C3(WriteableBitmap[] srcImages,
                                                                int iMin, int iMax, double dGamma,
                                                                bool bIsSaturation, int iSaturatonValue, bool bIsInverted)
        {
            if (srcImages == null || srcImages.Length == 0) { return null; }

            int width = 0;
            int height = 0;
            int srcBpp = 16;
            int srcStride = 0;
            PixelFormat srcPixelFormat = PixelFormats.Gray16;

            foreach (var srcBitmap in srcImages)
            {
                if (srcBitmap != null)
                {
                    width = srcBitmap.PixelWidth;
                    height = srcBitmap.PixelHeight;
                    srcPixelFormat = srcBitmap.Format;
                    srcBpp = srcBitmap.Format.BitsPerPixel;
                    srcStride = srcBitmap.BackBufferStride;
                    break;
                }
            }

            if (width == 0 || height == 0) { return null; }

            PixelFormat dstPixelFormat = PixelFormats.Rgb24;
            if (srcPixelFormat == PixelFormats.Gray16 || srcPixelFormat == PixelFormats.Gray8)
            {
                dstPixelFormat = PixelFormats.Rgb24;
            }
            else
            {
                return null;
            }

            // Expecting color order: R/G/B
            WriteableBitmap srcRchBitmap = srcImages[0];
            WriteableBitmap srcGchBitmap = srcImages[1];
            WriteableBitmap srcBchBitmap = srcImages[2];
            byte* srcRchData = null;
            byte* srcGchData = null;
            byte* srcBchData = null;

            if (srcRchBitmap != null)
                srcRchData = (byte*)srcRchBitmap.BackBuffer.ToPointer();
            if (srcGchBitmap != null)
                srcGchData = (byte*)srcGchBitmap.BackBuffer.ToPointer();
            if (srcBchBitmap != null)
                srcBchData = (byte*)srcBchBitmap.BackBuffer.ToPointer();

            WriteableBitmap dstImage = new WriteableBitmap(width, height, 96, 96, dstPixelFormat, null);
            int dstStride = dstImage.BackBufferStride;

            dstImage.Lock();

            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();

            byte[] lutRch = null;    //To avoid null access violation
            byte[] lutGch = null;
            byte[] lutBch = null;

            if (srcRchBitmap != null)
                lutRch = CreateLUT(srcBpp, iMin, iMax, dGamma, bIsInverted);
            if (srcGchBitmap != null)
                lutGch = CreateLUT(srcBpp, iMin, iMax, dGamma, bIsInverted);
            if (srcBchBitmap != null)
                lutBch = CreateLUT(srcBpp, iMin, iMax, dGamma, bIsInverted);

            fixed (byte* pLutRchData = lutRch, pLutGchData = lutGch, pLutBchData = lutBch)
            {
                byte* pLutRch = pLutRchData;
                byte* pLutGch = pLutGchData;
                byte* pLutBch = pLutBchData;
                byte* pSrcRchData = null;
                byte* pSrcGchData = null;
                byte* pSrcBchData = null;

                byte* pDstData = null;

                int tmp = 0;
                ushort tmpRchValue = 0;
                ushort tmpGchValue = 0;
                ushort tmpBchValue = 0;

                for (int i = 0; i < height; i++)
                {
                    pSrcRchData = (srcRchBitmap != null) ? srcRchData + i * srcStride : null;
                    pSrcGchData = (srcGchBitmap != null) ? srcGchData + i * srcStride : null;
                    pSrcBchData = (srcBchBitmap != null) ? srcBchData + i * srcStride : null;
                    pDstData = dstData + i * dstStride;

                    for (int j = 0; j < width; ++j)
                    {
                        tmp = j * 3;    // 3 bytes

                        if (srcBpp == 8)
                        {
                            tmpRchValue = (srcRchBitmap != null) ? *(pSrcRchData + j) : (byte)0;
                            tmpGchValue = (srcGchBitmap != null) ? *(pSrcGchData + j) : (byte)0;
                            tmpBchValue = (srcBchBitmap != null) ? *(pSrcBchData + j) : (byte)0;
                        }
                        else
                        {
                            tmpRchValue = (srcRchBitmap != null) ? *(((ushort*)pSrcRchData) + j) : (ushort)0;
                            tmpGchValue = (srcGchBitmap != null) ? *(((ushort*)pSrcGchData) + j) : (ushort)0;
                            tmpBchValue = (srcBchBitmap != null) ? *(((ushort*)pSrcBchData) + j) : (ushort)0;
                        }

                        uint scaledRchValue = (srcRchBitmap != null) ? *(pLutRch + tmpRchValue) : (uint)0;
                        uint scaledGchValue = (srcGchBitmap != null) ? *(pLutGch + tmpGchValue) : (uint)0;
                        uint scaledBchValue = (srcBchBitmap != null) ? *(pLutBch + tmpBchValue) : (uint)0;

                        if (bIsSaturation && ((tmpRchValue > iSaturatonValue) ||
                                              (tmpGchValue > iSaturatonValue) ||
                                              (tmpBchValue > iSaturatonValue)))
                        {
                            //*(pDstData + tmp) = 255;
                            //*(pDstData + tmp + 1) = 255;
                            //*(pDstData + tmp + 2) = 255;
                            *(pDstData + tmp) = 255;
                            *(pDstData + tmp + 1) = 186;
                            *(pDstData + tmp + 2) = 225;
                        }
                        else
                        {
                            *(pDstData + tmp) = (byte)scaledRchValue;
                            *(pDstData + tmp + 1) = (byte)scaledGchValue;
                            *(pDstData + tmp + 2) = (byte)scaledBchValue;
                        }
                    } // for width
                } //for height
            }

            lutRch = null;
            lutGch = null;
            lutBch = null;

            dstImage.Unlock();

            return dstImage;
        }

        internal static unsafe void Scale_16u8uC1_C3(WriteableBitmap[] srcImages,
                                                     int iMin, int iMax, double dGamma,
                                                     bool bIsSaturation, int iSaturatonValue, bool bIsInverted,
                                                     ref WriteableBitmap destBitmap)
        {
            if (srcImages == null || srcImages.Length == 0) { return; }

            int width = 0;
            int height = 0;
            int srcBpp = 16;
            int srcStride = 0;
            PixelFormat srcPixelFormat = PixelFormats.Gray16;

            foreach (var srcBitmap in srcImages)
            {
                if (srcBitmap != null)
                {
                    width = srcBitmap.PixelWidth;
                    height = srcBitmap.PixelHeight;
                    srcPixelFormat = srcBitmap.Format;
                    srcBpp = srcBitmap.Format.BitsPerPixel;
                    srcStride = srcBitmap.BackBufferStride;
                    break;
                }
            }

            if (width == 0 || height == 0) { return; }

            //PixelFormat dstPixelFormat = PixelFormats.Rgb24;
            //if (srcPixelFormat == PixelFormats.Gray16 || srcPixelFormat == PixelFormats.Gray8)
            //{
            //    dstPixelFormat = PixelFormats.Rgb24;
            //}
            //else
            //{
            //    return;
            //}

            // Expecting color order: R/G/B
            WriteableBitmap srcRchBitmap = srcImages[0];
            WriteableBitmap srcGchBitmap = srcImages[1];
            WriteableBitmap srcBchBitmap = srcImages[2];
            byte* srcRchData = null;
            byte* srcGchData = null;
            byte* srcBchData = null;

            if (srcRchBitmap != null)
                srcRchData = (byte*)srcRchBitmap.BackBuffer.ToPointer();
            if (srcGchBitmap != null)
                srcGchData = (byte*)srcGchBitmap.BackBuffer.ToPointer();
            if (srcBchBitmap != null)
                srcBchData = (byte*)srcBchBitmap.BackBuffer.ToPointer();

            //WriteableBitmap dstImage = new WriteableBitmap(width, height, 96, 96, dstPixelFormat, null);
            //int dstStride = dstImage.BackBufferStride;
            int dstStride = destBitmap.BackBufferStride;

            // Reserve the back buffer for updates.
            //destBitmap.Lock();

            byte* dstData = (byte*)destBitmap.BackBuffer.ToPointer();

            byte[] lutRch = null;    //To avoid null access violation
            byte[] lutGch = null;
            byte[] lutBch = null;

            if (srcRchBitmap != null)
                lutRch = CreateLUT(srcBpp, iMin, iMax, dGamma, bIsInverted);
            if (srcGchBitmap != null)
                lutGch = CreateLUT(srcBpp, iMin, iMax, dGamma, bIsInverted);
            if (srcBchBitmap != null)
                lutBch = CreateLUT(srcBpp, iMin, iMax, dGamma, bIsInverted);

            fixed (byte* pLutRchData = lutRch, pLutGchData = lutGch, pLutBchData = lutBch)
            {
                byte* pLutRch = pLutRchData;
                byte* pLutGch = pLutGchData;
                byte* pLutBch = pLutBchData;
                byte* pSrcRchData = null;
                byte* pSrcGchData = null;
                byte* pSrcBchData = null;

                byte* pDstData = null;

                int tmp = 0;
                ushort tmpRchValue = 0;
                ushort tmpGchValue = 0;
                ushort tmpBchValue = 0;

                for (int i = 0; i < height; i++)
                {
                    pSrcRchData = (srcRchBitmap != null) ? srcRchData + i * srcStride : null;
                    pSrcGchData = (srcGchBitmap != null) ? srcGchData + i * srcStride : null;
                    pSrcBchData = (srcBchBitmap != null) ? srcBchData + i * srcStride : null;
                    pDstData = dstData + i * dstStride;

                    for (int j = 0; j < width; ++j)
                    {
                        tmp = j * 3;    // 3 bytes

                        if (srcBpp == 8)
                        {
                            tmpRchValue = (srcRchBitmap != null) ? *(pSrcRchData + j) : (byte)0;
                            tmpGchValue = (srcGchBitmap != null) ? *(pSrcGchData + j) : (byte)0;
                            tmpBchValue = (srcBchBitmap != null) ? *(pSrcBchData + j) : (byte)0;
                        }
                        else
                        {
                            tmpRchValue = (srcRchBitmap != null) ? *(((ushort*)pSrcRchData) + j) : (ushort)0;
                            tmpGchValue = (srcGchBitmap != null) ? *(((ushort*)pSrcGchData) + j) : (ushort)0;
                            tmpBchValue = (srcBchBitmap != null) ? *(((ushort*)pSrcBchData) + j) : (ushort)0;
                        }

                        uint scaledRchValue = (srcRchBitmap != null) ? *(pLutRch + tmpRchValue) : (uint)0;
                        uint scaledGchValue = (srcGchBitmap != null) ? *(pLutGch + tmpGchValue) : (uint)0;
                        uint scaledBchValue = (srcBchBitmap != null) ? *(pLutBch + tmpBchValue) : (uint)0;

                        if (bIsSaturation && ((tmpRchValue > iSaturatonValue) ||
                                              (tmpGchValue > iSaturatonValue) ||
                                              (tmpBchValue > iSaturatonValue)))
                        {
                            //*(pDstData + tmp) = 255;
                            //*(pDstData + tmp + 1) = 255;
                            //*(pDstData + tmp + 2) = 255;
                            *(pDstData + tmp) = 255;
                            *(pDstData + tmp + 1) = 186;
                            *(pDstData + tmp + 2) = 225;
                        }
                        else
                        {
                            *(pDstData + tmp) = (byte)scaledRchValue;
                            *(pDstData + tmp + 1) = (byte)scaledGchValue;
                            *(pDstData + tmp + 2) = (byte)scaledBchValue;
                        }
                    } // for width
                } //for height
            }

            lutRch = null;
            lutGch = null;
            lutBch = null;

            // Release the back buffer and make it available for display.
            //destBitmap.Unlock();
        }

        internal static unsafe void Scale_16u8uC1_C3(byte*[] srcImages, int nWidth, int nHeight, int nSrcStride, PixelFormat pixelFormat,
                                                     byte* pDstImageData, int nDstStride,
                                                     int iMin, int iMax, double dGamma,
                                                     bool bIsSaturation, int iSaturatonValue, bool bIsInverted)
        {
            //int width = 0;
            //int height = 0;
            //int srcBpp = 16;
            //int srcStride = 0;
            //PixelFormat srcPixelFormat = PixelFormats.Gray16;

            //foreach (var srcBitmap in srcImages)
            //{
            //    if (srcBitmap != null)
            //    {
            //        width = srcBitmap.PixelWidth;
            //        height = srcBitmap.PixelHeight;
            //        srcPixelFormat = srcBitmap.Format;
            //        srcBpp = srcBitmap.Format.BitsPerPixel;
            //        srcStride = srcBitmap.BackBufferStride;
            //        break;
            //    }
            //}

            if (nWidth == 0 || nHeight == 0) { return; }

            //PixelFormat dstPixelFormat = PixelFormats.Rgb24;
            //if (srcPixelFormat == PixelFormats.Gray16 || srcPixelFormat == PixelFormats.Gray8)
            //{
            //    dstPixelFormat = PixelFormats.Rgb24;
            //}
            //else
            //{
            //    return;
            //}

            // Expecting color order: R/G/B
            //WriteableBitmap srcRchBitmap = srcImages[0];
            //WriteableBitmap srcGchBitmap = srcImages[1];
            //WriteableBitmap srcBchBitmap = srcImages[2];

            int bpp = 16;   // source image bits per pixel
            if (pixelFormat == PixelFormats.Gray16)
                bpp = 16;
            else if (pixelFormat == PixelFormats.Gray8)
                bpp = 8;

            byte* srcRchData = srcImages[0];
            byte* srcGchData = srcImages[1];
            byte* srcBchData = srcImages[2];

            //if (srcRchBitmap != null)
            //    srcRchData = (byte*)srcRchBitmap.BackBuffer.ToPointer();
            //if (srcGchBitmap != null)
            //    srcGchData = (byte*)srcGchBitmap.BackBuffer.ToPointer();
            //if (srcBchBitmap != null)
            //    srcBchData = (byte*)srcBchBitmap.BackBuffer.ToPointer();

            //WriteableBitmap dstImage = new WriteableBitmap(width, height, 96, 96, dstPixelFormat, null);
            //int dstStride = dstImage.BackBufferStride;
            //int dstStride = destBitmap.BackBufferStride;

            // Reserve the back buffer for updates.
            //destBitmap.Lock();

            //byte* dstData = (byte*)destBitmap.BackBuffer.ToPointer();
            byte* dstData = pDstImageData;

            byte[] lutRch = null;    //To avoid null access violation
            byte[] lutGch = null;
            byte[] lutBch = null;

            if (srcRchData != null)
                lutRch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            if (srcGchData != null)
                lutGch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            if (srcBchData != null)
                lutBch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            //byte[] lut = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);

            fixed (byte* pLutRchData = lutRch, pLutGchData = lutGch, pLutBchData = lutBch)
            {
                byte* pLutRch = pLutRchData;
                byte* pLutGch = pLutGchData;
                byte* pLutBch = pLutBchData;
                byte* pSrcRchData = null;
                byte* pSrcGchData = null;
                byte* pSrcBchData = null;

                byte* pDstData = null;

                int tmp = 0;
                ushort tmpRchValue = 0;
                ushort tmpGchValue = 0;
                ushort tmpBchValue = 0;

                for (int i = 0; i < nHeight; i++)
                {
                    pSrcRchData = (srcRchData != null) ? srcRchData + i * nSrcStride : null;
                    pSrcGchData = (srcGchData != null) ? srcGchData + i * nSrcStride : null;
                    pSrcBchData = (srcBchData != null) ? srcBchData + i * nSrcStride : null;
                    pDstData = dstData + i * nDstStride;

                    for (int j = 0; j < nWidth; ++j)
                    {
                        tmp = j * 3;    // 3 bytes

                        if (bpp == 8)
                        {
                            tmpRchValue = (srcRchData != null) ? *(pSrcRchData + j) : (byte)0;
                            tmpGchValue = (srcGchData != null) ? *(pSrcGchData + j) : (byte)0;
                            tmpBchValue = (srcBchData != null) ? *(pSrcBchData + j) : (byte)0;
                        }
                        else
                        {
                            tmpRchValue = (srcRchData != null) ? *(((ushort*)pSrcRchData) + j) : (ushort)0;
                            tmpGchValue = (srcGchData != null) ? *(((ushort*)pSrcGchData) + j) : (ushort)0;
                            tmpBchValue = (srcBchData != null) ? *(((ushort*)pSrcBchData) + j) : (ushort)0;
                        }

                        uint scaledRchValue = (srcRchData != null) ? *(pLutRch + tmpRchValue) : (uint)0;
                        uint scaledGchValue = (srcGchData != null) ? *(pLutGch + tmpGchValue) : (uint)0;
                        uint scaledBchValue = (srcBchData != null) ? *(pLutBch + tmpBchValue) : (uint)0;

                        if (bIsSaturation && ((tmpRchValue > iSaturatonValue) ||
                                              (tmpGchValue > iSaturatonValue) ||
                                              (tmpBchValue > iSaturatonValue)))
                        {
                            // Show saturated pixels as WHITE
                            //*(pDstData + tmp) = 255;
                            //*(pDstData + tmp + 1) = 255;
                            //*(pDstData + tmp + 2) = 255;
                            //
                            // Show saturated pixel as pink-ish purple
                            *(pDstData + tmp) = 255;
                            *(pDstData + tmp + 1) = 159;
                            *(pDstData + tmp + 2) = 217;
                        }
                        else
                        {
                            *(pDstData + tmp) = (byte)scaledRchValue;
                            *(pDstData + tmp + 1) = (byte)scaledGchValue;
                            *(pDstData + tmp + 2) = (byte)scaledBchValue;
                        }
                    } // for width
                } //for height
            }

            lutRch = null;
            lutGch = null;
            lutBch = null;

            // Release the back buffer and make it available for display.
            //destBitmap.Unlock();
        }

        internal static unsafe void Scale_16u8uC1_C4(byte*[] srcImages, int nWidth, int nHeight, int nSrcStride,
                                                     PixelFormat pixelFormat,
                                                     byte* pDstImageData, int nDstStride,
                                                     int iMin, int iMax, double dGamma,
                                                     bool bIsSaturation, int iSaturatonValue,
                                                     bool bIsInverted)
        {
            if (nWidth == 0 || nHeight == 0) { return; }

            int bpp = 16;   // source image bits per pixel
            if (pixelFormat == PixelFormats.Gray16)
                bpp = 16;
            else if (pixelFormat == PixelFormats.Gray8)
                bpp = 8;

            // Color order expected: R/G/B/K(Gray)
            byte* srcRchData = srcImages[0];
            byte* srcGchData = srcImages[1];
            byte* srcBchData = srcImages[2];
            byte* srcKchData = srcImages[3];

            byte* dstData = pDstImageData;

            // To avoid null access violation
            byte[] lutRch = null;
            byte[] lutGch = null;
            byte[] lutBch = null;
            byte[] lutKch = null;   // gray channel

            if (srcRchData != null)
                lutRch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            if (srcGchData != null)
                lutGch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            if (srcBchData != null)
                lutBch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);
            if (srcKchData != null)
                lutKch = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted);

            fixed (byte* pLutRchData = lutRch, pLutGchData = lutGch, pLutBchData = lutBch, pLutKchData = lutKch)
            {
                byte* pLutRch = pLutRchData;
                byte* pLutGch = pLutGchData;
                byte* pLutBch = pLutBchData;
                byte* pLutKch = pLutKchData;
                byte* pSrcRchData = null;
                byte* pSrcGchData = null;
                byte* pSrcBchData = null;
                byte* pSrcKchData = null;

                byte* pDstData = null;

                int tmp = 0;
                ushort tmpRchValue = 0;
                ushort tmpGchValue = 0;
                ushort tmpBchValue = 0;
                ushort tmpKchValue = 0;
                RGB rgb;

                for (int i = 0; i < nHeight; i++)
                {
                    pSrcRchData = (srcRchData != null) ? srcRchData + i * nSrcStride : null;
                    pSrcGchData = (srcGchData != null) ? srcGchData + i * nSrcStride : null;
                    pSrcBchData = (srcBchData != null) ? srcBchData + i * nSrcStride : null;
                    pSrcKchData = (srcKchData != null) ? srcKchData + i * nSrcStride : null;
                    pDstData = dstData + i * nDstStride;

                    for (int j = 0; j < nWidth; ++j)
                    {
                        tmp = j * 3;    // 3 bytes

                        if (bpp == 8)
                        {
                            tmpRchValue = (srcRchData != null) ? *(pSrcRchData + j) : (byte)0;
                            tmpGchValue = (srcGchData != null) ? *(pSrcGchData + j) : (byte)0;
                            tmpBchValue = (srcBchData != null) ? *(pSrcBchData + j) : (byte)0;
                            tmpKchValue = (srcKchData != null) ? *(pSrcKchData + j) : (byte)0;
                        }
                        else
                        {
                            tmpRchValue = (srcRchData != null) ? *(((ushort*)pSrcRchData) + j) : (ushort)0;
                            tmpGchValue = (srcGchData != null) ? *(((ushort*)pSrcGchData) + j) : (ushort)0;
                            tmpBchValue = (srcBchData != null) ? *(((ushort*)pSrcBchData) + j) : (ushort)0;
                            tmpKchValue = (srcKchData != null) ? *(((ushort*)pSrcKchData) + j) : (ushort)0;
                        }

                        uint scaledRchValue = (srcRchData != null) ? *(pLutRch + tmpRchValue) : (uint)0;
                        uint scaledGchValue = (srcGchData != null) ? *(pLutGch + tmpGchValue) : (uint)0;
                        uint scaledBchValue = (srcBchData != null) ? *(pLutBch + tmpBchValue) : (uint)0;
                        uint scaledKchValue = (srcKchData != null) ? *(pLutKch + tmpKchValue) : (uint)0;

                        if (bIsSaturation && ((tmpRchValue > iSaturatonValue) ||
                                              (tmpGchValue > iSaturatonValue) ||
                                              (tmpBchValue > iSaturatonValue) ||
                                              (tmpKchValue > iSaturatonValue)))
                        {
                            // Show saturated pixels as WHITE
                            //*(pDstData + tmp) = 255;
                            //*(pDstData + tmp + 1) = 255;
                            //*(pDstData + tmp + 2) = 255;
                            //
                            // Show saturated pixel as pink-ish purple
                            *(pDstData + tmp) = 255;
                            *(pDstData + tmp + 1) = 159;
                            *(pDstData + tmp + 2) = 217;
                        }
                        else
                        {
                            // Displaying the 4th channel as gray
                            rgb = ColorSpaceHelper.Convert4ChanneltoRGB((int)scaledRchValue, (int)scaledGchValue, (int)scaledBchValue, (int)scaledKchValue);
                            *(pDstData + tmp) = (byte)rgb.Red;
                            *(pDstData + tmp + 1) = (byte)rgb.Green;
                            *(pDstData + tmp + 2) = (byte)rgb.Blue;
                        }
                    } // for width
                } //for height
            }

            lutRch = null;
            lutGch = null;
            lutBch = null;
            lutKch = null;
        }

        internal static unsafe void Scale_16u8u_C1_Indexed(WriteableBitmap srcImage,
                                                           int iMin, int iMax, double dGamma,
                                                           bool bIsSaturation, int iSaturatonValue, bool bIsInverted,
                                                           ref WriteableBitmap dstImage)
        {
            Debug.Assert(srcImage != null, "Source image cannot be null");
            int iWidth = srcImage.PixelWidth;
            int iHeight = srcImage.PixelHeight;
            int bitsPerPixel = srcImage.Format.BitsPerPixel;
            int iSrcStep = srcImage.BackBufferStride;

            PixelFormat pixelFormat = srcImage.Format;
            Debug.Assert(pixelFormat == PixelFormats.Gray16 || pixelFormat == PixelFormats.Gray8, "Souurce pixel format must be Gray8 or Gray16");

            if (dstImage == null || dstImage.Format != PixelFormats.Indexed8)
            {
                PixelFormat dstPixelFormat;
                if (pixelFormat == PixelFormats.Gray16 || pixelFormat == PixelFormats.Gray8)
                {
                    dstPixelFormat = PixelFormats.Indexed8;
                }
                else
                {
                    return;
                }

                BitmapPalette pal = new BitmapPalette(GetColorTableIndexed(bIsSaturation));

                dstImage = new WriteableBitmap(iWidth, iHeight, 96, 96, dstPixelFormat, pal);
            }
            int iDstStep = dstImage.BackBufferStride;

            dstImage.Lock();

            byte* srcData = (byte*)srcImage.BackBuffer.ToPointer();
            byte* dstData = (byte*)dstImage.BackBuffer.ToPointer();

            // If we want to display saturation we need to map to [0,254] so that index 255 is available for saturation.
            byte[] lut = CreateLUT(bitsPerPixel, iMin, iMax, dGamma, bIsInverted,
                (byte)(bIsSaturation ? 254 : 255));

            for (int i = 0; i < iHeight; i++)
            {
                var pSrcData = srcData + i * iSrcStep;
                var pDstData = dstData + i * iDstStep;

                for (int j = 0; j < iWidth; ++j)
                {
                    ushort tmpValue;
                    if (bitsPerPixel == 8)
                    {
                        tmpValue = *(pSrcData + j);
                    }
                    else
                    {
                        tmpValue = *(((ushort*)pSrcData) + j);
                    }

                    if (bIsSaturation && tmpValue > iSaturatonValue)
                        *(pDstData + j) = 255;
                    else
                        *(pDstData + j) = lut[tmpValue];
                } // for width
            } //for height

            lut = null;
            dstImage.Unlock();
        }

        internal static unsafe void Scale_16u8u_C1_Indexed(byte* pSrcImageData, int nWidth, int nHeight, int nSrcStride, PixelFormat srcPixelFormat,
                                                           byte* pDstImageData, int nDstStride,
                                                           int iMin, int iMax, double dGamma,
                                                           bool bIsSaturation, int iSaturatonValue, bool bIsInverted)
        {
            int bpp = 16;
            if (srcPixelFormat == PixelFormats.Gray16)
                bpp = 16;
            else if (srcPixelFormat == PixelFormats.Gray8)
                bpp = 8;

            byte* srcData = pSrcImageData;
            byte* dstData = pDstImageData;

            // If we want to display saturation we need to map to [0,254] so that index 255 is available for saturation.
            byte[] lut = CreateLUT(bpp, iMin, iMax, dGamma, bIsInverted, (byte)(bIsSaturation ? 254 : 255));

            for (int i = 0; i < nHeight; i++)
            {
                var pSrcData = srcData + i * nSrcStride;
                var pDstData = dstData + i * nDstStride;

                for (int j = 0; j < nWidth; ++j)
                {
                    ushort tmpValue;
                    if (bpp == 8)
                    {
                        tmpValue = *(pSrcData + j);
                    }
                    else
                    {
                        tmpValue = *(((ushort*)pSrcData) + j);
                    }

                    if (bIsSaturation && tmpValue > iSaturatonValue)
                        *(pDstData + j) = 255;
                    else
                        *(pDstData + j) = lut[tmpValue];
                } // for width
            } //for height

            lut = null;
        }


        internal static unsafe void MergeColorChannel(byte*[] srcImageData, int srcStride, int width, int height, byte* dstImageData, int dstStride)
        {
            //if (redData == null && greenData == null && blueData == null) { return; }

            int nChannels = srcImageData.Length;
            //byte*[] srcImageData = { redData, blueData, greenData };
            byte*[] pSrcData = new byte*[nChannels];
            byte* pDstData = null;

            for (int i = 0; i < height; i++)
            {
                pDstData = dstImageData + (i * dstStride);

                for (int m = 0; m < nChannels; m++)
                {
                    pSrcData[m] = srcImageData[m] + (i * srcStride);
                }

                for (int j = 0; j < width; j++)
                {
                    for (int n = 0; n < nChannels; n++)
                    {
                        *pDstData++ = *pSrcData[n]++;
                    }
                }
            }
        }


        internal static unsafe void ThreadProcChannel1(byte* srcData, int iSrcStep, byte* dstData, int iDstStep,
                                                       byte[] lut, int iWidth, int iHeight, PixelFormatType pixelType,
                                                       bool bIsSaturation, int iSaturatonValue, int iColorGradation)
        {
            threadProcChan1Event.WaitOne();

            byte* pDstData = null;
            byte* pSatData = null;

            int iWLen = 0;
            int tmp = 0;
            int iLength = iWidth * iHeight;

            int iMultTempDst = 0;
            int iMultTempSrc = 0;
            int iMultTempSat = 0;

            if (pixelType == PixelFormatType.P8u_C3)
            {
                byte* pSrcData = null;

                for (int i = 0; i < iLength; i++)
                {
                    tmp = iWLen + iWLen + iWLen;
                    pDstData = dstData + iMultTempDst + tmp;
                    pSrcData = (byte*)(srcData + iMultTempSrc) + tmp;
                    fixed (void* pPtr = &lut[0])
                    {
                        byte* pLut = (byte*)pPtr;
                        //MemCopy(pDstData, pLut + *pSrcData, 1);
                        *pDstData = *(pLut + *pSrcData);
                    }
                    if (bIsSaturation)
                    {
                        //pSatData = satData + iMultTempSatr;
                        pSatData = srcData + iMultTempSat;
                        switch (iColorGradation)
                        {
                            case 0: // R level 
                                if (pSatData[tmp] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 1: // G level
                                if (pSatData[tmp + 1] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 2: // B level
                                if (pSatData[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 3: // All level
                                if (pSatData[tmp] > iSaturatonValue ||
                                    pSatData[tmp + 1] > iSaturatonValue ||
                                    pSatData[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                        }
                    }

                    iWLen++;
                    if (iWLen == iWidth)
                    {
                        iWLen = 0;
                        iMultTempDst = iMultTempDst + iDstStep;
                        iMultTempSrc = iMultTempSrc + iSrcStep;
                        iMultTempSat = iMultTempSat + iSrcStep;
                    }
                }
            }
            else
            {
                ushort* pSrcData = null;

                for (int i = 0; i < iLength; i++)
                {
                    tmp = iWLen + iWLen + iWLen;
                    pDstData = dstData + iMultTempDst + tmp;
                    pSrcData = (ushort*)(srcData + iMultTempSrc) + tmp;
                    fixed (void* pPtr = &lut[0])
                    {
                        byte* pLut = (byte*)pPtr;
                        //MemCopy(pDstData, pLut + *pSrcData, 1);
                        *pDstData = *(pLut + *pSrcData);
                    }

                    if (bIsSaturation)
                    {
                        //pSatData = satData + iMultTempSatr;
                        pSatData = srcData + iMultTempSat;
                        switch (iColorGradation)
                        {
                            case 0: // R channel
                                if (((ushort*)pSatData)[tmp] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 1: // G channel
                                if (((ushort*)pSatData)[tmp + 1] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 2: // B channel
                                if (((ushort*)pSatData)[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 3: // 3 channels
                                if (((ushort*)pSatData)[tmp] > iSaturatonValue ||
                                    ((ushort*)pSatData)[tmp + 1] > iSaturatonValue ||
                                    ((ushort*)pSatData)[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                        }
                    }

                    iWLen++;
                    if (iWLen == iWidth)
                    {
                        iWLen = 0;
                        iMultTempDst = iMultTempDst + iDstStep;
                        iMultTempSrc = iMultTempSrc + iSrcStep;
                        iMultTempSat = iMultTempSat + iSrcStep;
                    }
                }
            }

            threadProcChan1Event.Set();
        }

        internal static unsafe void ThreadProcChannel2(byte* srcData, int iSrcStep, byte* dstData, int iDstStep,
                                                       byte[] lut, int iWidth, int iHeight, PixelFormatType pixelType,
                                                       bool bIsSaturation, int iSaturatonValue, int iColorGradation)
        {
            threadProcChan2Event.WaitOne();

            byte* pDstData = null;
            byte* pSatData = null;

            int iWLen = 0;
            int tmp = 0;
            int iLength = iWidth * iHeight;

            int iMultTempDst = 0;
            int iMultTempSrc = 0;
            int iMultTempSatr = 0;

            if (pixelType == PixelFormatType.P8u_C3)
            {
                byte* pSrcData = null;

                for (int i = 0; i < iLength; i++)
                {
                    tmp = iWLen + iWLen + iWLen + 1;
                    pDstData = dstData + iMultTempDst + tmp;
                    pSrcData = (byte*)(srcData + iMultTempSrc) + tmp;
                    fixed (void* pPtr = &lut[0])
                    {
                        byte* pLut = (byte*)pPtr;
                        //MemCopy(pDstData, pLut + *pSrcData, 1);
                        *pDstData = *(pLut + *pSrcData);
                    }
                    if (bIsSaturation)
                    {
                        //pSatData = satData + iMultTempSatr;
                        pSatData = srcData + iMultTempSatr;
                        switch (iColorGradation)
                        {
                            case 0: // R level 
                                if (pSatData[tmp] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 1: // G level
                                if (pSatData[tmp + 1] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 2: // B level
                                if (pSatData[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 3: // All level
                                if (pSatData[tmp] > iSaturatonValue ||
                                    pSatData[tmp + 1] > iSaturatonValue ||
                                    pSatData[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                        }
                    }

                    iWLen++;
                    if (iWLen == iWidth)
                    {
                        iWLen = 0;
                        iMultTempDst = iMultTempDst + iDstStep;
                        iMultTempSrc = iMultTempSrc + iSrcStep;
                        iMultTempSatr = iMultTempSatr + iSrcStep;
                    }
                }
            }
            else
            {
                ushort* pSrcData = null;

                for (int i = 0; i < iLength; i++)
                {
                    tmp = iWLen + iWLen + iWLen + 1;
                    pDstData = dstData + iMultTempDst + tmp;
                    pSrcData = (ushort*)(srcData + iMultTempSrc) + tmp;
                    fixed (void* pPtr = &lut[0])
                    {
                        byte* pLut = (byte*)pPtr;
                        //MemCopy(pDstData, pLut + *pSrcData, 1);
                        *pDstData = *(pLut + *pSrcData);
                    }

                    if (bIsSaturation)
                    {
                        //pSatData = satData + iMultTempSatr;
                        pSatData = srcData + iMultTempSatr;
                        switch (iColorGradation)
                        {
                            case 0: // R channel
                                if (((ushort*)pSatData)[tmp] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 1: // G channel
                                if (((ushort*)pSatData)[tmp + 1] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 2: // B channel
                                if (((ushort*)pSatData)[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 3: // All channels
                                if (((ushort*)pSatData)[tmp] > iSaturatonValue ||
                                    ((ushort*)pSatData)[tmp + 1] > iSaturatonValue ||
                                    ((ushort*)pSatData)[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                        }
                    }

                    iWLen++;
                    if (iWLen == iWidth)
                    {
                        iWLen = 0;
                        iMultTempDst = iMultTempDst + iDstStep;
                        iMultTempSrc = iMultTempSrc + iSrcStep;
                        iMultTempSatr = iMultTempSatr + iSrcStep;
                    }
                }
            }

            threadProcChan2Event.Set();
        }

        internal static unsafe void ThreadProcChannel3(byte* srcData, int iSrcStep, byte* dstData, int iDstStep,
                                                       byte[] lut, int iWidth, int iHeight, PixelFormatType pixelType,
                                                       bool bIsSaturation, int iSaturatonValue, int iColorGradation)
        {
            threadProcChan3Event.WaitOne();

            byte* pDstData = null;
            byte* pSatData = null;
            //byte* gpBufDst = null;

            int iWLen = 0;
            int tmp = 0;
            int iLength = iWidth * iHeight;

            int iMultTempDst = 0;
            int iMultTempSrc = 0;
            int iMultTempSatr = 0;

            if (pixelType == PixelFormatType.P8u_C3)
            {
                byte* pSrcData = null;

                for (int i = 0; i < iLength; i++)
                {
                    tmp = iWLen + iWLen + iWLen + 2;
                    pDstData = dstData + iMultTempDst + tmp;
                    pSrcData = (byte*)(srcData + iMultTempSrc) + tmp;
                    fixed (void* pPtr = &lut[0])
                    {
                        byte* pLut = (byte*)pPtr;
                        //MemCopy(pDstData, pLut + *pSrcData, 1);
                        *pDstData = *(pLut + *pSrcData);
                    }
                    if (bIsSaturation)
                    {
                        //pSatData = satData + iMultTempSatr;
                        pSatData = srcData + iMultTempSatr;
                        switch (iColorGradation)
                        {
                            case 0: // R level 
                                if (pSatData[tmp] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 1: // G level
                                if (pSatData[tmp + 1] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 2: // B level
                                if (pSatData[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 3: // All level
                                if (pSatData[tmp] > iSaturatonValue ||
                                    pSatData[tmp + 1] > iSaturatonValue ||
                                    pSatData[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                        }
                    }

                    iWLen++;
                    if (iWLen == iWidth)
                    {
                        iWLen = 0;
                        iMultTempDst = iMultTempDst + iDstStep;
                        iMultTempSrc = iMultTempSrc + iSrcStep;
                        iMultTempSatr = iMultTempSatr + iSrcStep;
                    }
                }
            }
            else
            {
                ushort* pSrcData = null;

                for (int i = 0; i < iLength; i++)
                {
                    tmp = iWLen + iWLen + iWLen + 2;
                    pDstData = dstData + iMultTempDst + tmp;
                    pSrcData = (ushort*)(srcData + iMultTempSrc) + tmp;
                    fixed (void* pPtr = &lut[0])
                    {
                        byte* pLut = (byte*)pPtr;
                        //MemCopy(pDstData, pLut + *pSrcData, 1);
                        *pDstData = *(pLut + *pSrcData);
                    }

                    if (bIsSaturation)
                    {
                        //pSatData = satData + iMultTempSatr;
                        pSatData = srcData + iMultTempSatr;
                        switch (iColorGradation)
                        {
                            case 0: // R channel
                                if (((ushort*)pSatData)[tmp] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 1: // G channel
                                if (((ushort*)pSatData)[tmp + 1] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 2: // B channel
                                if (((ushort*)pSatData)[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                            case 3: // All channels
                                if (((ushort*)pSatData)[tmp] > iSaturatonValue ||
                                    ((ushort*)pSatData)[tmp + 1] > iSaturatonValue ||
                                    ((ushort*)pSatData)[tmp + 2] > iSaturatonValue)
                                {
                                    pDstData[0] = 255;
                                }
                                break;
                        }
                    }

                    iWLen++;
                    if (iWLen == iWidth)
                    {
                        iWLen = 0;
                        iMultTempDst = iMultTempDst + iDstStep;
                        iMultTempSrc = iMultTempSrc + iSrcStep;
                        iMultTempSatr = iMultTempSatr + iSrcStep;
                    }
                }
            }

            threadProcChan3Event.Set();
        }

        internal static unsafe void MemCopy(void* dest, void* src, int count)
        {
            int block;

            block = count >> 3;

            long* pDest = (long*)dest;
            long* pSrc = (long*)src;

            for (int i = 0; i < block; i++)
            {
                *pDest = *pSrc;
                pDest++;
                pSrc++;
            }
            dest = pDest;
            src = pSrc;
            count = count - (block << 3);

            if (count > 0)
            {
                byte* pDestB = (byte*)dest;
                byte* pSrcB = (byte*)src;
                for (int i = 0; i < count; i++)
                {
                    *pDestB = *pSrcB;
                    pDestB++;
                    pSrcB++;
                }
            }
        }


        public static unsafe void GetAutoScaleValues(WriteableBitmap srcBitmap,
                                                     ref int nMaxValue, ref int nMinValue,
                                                     ImageChannelType imageChannel = ImageChannelType.Mix)
        {
            if (srcBitmap == null)
                return;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int nWidth = srcBitmap.PixelWidth;
            int nHeight = srcBitmap.PixelHeight;
            int nSrcStep = srcBitmap.BackBufferStride;

            byte* pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();

            if (pixelFormat == PixelFormatType.P8u_C1)
            {
                #region === PixelType: PixelFormatType.P8u_C1 ===

                const int nBins = 255;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 256;

                int[,] pHist = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);

                // Get histogram highest peak
                int histoMax = 0;
                int histoMaxIndex = 0;
                for (int i = 0; i < nLevels; i++)
                {
                    if ((int)pHist[0, i] > histoMax)
                    {
                        histoMax = (int)pHist[0, i];
                        histoMaxIndex = i;
                    }
                }

                // Get image min and max pixel intensity
                uint minValue = 0;
                uint maxValue = 0;
                Rect roiRect = new Rect(0, 0, srcBitmap.PixelWidth, srcBitmap.PixelHeight);
                MinMax(srcBitmap, roiRect, ref minValue, ref maxValue);

                // If the intensity at histogram max is closer to max intensity than min intensity, chop the top 0.2% and bottom 0.2%.
                // If the intensity at histogram max is closer to min intensity than max intensity, chop the bottom 2% and top 0.2%.
                double pctTop = 0.0;
                double pctBottom = 0.0;
                if (Math.Abs(maxValue - histoMaxIndex) < Math.Abs(minValue - histoMaxIndex))
                {
                    pctTop = 0.002;
                    pctBottom = 0.002;
                }
                else
                {
                    pctTop = 0.002;
                    pctBottom = 0.02;
                }

                double iMinPos = 0, iMaxPos = 255, iTotalMin = 0, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    iTotalMin += pHist[0, i];
                    if (iTotalMin / iTotalPixel < pctBottom)
                        iMinPos = i;
                    iTotalMax += pHist[0, nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                nMaxValue = (int)iMaxPos;
                nMinValue = (int)iMinPos;

                #endregion
            }
            else if (pixelFormat == PixelFormatType.P8u_C3)
            {
                #region === PixelType: PixelFormatType.P8u_C3 ===

                const int nBins = 255;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 256;

                int[,] pHist_C3 = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);
                int[] pHist = new int[nLevels];

                int value = 0;
                if (imageChannel != ImageChannelType.Mix)
                {
                    // Single channel auto contrast (expected color order: R-G-B-Gray)
                    // See: ImageChannelType: 0=None, 1=red, 2=green, 3=blue, 4=gray, 5=mix (composite)
                    int nChannel = (int)imageChannel - 1;
                    // Get the histogram of the specified channel
                    for (int i = 0; i < nLevels; i++)
                    {
                        pHist[i] = pHist_C3[nChannel, i];
                    }
                }
                else
                {
                    // Composite/Multi-channel auto contrast
                    for (int i = 0; i < nLevels; i++)
                    {
                        value = Math.Max(pHist_C3[0, i], pHist_C3[1, i]);
                        value = Math.Max(value, pHist_C3[2, i]);
                        pHist[i] = value;
                    }
                }

                // Get histogram highest peak
                int histoMax = 0;
                int histoMaxIndex = 0;
                for (int i = 0; i < nLevels; i++)
                {
                    if ((int)pHist[i] > histoMax)
                    {
                        histoMax = (int)pHist[i];
                        histoMaxIndex = i;
                    }
                }

                // Get image min and max pixel intensity
                uint minValue = 0;
                uint maxValue = 0;
                Rect roiRect = new Rect(0, 0, srcBitmap.PixelWidth, srcBitmap.PixelHeight);
                MinMax(srcBitmap, roiRect, ref minValue, ref maxValue);

                // If the intensity at histogram max is closer to max intensity than min intensity, chop the top 0.2% and bottom 0.2%.
                // If the intensity at histogram max is closer to min intensity than max intensity, chop the bottom 2% and top 0.2%.
                double pctTop = 0.0;
                double pctBottom = 0.0;
                if (Math.Abs(maxValue - histoMaxIndex) < Math.Abs(minValue - histoMaxIndex))
                {
                    pctTop = 0.002;
                    pctBottom = 0.002;
                }
                else
                {
                    pctTop = 0.002;
                    pctBottom = 0.02;
                }

                double iMinPos = 0, iMaxPos = 255, iTotalMin = 0, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    iTotalMin += pHist[i];
                    if (iTotalMin / iTotalPixel < pctBottom)
                        iMinPos = i;
                    iTotalMax += pHist[nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                nMaxValue = (int)iMaxPos;
                nMinValue = (int)iMinPos;

                #endregion
            }
            else if (pixelFormat == PixelFormatType.P8u_C4)
            {
                #region === PixelType: PixelFormatType.P8u_C4 ===

                const int nBins = 255;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 256;

                int[,] pHist_C4 = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);
                int[] pHist = new int[nLevels];

                int value = 0;
                if (imageChannel != ImageChannelType.Mix)
                {
                    // Single channel auto contrast (expected color order: R-G-B-Gray)
                    // See: ImageChannelType: 0=None, 1=red, 2=green, 3=blue, 4=gray, 5=mix (composite)
                    int nChannel = (int)imageChannel - 1;
                    // Get the histogram of the specified channel
                    for (int i = 0; i < nLevels; i++)
                    {
                        pHist[i] = pHist_C4[nChannel, i];
                    }
                }
                else
                {
                    // Composite/Multi-channel auto contrast
                    for (int i = 0; i < nLevels; i++)
                    {
                        //value = Math.Max(pHist_C4[0, i], pHist_C4[1, i]);
                        //value = Math.Max(value, pHist_C4[2, i]);
                        //value = Math.Max(value, pHist_C4[3, i]);
                        int[] arrayVal = { pHist_C4[0, i], pHist_C4[1, i], pHist_C4[2, i], pHist_C4[3, i] };
                        value = arrayVal.Max();
                        pHist[i] = value;
                    }
                }

                // Get histogram highest peak
                int histoMax = 0;
                int histoMaxIndex = 0;
                for (int i = 0; i < nLevels; i++)
                {
                    if ((int)pHist[i] > histoMax)
                    {
                        histoMax = (int)pHist[i];
                        histoMaxIndex = i;
                    }
                }

                // Get image min and max pixel intensity
                uint minValue = 0;
                uint maxValue = 0;
                Rect roiRect = new Rect(0, 0, srcBitmap.PixelWidth, srcBitmap.PixelHeight);
                MinMax(srcBitmap, roiRect, ref minValue, ref maxValue);

                // If the intensity at histogram max is closer to max intensity than min intensity, chop the top 0.2% and bottom 0.2%.
                // If the intensity at histogram max is closer to min intensity than max intensity, chop the bottom 2% and top 0.2%.
                double pctTop = 0.0;
                double pctBottom = 0.0;
                if (Math.Abs(maxValue - histoMaxIndex) < Math.Abs(minValue - histoMaxIndex))
                {
                    pctTop = 0.002;
                    pctBottom = 0.002;
                }
                else
                {
                    pctTop = 0.002;
                    pctBottom = 0.02;
                }

                double iMinPos = 0, iMaxPos = 255, iTotalMin = 0, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    iTotalMin += pHist[i];
                    if (iTotalMin / iTotalPixel < pctBottom)
                        iMinPos = i;
                    iTotalMax += pHist[nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                nMaxValue = (int)iMaxPos;
                nMinValue = (int)iMinPos;

                #endregion
            }
            else if (pixelFormat == PixelFormatType.P16u_C1)
            {
                #region === PixelType: PixelFormatType.P16u_C1 ===

                const int nBins = 3000;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 65536;

                int[,] pHist = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);

                // Get histogram highest peak
                int histoMax = 0;
                int histoMaxIndex = 0;
                for (int i = 0; i < nLevels; i++)
                {
                    if ((int)pHist[0, i] > histoMax)
                    {
                        histoMax = (int)pHist[0, i];
                        histoMaxIndex = i;
                    }
                }

                // Scale the histogram peak to 65535
                histoMaxIndex = (int)(((double)histoMaxIndex + 0.5) * ((double)65535.0 / (double)nBins));

                // Get image min and max pixel intensity
                uint minValue = 0;
                uint maxValue = 0;
                Rect roiRect = new Rect(0, 0, srcBitmap.PixelWidth, srcBitmap.PixelHeight);
                MinMax(srcBitmap, roiRect, ref minValue, ref maxValue);

                // If the intensity at histogram max is closer to max intensity than min intensity, chop the top 0.2% and bottom 0.2%.
                // If the intensity at histogram max is closer to min intensity than max intensity, chop the bottom 2% and top 0.2%.
                double pctTop = 0.0;
                double pctBottom = 0.0;
                if (Math.Abs(maxValue - histoMaxIndex) < Math.Abs(minValue - histoMaxIndex))
                {
                    pctTop = 0.002;
                    pctBottom = 0.002;
                }
                else
                {
                    pctTop = 0.002;
                    pctBottom = 0.02;
                }

                double iMinPos = 0, iMaxPos = 65535, iTotalMin = 0, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    iTotalMin += pHist[0, i];
                    if (iTotalMin / iTotalPixel < pctBottom)
                        iMinPos = i;
                    iTotalMax += pHist[0, nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                // Scale the histogram peak to 65535
                iMinPos = (int)(((double)iMinPos + 0.5) * ((double)65535.0 / (double)nBins));
                // Scale the histogram peak to 65535
                iMaxPos = (int)(((double)iMaxPos + 0.5) * ((double)65535.0 / (double)nBins));

                nMaxValue = (int)iMaxPos;
                nMinValue = (int)iMinPos;

                #endregion
            }
            else if (pixelFormat == PixelFormatType.P16u_C3)
            {
                #region === PixelType: PixelFormatType.P16u_C3 ===

                const int nBins = 3000;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 65536;

                int[,] pHist_C3 = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);
                int[] pHist = new int[nLevels];

                int value = 0;
                if (imageChannel != ImageChannelType.Mix)
                {
                    // Single channel auto contrast (expected color order: R-G-B-Gray)
                    // See: ImageChannelType: 0=None, 1=red, 2=green, 3=blue, 4=gray, 5=mix (composite)
                    int nChannel = (int)imageChannel - 1;
                    // Get the histogram of the specified channel
                    for (int i = 0; i < nLevels; i++)
                    {
                        pHist[i] = pHist_C3[nChannel, i];
                    }
                }
                else
                {
                    // Composite/Multi-channel auto contrast
                    for (int i = 0; i < nLevels; i++)
                    {
                        value = Math.Max(pHist_C3[0, i], pHist_C3[1, i]);
                        value = Math.Max(value, pHist_C3[2, i]);
                        pHist[i] = value;
                    }
                }

                // Get histogram highest peak
                int histoMax = 0;
                int histoMaxIndex = 0;
                for (int i = 0; i < nLevels; i++)
                {
                    if ((int)pHist[i] > histoMax)
                    {
                        histoMax = (int)pHist[i];
                        histoMaxIndex = i;
                    }
                }

                // Get image min and max pixel intensity
                uint minValue = 0;
                uint maxValue = 0;
                Rect roiRect = new Rect(0, 0, srcBitmap.PixelWidth, srcBitmap.PixelHeight);
                MinMax(srcBitmap, roiRect, ref minValue, ref maxValue);

                // If the intensity at histogram max is closer to max intensity than min intensity, chop the top 0.2% and bottom 0.2%.
                // If the intensity at histogram max is closer to min intensity than max intensity, chop the bottom 2% and top 0.2%.
                double pctTop = 0.0;
                double pctBottom = 0.0;
                if (Math.Abs(maxValue - histoMaxIndex) < Math.Abs(minValue - histoMaxIndex))
                {
                    pctTop = 0.002;
                    pctBottom = 0.002;
                }
                else
                {
                    pctTop = 0.002;
                    pctBottom = 0.02;
                }

                double iMinPos = 0, iMaxPos = 65535, iTotalMin = 0, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    iTotalMin += pHist[i];
                    if (iTotalMin / iTotalPixel < pctBottom)
                        iMinPos = i;
                    iTotalMax += pHist[nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                // Scale the histogram peak to 65535
                iMinPos = (int)(((double)iMinPos + 0.5) * ((double)65535.0 / (double)nBins));
                // Scale the histogram peak to 65535
                iMaxPos = (int)(((double)iMaxPos + 0.5) * ((double)65535.0 / (double)nBins));

                nMaxValue = (int)iMaxPos;
                nMinValue = (int)iMinPos;

                #endregion
            }
            else if (pixelFormat == PixelFormatType.P16u_C4)
            {
                #region === PixelType: PixelFormatType.P16u_C4 ===

                const int nBins = 3000;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 65536;

                int[,] pHist_C4 = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);
                int[] pHist = new int[nLevels];

                int value = 0;
                if (imageChannel != ImageChannelType.Mix)
                {
                    // Single channel auto contrast (expected color order: R-G-B-Gray)
                    // See: ImageChannelType: 0=None, 1=red, 2=green, 3=blue, 4=gray, 5=mix (composite)
                    int nChannel = (int)imageChannel - 1;
                    // Get the histogram of the specified channel
                    for (int i = 0; i < nLevels; i++)
                    {
                        pHist[i] = pHist_C4[nChannel, i];
                    }
                }
                else
                {
                    // Composite/Multi-channel auto contrast
                    for (int i = 0; i < nLevels; i++)
                    {
                        value = Math.Max(pHist_C4[0, i], pHist_C4[1, i]);
                        value = Math.Max(value, pHist_C4[2, i]);
                        value = Math.Max(value, pHist_C4[3, i]);
                        //int[] arrayVal = { pHist_C4[0, i], pHist_C4[1, i], pHist_C4[2, i], pHist_C4[3, i] };
                        //value = arrayVal.Max();
                        pHist[i] = value;
                    }
                }

                // Get histogram highest peak
                int histoMax = 0;
                int histoMaxIndex = 0;
                for (int i = 0; i < nLevels; i++)
                {
                    if ((int)pHist[i] > histoMax)
                    {
                        histoMax = (int)pHist[i];
                        histoMaxIndex = i;
                    }
                }

                // Get image min and max pixel intensity
                uint minValue = 0;
                uint maxValue = 0;
                Rect roiRect = new Rect(0, 0, srcBitmap.PixelWidth, srcBitmap.PixelHeight);
                MinMax(srcBitmap, roiRect, ref minValue, ref maxValue);

                // If the intensity at histogram max is closer to max intensity than min intensity, chop the top 0.2% and bottom 0.2%.
                // If the intensity at histogram max is closer to min intensity than max intensity, chop the bottom 2% and top 0.2%.
                double pctTop = 0.0;
                double pctBottom = 0.0;
                if (Math.Abs(maxValue - histoMaxIndex) < Math.Abs(minValue - histoMaxIndex))
                {
                    pctTop = 0.002;
                    pctBottom = 0.002;
                }
                else
                {
                    pctTop = 0.002;
                    pctBottom = 0.02;
                }

                double iMinPos = 0, iMaxPos = 65535, iTotalMin = 0, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    iTotalMin += pHist[i];
                    if (iTotalMin / iTotalPixel < pctBottom)
                        iMinPos = i;
                    iTotalMax += pHist[nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                // Scale the histogram peak to 65535
                iMinPos = (int)(((double)iMinPos + 0.5) * ((double)65535.0 / (double)nBins));
                // Scale the histogram peak to 65535
                iMaxPos = (int)(((double)iMaxPos + 0.5) * ((double)65535.0 / (double)nBins));

                nMaxValue = (int)iMaxPos;
                nMinValue = (int)iMinPos;

                #endregion
            }

        }

        public static void GetChemiAutoScaleValues(WriteableBitmap srcBitmap, ref int iBlackValue, ref int iWhiteValue)
        {
            if (srcBitmap == null)
                return;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);

            if (pixelFormat != PixelFormatType.P8u_C1 && pixelFormat != PixelFormatType.P8u_C3 &&
                pixelFormat != PixelFormatType.P16u_C1 && pixelFormat != PixelFormatType.P16u_C3)
            {
                throw new Exception("Pixel format not supported");
            }

            const int nBins = 65535;
            const int nLevels = nBins + 1;
            int nLowerLevel = 0;
            int nUpperLevel = 65536;
            double dSum = 0;
            int totalPixels = srcBitmap.PixelWidth * srcBitmap.PixelHeight;
            int iThreshold = 0;
            double testVal = 0.0;
            double dMean = 0.0;
            double dStdDev = 0.0;
            iBlackValue = 0;
            iWhiteValue = 65535;

            //Get the image histogram
            int[,] pHist = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);

            for (int i = 0; i < nLevels; i++)
            {
                dSum += pHist[0, i];
                testVal = dSum / (double)totalPixels;
                if (testVal <= 0.990)
                {
                    iThreshold = i;
                }
                if (testVal <= 0.9993)
                {
                    iWhiteValue = i;
                }
            }

            MeanStdDev(srcBitmap, ref dMean, ref dStdDev);
            WriteableBitmap image2 = OutlierSubstitution(srcBitmap, iThreshold, dMean);

            double dMean2 = 0.0;
            double dStdDev2 = 0.0;
            MeanStdDev(image2, ref dMean2, ref dStdDev2);

            iBlackValue = (int)(dMean2 + 0.0 * dStdDev);

            // clean up
            pHist = null;
            image2 = null;

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        public static void GetVisibleAutoScaleValues(WriteableBitmap srcBitmap, ref int nMinValue, ref int nMaxValue)
        {
            if (srcBitmap == null)
                return;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);

            if (pixelFormat != PixelFormatType.P8u_C1 && pixelFormat != PixelFormatType.P8u_C3 &&
                pixelFormat != PixelFormatType.P16u_C1 && pixelFormat != PixelFormatType.P16u_C3)
            {
                throw new Exception("Pixel format not supported");
            }

            int nWidth = srcBitmap.PixelWidth;
            int nHeight = srcBitmap.PixelHeight;
            int nSrcStep = srcBitmap.BackBufferStride;

            if (pixelFormat == PixelFormatType.P8u_C1)
            {
                #region === PixelType: MVImage.P8uC1 ===

                const int nBins = 255;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 256;

                int[,] pHist = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);

                // chop the top 0.2% and don't chop from the bottom (uses image min).
                double pctTop = 0.002;
                //double pctBottom = 0.002;
                //double iMinPos = 0, iMaxPos = 255, iTotalMin = 0, iTotalMax = 0;
                double iMaxPos = 255, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    //iTotalMin += pHist[0, i];
                    //if (iTotalMin / iTotalPixel < pctBottom)
                    //    iMinPos = i;
                    iTotalMax += pHist[0, nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    //if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                    if (iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                nMaxValue = (int)iMaxPos;
                //nMinValue = (int)iMinPos;
                nMinValue = Min(srcBitmap, new Rect(0, 0, nWidth, nHeight));

                #endregion
            }
            else if (pixelFormat == PixelFormatType.P8u_C3)
            {
                #region === PixelType: MVImage.P8uC3 ===

                const int nBins = 255;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 256;

                int[,] pHist_C3 = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);
                int[] pHist = new int[nLevels];

                int value = 0;
                for (int i = 0; i < nLevels; i++)
                {
                    value = Math.Max(pHist_C3[0, i], pHist_C3[1, i]);
                    value = Math.Max(value, pHist_C3[2, i]);
                    pHist[i] = value;
                }

                // chop the top 0.2% and don't chop from the bottom (uses image min).
                double pctTop = 0.002;
                //double pctBottom = 0.002;
                //double iMinPos = 0, iMaxPos = 255, iTotalMin = 0, iTotalMax = 0;
                double iMaxPos = 255, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    //iTotalMin += pHist[i];
                    //if (iTotalMin / iTotalPixel < pctBottom)
                    //    iMinPos = i;
                    iTotalMax += pHist[nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    //if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                    if (iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                nMaxValue = (int)iMaxPos;
                //nMinValue = (int)iMinPos;
                nMinValue = Min(srcBitmap, new Rect(0, 0, nWidth, nHeight));

                #endregion
            }
            else if (pixelFormat == PixelFormatType.P16u_C1)
            {
                #region === PixelType: MVImage.P16uC1 ===

                const int nBins = 3000;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 65536;

                int[,] pHist = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);

                // chop the top 0.2% and don't chop from the bottom (uses image min).
                double pctTop = 0.002;
                //double pctBottom = 0.002;
                //double iMinPos = 0, iMaxPos = 65535, iTotalMin = 0, iTotalMax = 0;
                double iMaxPos = 65535, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    //iTotalMin += pHist[0, i];
                    //if (iTotalMin / iTotalPixel < pctBottom)
                    //    iMinPos = i;
                    iTotalMax += pHist[0, nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    //if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                    //    break;
                    if (iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                // Scale the histogram peak to 65535
                //iMinPos = (int)(((double)iMinPos + 0.5) * ((double)65535.0 / (double)nBins));
                // Scale the histogram peak to 65535
                iMaxPos = (int)(((double)iMaxPos + 0.5) * ((double)65535.0 / (double)nBins));

                nMaxValue = (int)iMaxPos;
                //nMinValue = (int)iMinPos;
                nMinValue = Min(srcBitmap, new Rect(0, 0, nWidth, nHeight));

                #endregion
            }
            else if (pixelFormat == PixelFormatType.P16u_C3)
            {
                #region === PixelType: MVImage.P16uC3 ===

                const int nBins = 3000;
                const int nLevels = nBins + 1;
                int nLowerLevel = 0;
                int nUpperLevel = 65536;

                int[,] pHist_C3 = HistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);
                int[] pHist = new int[nLevels];

                int value = 0;
                for (int i = 0; i < nLevels; i++)
                {
                    value = Math.Max(pHist_C3[0, i], pHist_C3[1, i]);
                    value = Math.Max(value, pHist_C3[2, i]);
                    pHist[i] = value;
                }

                // chop the top 0.2% and don't chop from the bottom (uses image min).
                double pctTop = 0.002;
                //double pctBottom = 0.002;
                //double iMinPos = 0, iMaxPos = 65535, iTotalMin = 0, iTotalMax = 0;
                double iMaxPos = 65535, iTotalMax = 0;
                double iTotalPixel = nWidth * nHeight;
                for (int i = 0; i < nBins; ++i)
                {
                    //iTotalMin += pHist[i];
                    //if (iTotalMin / iTotalPixel < pctBottom)
                    //    iMinPos = i;
                    iTotalMax += pHist[nBins - i];
                    if (iTotalMax / iTotalPixel < pctTop)
                        iMaxPos = nBins - i;
                    //if (iTotalMin / iTotalPixel > pctBottom && iTotalMax / iTotalPixel > pctTop)
                    if (iTotalMax / iTotalPixel > pctTop)
                        break;
                }

                // Scale the histogram peak to 65535
                //iMinPos = (int)(((double)iMinPos + 0.5) * ((double)65535.0 / (double)nBins));
                // Scale the histogram peak to 65535
                iMaxPos = (int)(((double)iMaxPos + 0.5) * ((double)65535.0 / (double)nBins));

                nMaxValue = (int)iMaxPos;
                //nMinValue = (int)iMinPos;
                nMinValue = Min(srcBitmap, new Rect(0, 0, nWidth, nHeight));

                #endregion
            }
        }

        /// <summary>
        /// Substitute each pixel with the mean value if the pixel is greater than the threshold value.
        /// </summary>
        /// <param name="srcImage"></param>
        /// <param name="dThreshold"></param>
        /// <param name="dMean"></param>
        /// <returns></returns>
        public static unsafe WriteableBitmap OutlierSubstitution(WriteableBitmap srcImage, double dThreshold, double dMean)
        {
            if (srcImage == null) { return srcImage; }

            int imageWidth = srcImage.PixelWidth;
            int imageHeight = srcImage.PixelHeight;

            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            // Get a pointer to the back buffer.
            ushort* pCpyData = (ushort*)cpyImage.BackBuffer.ToPointer();

            // Reserve the back buffer for updates.
            cpyImage.Lock();

            for (int iRow = 0; iRow < imageHeight; ++iRow)
            {
                for (int iCol = 0; iCol < imageWidth; ++iCol)
                {
                    if (*(pCpyData + iCol + imageWidth * iRow) > (int)dThreshold)
                    {
                        *(pCpyData + iCol + imageWidth * iRow) = (ushort)dMean;
                    }
                }
            }

            // Release the back buffer and make it available for display.
            cpyImage.Unlock();

            return cpyImage;
        }

        public static int[,] HistogramEven(WriteableBitmap srcBitmap, int nLevels, int nLowerLevel, int nUpperLevel)
        {
            if (srcBitmap == null)
                return null;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            if (pixelFormat == PixelFormatType.P8u_C1)
            {
                int[,] pLevels = new int[1, nLevels];
                //for (int i = 0; i < nLevels; ++i)
                //{
                //    pLevels[0, i] = i;
                //}
                int[,] pHist = new int[1, nLevels];
                int[] pnLevels = new int[1];
                int[] pnLowerLevel = new int[1];
                int[] pnUpperLevel = new int[1];

                pnLevels[0] = nLevels;
                pnLowerLevel[0] = nLowerLevel;
                pnUpperLevel[0] = nUpperLevel;

                HistogramEven(srcBitmap, pHist, pLevels, pnLevels, pnLowerLevel, pnUpperLevel);

                return pHist;
            }
            else if (pixelFormat == PixelFormatType.P8u_C3)
            {
                int[,] p8uLevels = new int[3, nLevels];
                for (int i = 0; i < nLevels; ++i)
                {
                    p8uLevels[0, i] = i;
                    p8uLevels[1, i] = i;
                    p8uLevels[2, i] = i;
                }
                int[,] p8uHist = new int[3, nLevels];
                int[] pn8uLevels = new int[3];
                int[] pn8uLowerLevel = new int[3];
                int[] pn8uUpperLevel = new int[3];
                for (int i = 0; i < 3; ++i)
                {
                    pn8uLevels[i] = nLevels;
                    pn8uLowerLevel[i] = nLowerLevel;
                    pn8uUpperLevel[i] = nUpperLevel;
                }

                HistogramEven(srcBitmap, p8uHist, p8uLevels, pn8uLevels, pn8uLowerLevel, pn8uUpperLevel);

                return p8uHist;
            }
            else if (pixelFormat == PixelFormatType.P16u_C1)
            {
                int[,] pLevels = new int[1, nLevels];
                //for (int i = 0; i < nLevels; ++i)
                //{
                //    pLevels[0, i] = i;
                //}
                int[,] pHist = new int[1, nLevels];
                int[] pnLevels = new int[1];
                int[] pnLowerLevel = new int[1];
                int[] pnUpperLevel = new int[1];

                pnLevels[0] = nLevels;
                pnLowerLevel[0] = nLowerLevel;
                pnUpperLevel[0] = nUpperLevel;

                HistogramEven(srcBitmap, pHist, pLevels, pnLevels, pnLowerLevel, pnUpperLevel);

                return pHist;
            }
            else if (pixelFormat == PixelFormatType.P16u_C3)
            {
                int[,] p16uLevels = new int[3, nLevels];
                for (int i = 0; i < nLevels; ++i)
                {
                    p16uLevels[0, i] = i;
                    p16uLevels[1, i] = i;
                    p16uLevels[2, i] = i;
                }
                int[,] p16uHist = new int[3, nLevels];
                int[] pn16uLevels = new int[3];
                int[] pn16uLowerLevel = new int[3];
                int[] pn16uUpperLevel = new int[3];
                for (int i = 0; i < 3; ++i)
                {
                    pn16uLevels[i] = nLevels;
                    pn16uLowerLevel[i] = nLowerLevel;
                    pn16uUpperLevel[i] = nUpperLevel;
                }

                HistogramEven(srcBitmap, p16uHist, p16uLevels, pn16uLevels, pn16uLowerLevel, pn16uUpperLevel);

                return p16uHist;
            }
            else if (pixelFormat == PixelFormatType.P16u_C4)
            {
                int[,] p16uLevels = new int[4, nLevels];
                for (int i = 0; i < nLevels; ++i)
                {
                    p16uLevels[0, i] = i;
                    p16uLevels[1, i] = i;
                    p16uLevels[2, i] = i;
                    p16uLevels[3, i] = i;
                }
                int[,] p16uHist = new int[4, nLevels];
                int[] pn16uLevels = new int[4];
                int[] pn16uLowerLevel = new int[4];
                int[] pn16uUpperLevel = new int[4];
                for (int i = 0; i < 4; ++i)
                {
                    pn16uLevels[i] = nLevels;
                    pn16uLowerLevel[i] = nLowerLevel;
                    pn16uUpperLevel[i] = nUpperLevel;
                }

                HistogramEven(srcBitmap, p16uHist, p16uLevels, pn16uLevels, pn16uLowerLevel, pn16uUpperLevel);

                return p16uHist;
            }

            return null;
        }

        //
        // Computes the intensity histogram of an image using equal bins.
        // This function computes the intensity histogram of an image in the ranges
        // specified by the values lowerLevel (inclusive), upperLevel (exclusive), and nLevels.
        // The function operates on the assumption that all histogram bins have the same width
        // and equal boundary values of the bins (levels).
        //
        public static unsafe void HistogramEven(WriteableBitmap srcBitmap, int[,] pHist, int[,] pLevels, int[] nLevels, int[] nLowerLevel, int[] nUpperLevel)
        {
            if (srcBitmap == null)
                return;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);

            int width = srcBitmap.PixelWidth;
            int height = srcBitmap.PixelHeight;
            //int iSrcStep = ((srcBitmap.PixelWidth * bitmap.Format.BitsPerPixel) + 31) / 32 * 4;
            int srcStep = srcBitmap.BackBufferStride;
            IppiSize roiSize = new IppiSize(width, height);

            byte* pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();

            fixed (int* fpHist = pHist, fpLevels = pLevels)
            {
                int col = pHist.GetUpperBound(1) + 1;
                int*[] fpHistAll = new int*[col];
                int*[] fpLevelsAll = new int*[col];

                for (int i = 0; i < col; ++i)
                {
                    fpHistAll[i] = fpHist + i * col;
                    fpLevelsAll[i] = fpLevels + i * col;
                }

                IppImaging.HistogramEven(pSrc, srcStep, pixelFormat, roiSize, fpHistAll, fpLevelsAll, nLevels, nLowerLevel, nUpperLevel);
            }
        }

        public static int[,] GetHistogramEven(WriteableBitmap srcBitmap, int nLevels, int nLowerLevel, int nUpperLevel)
        {
            if (srcBitmap == null)
                return null;

            PixelFormatType pixelType = IppImaging.GetPixelFormatType(srcBitmap.Format);

            if (pixelType == PixelFormatType.P8u_C1)
            {
                int[,] pLevels = new int[1, nLevels];
                //for (int i = 0; i < nLevels; ++i)
                //{
                //    pLevels[0, i] = i;
                //}
                int[,] pHist = new int[1, nLevels];
                int[] pnLevels = new int[1];
                int[] pnLowerLevel = new int[1];
                int[] pnUpperLevel = new int[1];

                pnLevels[0] = nLevels;
                pnLowerLevel[0] = nLowerLevel;
                pnUpperLevel[0] = nUpperLevel;

                HistogramEven(srcBitmap, pHist, pLevels, pnLevels, pnLowerLevel, pnUpperLevel);

                return pHist;
            }
            else if (pixelType == PixelFormatType.P8u_C3)
            {
                int[,] p8uLevels = new int[3, nLevels];
                for (int i = 0; i < nLevels; ++i)
                {
                    p8uLevels[0, i] = i;
                    p8uLevels[1, i] = i;
                    p8uLevels[2, i] = i;
                }
                int[,] p8uHist = new int[3, nLevels];
                int[] pn8uLevels = new int[3];
                int[] pn8uLowerLevel = new int[3];
                int[] pn8uUpperLevel = new int[3];
                for (int i = 0; i < 3; ++i)
                {
                    pn8uLevels[i] = nLevels;
                    pn8uLowerLevel[i] = nLowerLevel;
                    pn8uUpperLevel[i] = nUpperLevel;
                }

                HistogramEven(srcBitmap, p8uHist, p8uLevels, pn8uLevels, pn8uLowerLevel, pn8uUpperLevel);

                return p8uHist;
            }
            else if (pixelType == PixelFormatType.P16u_C1)
            {
                int[,] pLevels = new int[1, nLevels];
                //for (int i = 0; i < nLevels; ++i)
                //{
                //    pLevels[0, i] = i;
                //}
                int[,] pHist = new int[1, nLevels];
                int[] pnLevels = new int[1];
                int[] pnLowerLevel = new int[1];
                int[] pnUpperLevel = new int[1];

                pnLevels[0] = nLevels;
                pnLowerLevel[0] = nLowerLevel;
                pnUpperLevel[0] = nUpperLevel;

                HistogramEven(srcBitmap, pHist, pLevels, pnLevels, pnLowerLevel, pnUpperLevel);

                return pHist;
            }
            else if (pixelType == PixelFormatType.P16u_C3)
            {
                int[,] p16uLevels = new int[3, nLevels];
                for (int i = 0; i < nLevels; ++i)
                {
                    p16uLevels[0, i] = i;
                    p16uLevels[1, i] = i;
                    p16uLevels[2, i] = i;
                }
                int[,] p16uHist = new int[3, nLevels];
                int[] pn16uLevels = new int[3];
                int[] pn16uLowerLevel = new int[3];
                int[] pn16uUpperLevel = new int[3];
                for (int i = 0; i < 3; ++i)
                {
                    pn16uLevels[i] = nLevels;
                    pn16uLowerLevel[i] = nLowerLevel;
                    pn16uUpperLevel[i] = nUpperLevel;
                }

                HistogramEven(srcBitmap, p16uHist, p16uLevels, pn16uLevels, pn16uLowerLevel, pn16uUpperLevel);

                return p16uHist;
            }

            return null;
        }

        public static int GetHistogramPeak(WriteableBitmap srcBitmap)
        {
            if (srcBitmap == null)
            {
                throw new ArgumentNullException("GetHistogramPeak parameter is null");
            }

            if (srcBitmap.Format.BitsPerPixel != 16)
            {
                throw new ArgumentException("GetHistogramPeak parameter is not 16-bit image.");
            }

            const int nBins = 3000;
            const int nLevels = nBins + 1;
            int nLowerLevel = 0;
            int nUpperLevel = 65536;

            int[,] pHist = GetHistogramEven(srcBitmap, nLevels, nLowerLevel, nUpperLevel);

            // Get histogram highest peak
            int histoMax = 0;
            int histoMaxIndex = 0;
            for (int i = 0; i < nLevels; i++)
            {
                if ((int)pHist[0, i] > histoMax)
                {
                    histoMax = (int)pHist[0, i];
                    histoMaxIndex = i;
                }
            }

            // Scale the histogram peak to 65535
            histoMaxIndex = (int)((double)histoMaxIndex * ((double)65535.0 / (double)nBins));

            return histoMaxIndex;
        }

        #endregion //Image Scaling


        /*public static unsafe WriteableBitmap[] GetChannel(WriteableBitmap srcBitmap)
        {
            if (srcBitmap == null)
            {
                return null;
            }

            PixelFormat srcPixelFormat = srcBitmap.Format;
            if (srcPixelFormat != PixelFormats.Bgr24 &&
                srcPixelFormat != PixelFormats.Rgb24 &&
                srcPixelFormat != PixelFormats.Rgb48)
            {
                return null;
            }

            int iWidth = srcBitmap.PixelWidth;
            int iHeight = srcBitmap.PixelHeight;
            int iSrcBufferWidth = srcBitmap.BackBufferStride;
            WriteableBitmap[] retBitmap = { null, null, null };

            WriteableBitmap redChBitmap;
            WriteableBitmap greenChBitmap;
            WriteableBitmap blueChBitmap;

            int iDstStep = 0;
            int iSrcStep = ((iWidth * srcPixelFormat.BitsPerPixel) + 31) / 32 * 4;
            byte* pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();

            PixelFormat dstPixelFormat;
            if (srcBitmap.Format == PixelFormats.Bgr24)
            {
                dstPixelFormat = PixelFormats.Gray8;
            }
            else if (srcBitmap.Format == PixelFormats.Rgb48)
            {
                dstPixelFormat = PixelFormats.Gray16;
            }
            else
            {
                return null;
            }

            //int iDstStep = ((iWidth * dstPixelFormat.BitsPerPixel) + 31) / 32 * 4;

            try
            {
                redChBitmap = new WriteableBitmap(iWidth, iHeight, 300, 300, dstPixelFormat, null);
                greenChBitmap = new WriteableBitmap(iWidth, iHeight, 300, 300, dstPixelFormat, null);
                blueChBitmap = new WriteableBitmap(iWidth, iHeight, 300, 300, dstPixelFormat, null);

                iDstStep = redChBitmap.BackBufferStride;

                byte* pRedChData = (byte*)(void*)redChBitmap.BackBuffer.ToPointer();
                byte* pGreenChData = (byte*)(void*)greenChBitmap.BackBuffer.ToPointer();
                byte* pBlueChData = (byte*)(void*)blueChBitmap.BackBuffer.ToPointer();

                if (srcBitmap.Format == PixelFormats.Bgr24 || srcBitmap.Format == PixelFormats.Rgb24)
                {
                    byte* pSrc = null;
                    for (int iRow = 0; iRow < iHeight; iRow++)
                    {
                        pSrc = pSrcData + (iRow * iSrcBufferWidth);
                        for (int iCol = 0; iCol < iWidth; iCol++)
                        {
                            *pRedChData++ = *pSrc++;
                            *pGreenChData++ = *pSrc++;
                            *pBlueChData++ = *pSrc++;
                        }
                    }
                }
                else if (srcBitmap.Format == PixelFormats.Rgb48)
                {
                    ushort* pSrc16;
                    ushort* pRedChData16 = (ushort*)pRedChData;
                    ushort* pGreenChData16 = (ushort*)pGreenChData;
                    ushort* pBlueChData16 = (ushort*)pBlueChData;

                    for (int iRow = 0; iRow < iHeight; iRow++)
                    {
                        pSrc16 = (ushort*)(pSrcData + (iRow * iSrcBufferWidth));

                        for (int iCol = 0; iCol < iWidth; iCol++)
                        {
                            *pRedChData16++ = *pSrc16++;
                            *pGreenChData16++ = *pSrc16++;
                            *pBlueChData16++ = *pSrc16++;

                        }
                    }
                }

                retBitmap[0] = redChBitmap;
                retBitmap[1] = greenChBitmap;
                retBitmap[2] = blueChBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetChannel: " + ex.Message);
                throw ex;
            }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

            return retBitmap;
        }*/

        /*public static unsafe WriteableBitmap SetChannel(WriteableBitmap srcRedCh, WriteableBitmap srcGreenCh, WriteableBitmap srcBlueCh)
        {
            if (srcRedCh == null && srcGreenCh == null && srcBlueCh == null)
                return null;

            int iPixelType = 0;
            int iWidth = 0, iHeight = 0, iSrcStep = 0, iDstStep = 0;
            PixelFormat pixelFormat;
            byte* srcRData = null;
            byte* srcGData = null;
            byte* srcBData = null;
            WriteableBitmap resImage = null;

            try
            {
                if (srcRedCh != null)
                {
                    // Reserve the back buffer for updates.
                    srcRedCh.Lock();

                    iPixelType = GetPixelType(srcRedCh.Format);
                    if (iPixelType != 1 && iPixelType != 2)
                        return null;
                    iWidth = srcRedCh.PixelWidth;
                    iHeight = srcRedCh.PixelHeight;
                    iSrcStep = ((srcRedCh.PixelWidth * srcRedCh.Format.BitsPerPixel) + 31) / 32 * 4;
                    srcRData = (byte*)(void*)srcRedCh.BackBuffer.ToPointer();
                }

                if (srcGreenCh != null)
                {
                    // Reserve the back buffer for updates.
                    srcGreenCh.Lock();

                    iPixelType = GetPixelType(srcGreenCh.Format);
                    if (iPixelType != 1 && iPixelType != 2)
                        return null;
                    iWidth = srcGreenCh.PixelWidth;
                    iHeight = srcGreenCh.PixelHeight;
                    iSrcStep = ((srcGreenCh.PixelWidth * srcGreenCh.Format.BitsPerPixel) + 31) / 32 * 4;
                    srcGData = (byte*)(void*)srcGreenCh.BackBuffer.ToPointer();
                }

                if (srcBlueCh != null)
                {
                    // Reserve the back buffer for updates.
                    srcBlueCh.Lock();

                    iPixelType = GetPixelType(srcBlueCh.Format);
                    if (iPixelType != 1 && iPixelType != 2)
                        return null;
                    iWidth = srcBlueCh.PixelWidth;
                    iHeight = srcBlueCh.PixelHeight;
                    iSrcStep = ((srcBlueCh.PixelWidth * srcBlueCh.Format.BitsPerPixel) + 31) / 32 * 4;
                    srcBData = (byte*)(void*)srcBlueCh.BackBuffer.ToPointer();
                }

                if (iPixelType == 1)
                {
                    iDstStep = ((iWidth * PixelFormats.Bgr24.BitsPerPixel) + 31) / 32 * 4;
                    pixelFormat = PixelFormats.Bgr24;
                    resImage = new WriteableBitmap(iWidth, iHeight, 300, 300, pixelFormat, null);
                    
                    byte * dstData = (byte*)(void*)resImage.BackBuffer.ToPointer();

                    byte* pSrcRData = null;
                    byte* pSrcGData = null;
                    byte* pSrcBData = null;
                    byte* pDstData = null;

                    for (int iRow = 0; iRow < iHeight; iRow++)
                    {
                        pSrcRData = (srcRData != null) ? srcRData + iRow * iSrcStep : null;
                        pSrcGData = (srcGData != null) ? srcGData + iRow * iSrcStep : null;
                        pSrcBData = (srcBData != null) ? srcBData + iRow * iSrcStep : null;
                        pDstData = dstData + iRow * iDstStep;

                        for (int iCol = 0; iCol < iWidth; iCol++)
                        {
                            //*pDstData++ = (pSrcRData != null) ? *pSrcRData++ : (byte)0;
                            //*pDstData++ = (pSrcGData != null) ? *pSrcGData++ : (byte)0;
                            //*pDstData++ = (pSrcBData != null) ? *pSrcBData++ : (byte)0;

                            //BGR color order
                            *pDstData++ = (pSrcBData != null) ? *pSrcBData++ : (byte)0;
                            *pDstData++ = (pSrcGData != null) ? *pSrcGData++ : (byte)0;
                            *pDstData++ = (pSrcRData != null) ? *pSrcRData++ : (byte)0;

                        }
                    }
                }
                else
                {
                    iDstStep = ((iWidth * PixelFormats.Rgb48.BitsPerPixel) + 31) / 32 * 4;
                    pixelFormat = PixelFormats.Rgb48;
                    resImage = new WriteableBitmap(iWidth, iHeight, 300, 300, pixelFormat, null);

                    ushort* dstData = (ushort*)(void*)resImage.BackBuffer.ToPointer();

                    ushort* pSrcRData16 = null;
                    ushort* pSrcGData16 = null;
                    ushort* pSrcBData16 = null;
                    ushort* pDstData16 = null;

                    for (int iRow = 0; iRow < iHeight; iRow++)
                    {
                        pSrcRData16 = (srcRData != null) ? (ushort*)(srcRData + iRow * iSrcStep) : null;
                        pSrcGData16 = (srcGData != null) ? (ushort*)(srcGData + iRow * iSrcStep) : null;
                        pSrcBData16 = (srcBData != null) ? (ushort*)(srcBData + iRow * iSrcStep) : null;
                        pDstData16 = dstData + iRow * iDstStep;

                        for (int iCol = 0; iCol < iWidth; iCol++)
                        {
                            //*pDstData16++ = (srcRData != null) ? *pSrcBData16++ : (ushort)0;
                            //*pDstData16++ = (srcGData != null) ? *pSrcGData16++ : (ushort)0;
                            //*pDstData16++ = (srcBData != null) ? *pSrcRData16++ : (ushort)0;

                            //BGR color order
                            *pDstData16++ = (srcBData != null) ? *pSrcRData16++ : (ushort)0;
                            *pDstData16++ = (srcGData != null) ? *pSrcGData16++ : (ushort)0;
                            *pDstData16++ = (srcRData != null) ? *pSrcBData16++ : (ushort)0;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                // Release the back buffer and make it available for display.
                if (srcRedCh != null) { srcRedCh.Unlock(); }
                if (srcGreenCh != null) { srcGreenCh.Unlock(); }
                if (srcBlueCh != null) { srcBlueCh.Unlock(); }
            }

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return resImage;
        }*/


        //
        //Divide the source image by the flat image and scale it to 65000.
        //
        /*public static unsafe WriteableBitmap ImageDivAndScale(WriteableBitmap srcImage, WriteableBitmap flatImage, int upperLimit, int offsetX = 0, int offsetY = 0)
        {
            if (srcImage == null || flatImage == null)
                return null;

            if (IppImaging.GetPixelFormatType(srcImage.Format) != PixelFormatType.P16u_C1)
            {
                throw new Exception("Source image format not supported");
            }

            int srcWidth = srcImage.PixelWidth;
            int srcHeight = srcImage.PixelHeight;
            int bitsPerPixel = srcImage.Format.BitsPerPixel;

            byte* pSrc = (byte*)srcImage.BackBuffer.ToPointer();
            byte* pFlat = (byte*)flatImage.BackBuffer.ToPointer();
            int srcStep = srcImage.BackBufferStride;
            int flatStep = flatImage.BackBufferStride;

            float[] srcCvtData = null;
            float[] flatCvtData = null;
            float[] dst32fData = null;
            float* pSrcCvt = null;
            float* pFlatCvt = null;
            float* pDst32f = null;
            int srcCvtStep = srcWidth * sizeof(float);
            int flatCvtStep = srcCvtStep;
            int dst32fStep = srcCvtStep;
            IppiSize roiSize = new IppiSize(srcWidth, srcHeight);

            try
            {
                srcCvtData = new float[srcWidth * srcHeight];
                flatCvtData = new float[srcWidth * srcHeight];
                dst32fData = new float[srcWidth * srcHeight];

                fixed (float* p = srcCvtData) pSrcCvt = p;
                fixed (float* p = flatCvtData) pFlatCvt = p;
                fixed (float* p = dst32fData) pDst32f = p;

                IppImaging.Convert_16u32f_C1R(pSrc, srcStep, pSrcCvt, srcCvtStep, new IppiSize(srcWidth, srcHeight));
                IppImaging.Convert_16u32f_C1R(pFlat, flatStep, pFlatCvt, flatCvtStep, new IppiSize(srcWidth, srcHeight));

                IppImaging.Div(pFlatCvt, flatCvtStep, pSrcCvt, srcCvtStep, pDst32f, dst32fStep, roiSize);

                float pixelMax = 0;

                IppiSize offsettedRoiSize = new IppiSize(srcWidth - (2 * offsetX), srcHeight - (2 * offsetY));
                IppImaging.Max(pDst32f + ((srcWidth * offsetY) + offsetX), dst32fStep, offsettedRoiSize, ref pixelMax);
                float scaledFactor = (float)upperLimit / pixelMax;

                IppImaging.MulC(pDst32f, dst32fStep, scaledFactor, pDst32f, dst32fStep, roiSize);

                IppImaging.Convert_32f16u_C1R(pDst32f, dst32fStep, pSrc, srcStep, srcWidth, srcHeight);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                srcCvtData = null;
                flatCvtData = null;
                dst32fData = null;

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.Collect();
            }

            return srcImage;
        }*/

        /// <summary>
        /// image edge
        /// </summary>
        /// <param name="bitmap">src data</param>
        /// <returns>dst data</returns>
        /***public static BitmapSource Edge(BitmapSource bitmap)
        {
            if (bitmap == null)
                return null;

            BitmapSource retBitmap;
            System.Drawing.Bitmap tmpBitmap = BitmapImage2Bitmap(bitmap);
            System.Drawing.Bitmap originalImage = EdgeProcess(tmpBitmap);
            retBitmap = Bitmap2BitmapImage(originalImage);
            return retBitmap;
        }***/
        //public static Blob[] blobs = null;
        /// <summary>
        /// image blob
        /// </summary>
        /// <param name="bitmap">src data</param>
        /// <returns>dst data</returns>
        /***public static BitmapSource Blob(BitmapSource bitmap)
        {
            if (bitmap == null)
                return null;

            BitmapSource retBitmap;
            System.Drawing.Bitmap tmpBitmap = BitmapImage2Bitmap(bitmap);
            System.Drawing.Bitmap originalImage = BlobProcess(tmpBitmap);
            retBitmap = Bitmap2BitmapImage(originalImage);
            return retBitmap;
        }***/
        public static System.Drawing.Bitmap BitmapImage2Bitmap(BitmapSource bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                // return bitmap; <-- leads to problems, stream is closed/closing ...
                return new System.Drawing.Bitmap(bitmap);
            }
        }
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        private static System.Windows.Media.Imaging.BitmapSource Bitmap2BitmapImage(System.Drawing.Bitmap source)
        {
            try
            {
                var hBitmap = source.GetHbitmap();
                var result = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(hBitmap);
                result.Freeze();
                return result;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return null;
            }
        }
        /***private static System.Drawing.Bitmap BlobProcess(System.Drawing.Bitmap image)//, IImageProcessingLog log)
        {
            System.Drawing.Bitmap grayImage = image;
            //Blob[] blobs;

            Grayscale gfilter = new Grayscale(0.2125, 0.7154, 0.0721);
            //  apply the filter
            if (image.PixelFormat != System.Drawing.Imaging.PixelFormat.Format16bppGrayScale && image.PixelFormat != System.Drawing.Imaging.PixelFormat.Format8bppIndexed)
                grayImage = gfilter.Apply(image);


            // Threshold edges
            // create filter
            OtsuThreshold filter = new OtsuThreshold();
            // apply the filter
            filter.ApplyInPlace(grayImage);
            // check threshold value
            int aforgeThresh = filter.ThresholdValue;

            Threshold thresholdFilter = new Threshold(aforgeThresh);

            thresholdFilter.ApplyInPlace(grayImage);

            // return grayImage;

            // 2 - Edge detection
            DifferenceEdgeDetector edgeDetector = new DifferenceEdgeDetector();
            System.Drawing.Bitmap edges = edgeDetector.Apply(grayImage);
            //log.AddImage("Edges", edges);


            //return image;
            //return grayImage;
            //log.AddImage("Thresholded Edges", edges);

            // 4 - Blob Counter
            BlobCounter blobCounter = new BlobCounter();
            blobCounter.MinHeight = 32;
            blobCounter.MinWidth = 32;
            blobCounter.FilterBlobs = true;
            blobCounter.ObjectsOrder = ObjectsOrder.Size;

            blobCounter.ProcessImage(edges);
            blobs = blobCounter.GetObjectsInformation();
            return grayImage;

        }***/
        /***private static System.Drawing.Bitmap EdgeProcess(System.Drawing.Bitmap image)//, IImageProcessingLog log)
        {
            System.Drawing.Bitmap grayImage = image;
            Blob[] blobs;

            Grayscale gfilter = new Grayscale(0.2125, 0.7154, 0.0721);
            //  apply the filter
            if (image.PixelFormat != System.Drawing.Imaging.PixelFormat.Format16bppGrayScale && image.PixelFormat != System.Drawing.Imaging.PixelFormat.Format8bppIndexed)
                grayImage = gfilter.Apply(image);


            // Threshold edges
            // create filter
            OtsuThreshold filter = new OtsuThreshold();
            // apply the filter
            filter.ApplyInPlace(grayImage);
            // check threshold value
            int aforgeThresh = filter.ThresholdValue;

            Threshold thresholdFilter = new Threshold(aforgeThresh);

            thresholdFilter.ApplyInPlace(grayImage);

            // 2 - Edge detection
            DifferenceEdgeDetector edgeDetector = new DifferenceEdgeDetector();
            System.Drawing.Bitmap edges = edgeDetector.Apply(grayImage);
            //log.AddImage("Edges", edges);

            //return image;
            //return grayImage;
            //log.AddImage("Thresholded Edges", edges);

            // 4 - Blob Counter
            BlobCounter blobCounter = new BlobCounter();
            blobCounter.MinHeight = 32;
            blobCounter.MinWidth = 32;
            blobCounter.FilterBlobs = true;
            blobCounter.ObjectsOrder = ObjectsOrder.Size;

            blobCounter.ProcessImage(edges);
            blobs = blobCounter.GetObjectsInformation();

            return edges;
        }***/

        public static unsafe void HistogramRange(WriteableBitmap srcBitmap, int[,] pHist, int[,] pLevels, int[] nLevels)
        {
            if (srcBitmap == null)
                return;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            byte* pSrc = null;
            int iWidth = srcBitmap.PixelWidth;
            int iHeight = srcBitmap.PixelHeight;
            int iSrcStep = ((srcBitmap.PixelWidth * srcBitmap.Format.BitsPerPixel) + 31) / 32 * 4;

            pSrc = (byte*)srcBitmap.BackBuffer.ToPointer();

            fixed (int* fpHist = pHist, fpLevels = pLevels)
            {
                int col = pHist.GetUpperBound(1) + 1;
                int*[] fpHistAll = new int*[col];
                int*[] fpLevelsAll = new int*[col];
                for (int i = 0; i < col; ++i)
                {
                    fpHistAll[i] = fpHist + i * col;
                    fpLevelsAll[i] = fpLevels + i * col;
                }

                IppiSize roiSize = new IppiSize(iWidth, iHeight);

                IppImaging.HistogramRange(pSrc, iSrcStep, pixelFormat, roiSize, fpLevelsAll, fpLevelsAll, nLevels);
            }
        }

        private static int[,] GetHistogram(BitmapSource bitmap)
        {
            if (bitmap == null)
                return null;

            WriteableBitmap srcBitmap = new WriteableBitmap(bitmap);

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            if (pixelFormat == PixelFormatType.P8u_C1)
            {
                int[,] p8uLevels = new int[1, 256];
                for (int i = 0; i < 256; ++i)
                {
                    p8uLevels[0, i] = i;
                }
                int[,] p8uHist = new int[1, 256];
                int[] pn8uLevels = new int[1];
                for (int i = 0; i < 1; ++i)
                {
                    pn8uLevels[i] = 256;
                }
                HistogramRange(srcBitmap, p8uHist, p8uLevels, pn8uLevels);

                return p8uHist;
            }
            else if (pixelFormat == PixelFormatType.P8u_C3)
            {
                int[,] p8uLevels = new int[3, 256];
                for (int i = 0; i < 256; ++i)
                {
                    p8uLevels[0, i] = i;
                    p8uLevels[1, i] = i;
                    p8uLevels[2, i] = i;
                }
                int[,] p8uHist = new int[3, 256];
                int[] pn8uLevels = new int[3];
                for (int i = 0; i < 3; ++i)
                {
                    pn8uLevels[i] = 256;
                }
                HistogramRange(srcBitmap, p8uHist, p8uLevels, pn8uLevels);

                return p8uHist;
            }
            else if (pixelFormat == PixelFormatType.P8u_C4)
            {
                int[,] p8uLevels = new int[4, 256];
                for (int i = 0; i < 256; ++i)
                {
                    p8uLevels[0, i] = i;
                    p8uLevels[1, i] = i;
                    p8uLevels[2, i] = i;
                    p8uLevels[3, i] = i;
                }
                int[,] p8uHist = new int[4, 256];
                int[] pn8uLevels = new int[4];
                for (int i = 0; i < 4; ++i)
                {
                    pn8uLevels[i] = 256;
                }
                HistogramRange(srcBitmap, p8uHist, p8uLevels, pn8uLevels);

                return p8uHist;
            }
            else if (pixelFormat == PixelFormatType.P16u_C1)
            {
                int[,] pLevels = new int[1, 65536];
                for (int i = 0; i < 65536; ++i)
                {
                    pLevels[0, i] = i;
                }
                int[,] pHist = new int[1, 65536];
                int[] pnLevels = new int[1];
                for (int i = 0; i < 1; ++i)
                {
                    pnLevels[i] = 65536;
                }
                HistogramRange(srcBitmap, pHist, pLevels, pnLevels);

                return pHist;
            }
            else if (pixelFormat == PixelFormatType.P16u_C3)
            {
                int[,] p16uLevels = new int[3, 65536];
                for (int i = 0; i < 65536; ++i)
                {
                    p16uLevels[0, i] = i;
                    p16uLevels[1, i] = i;
                    p16uLevels[2, i] = i;
                }
                int[,] p16uHist = new int[3, 65536];
                int[] pn16uLevels = new int[3];
                for (int i = 0; i < 3; ++i)
                {
                    pn16uLevels[i] = 65536;
                }
                HistogramRange(srcBitmap, p16uHist, p16uLevels, pn16uLevels);

                return p16uHist;
            }
            else if (pixelFormat == PixelFormatType.P16u_C4)
            {
                int[,] p16uLevels = new int[4, 65536];
                for (int i = 0; i < 65536; ++i)
                {
                    p16uLevels[0, i] = i;
                    p16uLevels[1, i] = i;
                    p16uLevels[2, i] = i;
                    p16uLevels[3, i] = i;
                }
                int[,] p16uHist = new int[4, 65536];
                int[] pn16uLevels = new int[4];
                for (int i = 0; i < 4; ++i)
                {
                    pn16uLevels[i] = 65536;
                }
                HistogramRange(srcBitmap, p16uHist, p16uLevels, pn16uLevels);

                return p16uHist;
            }

            return null;
        }

        private static unsafe int GetThreshold(int w, int h, int[,] integerHistogram)
        {
            // get start and stop X-Y coordinates
            int startX = 0;
            int startY = 0;
            int stopX = w;
            int stopY = h;
            int calculatedThreshold = 0;

            if (integerHistogram == null) return 0;

            double[] histogram = new double[65536];

            // pixels count in the processing region
            int pixelCount = (stopX - startX) * (stopY - startY);
            // mean value of the processing region
            double imageMean = 0;

            for (int i = 0; i < 65536; i++)
            {
                histogram[i] = (double)integerHistogram[0, i] / pixelCount;
                imageMean += histogram[i] * i;
            }

            unsafe
            {
                double max = double.MinValue;

                // initial class probabilities
                double class1Probability = 0;
                double class2Probability = 1;

                // initial class 1 mean value
                double class1MeanInit = 0;

                // check all thresholds
                for (int t = 0; (t < 65536) && (class2Probability > 0); t++)
                {
                    // calculate class means for the given threshold
                    double class1Mean = class1MeanInit;
                    double class2Mean = (imageMean - (class1Mean * class1Probability)) / class2Probability;

                    // calculate between class variance
                    double betweenClassVariance = (class1Probability) * (1.0 - class1Probability) * Math.Pow(class1Mean - class2Mean, 2);

                    // check if we found new threshold candidate
                    if (betweenClassVariance > max)
                    {
                        max = betweenClassVariance;
                        calculatedThreshold = t;
                    }

                    // update initial probabilities and mean value
                    class1MeanInit *= class1Probability;

                    class1Probability += histogram[t];
                    class2Probability -= histogram[t];

                    class1MeanInit += (double)t * (double)histogram[t];

                    if (class1Probability != 0)
                    {
                        class1MeanInit /= class1Probability;
                    }
                }
            }

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return calculatedThreshold;
        }

        /***public static int GetMinimumPixelInBlob(BitmapSource bitmap)
        {
            //int minX=0, minY=0;
            int largestBlob = 0;
            int largestArea = 0;
            int minPixel = 65535;
            int tmpPixel;

            int pos = 0;
            int[,] pHist = GetHistogram(bitmap);
            int threshold = GetThreshold(bitmap.PixelWidth, bitmap.PixelHeight, pHist);
            MVImage.Blob(bitmap);

            if (blobs == null)
            {
                return 0;
            }

            if (bitmap.Format == PixelFormats.Gray16)
            {
                ushort[] pSrcData = null;
                int iSrcStep = ((bitmap.PixelWidth * (bitmap.Format.BitsPerPixel / 2)) + 31) / 32 * 4;
                pSrcData = new ushort[bitmap.PixelHeight * iSrcStep];
                bitmap.CopyPixels(pSrcData, iSrcStep * 2, 0);

                if (blobs.Count() > 0)
                {
                    largestArea = blobs[0].Area;
                    for (int ii = 1, n = blobs.Length; ii < n; ii++)
                    {
                        if (blobs[ii].Area > largestArea) largestBlob = ii;
                    }
                    // Iterate through the image pixels in the largest blob

                    int x = blobs[largestBlob].Rectangle.X;
                    int y = blobs[largestBlob].Rectangle.Y;
                    int w = blobs[largestBlob].Rectangle.Width;
                    int h = blobs[largestBlob].Rectangle.Height;
                    for (int i = y; i < h; i++)
                    {
                        for (int j = x; j < w; j++)
                        {
                            pos = i * iSrcStep + j;
                            tmpPixel = ((ushort[])pSrcData)[pos];
                            if (tmpPixel >= threshold && tmpPixel < minPixel)
                            {

                                minPixel = tmpPixel;
                                //minX = j;
                                //minY = i;
                            }
                        }
                    }
                }
            }

            return minPixel;
        }***/

        public static int GetThreshold(WriteableBitmap srcBitmap)
        {
            int threshold = 0;
            uint imageMin = 0;
            uint imageMax = 0;
            int nLevels = 1024;

            Rect roiRect = new Rect(0, 0, srcBitmap.PixelWidth, srcBitmap.PixelHeight);

            MinMax(srcBitmap, roiRect, ref imageMin, ref imageMax);

            // get the image histogram
            int[,] pHist = HistogramEven(srcBitmap, nLevels, (int)imageMin, (int)imageMax);

            //
            // find the peaks in the histogram: make the first and last one the peak
            //
            List<Peaks> peaks = new List<Peaks>();
            peaks.Add(new Peaks(0, pHist[0, 0]));
            peaks.Add(new Peaks(nLevels - 1, pHist[0, nLevels - 1]));

            for (int i = 1; i < nLevels - 1; i++)
            {
                if (pHist[0, i] > pHist[0, i - 1] && pHist[0, i] > pHist[0, i + 1])
                {
                    peaks.Add(new Peaks(i, pHist[0, i]));
                }
            }

            // sort the peaks by value
            peaks.Sort(delegate (Peaks p1, Peaks p2)
            {
                return p2.value.CompareTo(p1.value);
            });

            //
            // find the intensity value of the lowest bin between the 2 largest peaks.
            //
            int index1 = peaks[0].index;
            int index2 = peaks[1].index;
            int start = (index1 > index2) ? index2 : index1;
            int end = (index1 > index2) ? index1 : index2;

            //
            // find the index of the min value within the range
            //
            int minIndex = start;
            int minValue = pHist[0, start];
            for (int i = start + 1; i <= end; i++)
            {
                if (pHist[0, i] <= minValue)
                {
                    minValue = pHist[0, i];
                    minIndex = i;
                }
            }

            // calculate the threshold
            threshold = (int)Math.Round((imageMin + (imageMax - imageMin) / (double)nLevels * (minIndex + 1)));

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return threshold;
        }

        /// <summary>
        /// Rank filter the chemi capture.
        /// Replicate border and uses median in substitution.
        /// </summary>
        /// <param name="srcImage"></param>
        /// <param name="kernel">value of half the kernel</param>
        /// <returns></returns>
        /*public static unsafe WriteableBitmap RankFilter(WriteableBitmap srcImage, int kernel)
        {
            if (kernel == 0)
            {
                return srcImage;
            }
            else
            {
                kernel = 2 * kernel + 1;
            }

            // Replicate border pixels
            int borderSize = kernel / 2;
            WriteableBitmap cpyBorder = MVImage.CopyReplicateBorder(srcImage, borderSize);
            //WriteableBitmap cpyBorder = MVImage.CopyConstBorder(srcImage, borderSize, 0);

            // copy of the original source image
            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            int imageWidth = cpyBorder.PixelWidth;
            int imageHeight = cpyBorder.PixelHeight;
            int origImageWidth = srcImage.PixelWidth;
            double mean = 0.0;
            double stdDev = 0.0;
            Int64 sumOfSquare = 0;
            double testVal = 0.0;
            //ushort min = 65535;
            double median = 0.0;
            int populationSize = kernel * kernel - 1;
            ushort[] pixelPopulation = new ushort[populationSize];
            int localTargetIndex = populationSize / 2;
            int pixelIndex = 0;
            int popIndex = 0;
            bool bIsPixelIndex = false;

            // Reserve the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();
            cpyBorder.Lock();

            // Get a pointer to the back buffer.
            ushort* pOrigData = (ushort*)srcImage.BackBuffer.ToPointer();
            ushort* pCopyOfData = (ushort*)cpyImage.BackBuffer.ToPointer();
            ushort* pCpyBorder = (ushort*)cpyBorder.BackBuffer.ToPointer();

            fixed (void* pPtr = &pixelPopulation[0])
            {
                ushort* pPopulation = (ushort*)pPtr;

                for (int iRow = 1; iRow <= imageHeight - kernel; ++iRow)
                {
                    for (int iCol = 1; iCol <= imageWidth - kernel; ++iCol)
                    {
                        //
                        // Flatten the 2-D loop (the looping over kernelSize x kernelSize matrix) around target pixel into a 1-D array 
                        // Note: Use of pointers is quicker than array indexing due to the bounds checking .NET 
                        // languages use:
                        //
                        //*(pPopulation) = *(pOrigData + (iCol - 1) + imageWidth * (iRow - 1));
                        //*(pPopulation + 1) = *(pOrigData + iCol + imageWidth * (iRow - 1));
                        //*(pPopulation + 2) = *(pOrigData + (iCol + 1) + imageWidth * (iRow - 1));

                        //*(pPopulation + 3) = *(pOrigData + (iCol - 1) + imageWidth * iRow);
                        ////Filtered (target) pixel would go here if we were using it.
                        //*(pPopulation + 4) = *(pOrigData + (iCol + 1) + imageWidth * iRow);

                        //*(pPopulation + 5) = *(pOrigData + (iCol - 1) + imageWidth * (iRow + 1));
                        //*(pPopulation + 6) = *(pOrigData + iCol + imageWidth * (iRow + 1));
                        //*(pPopulation + 7) = *(pOrigData + (iCol + 1) + imageWidth * (iRow + 1));

                        pixelIndex = 0;
                        popIndex = 0;
                        bIsPixelIndex = false;

                        for (int i = 1; i <= kernel; i++)
                        {
                            for (int j = 1; j <= kernel; j++)
                            {
                                if (popIndex != localTargetIndex || (popIndex == localTargetIndex && bIsPixelIndex))
                                {
                                    *(pPopulation + popIndex++) = *(pCpyBorder + (iCol + (j - 2) + imageWidth * ((iRow - 1) + (i - 1))));
                                    bIsPixelIndex = false;
                                }
                                else
                                {
                                    pixelIndex = iCol - 1 + origImageWidth * (iRow - 1);
                                    bIsPixelIndex = true;
                                }
                            }
                        }

                        mean = 0.0;
                        sumOfSquare = 0;
                        //min = 65535;
                        median = 0.0;

                        for (int i = 0; i < populationSize; ++i)
                        {
                            mean += *(pPopulation + i);
                            sumOfSquare += (Int64)(*(pPopulation + i)) * (*(pPopulation + i));
                            //if (*(pPopulation + i) < min)
                            //{
                            //    min = *(pPopulation + i);
                            //}
                        }

                        mean = mean / populationSize;
                        //Array.Sort(pixelPopulation);
                        //int mid = populationSize / 2;
                        //median = (double)(pixelPopulation[mid - 1] + pixelPopulation[mid]) / 2.0;    // zero index
                        //double min = pixelPopulation[0];
                        stdDev = Math.Sqrt((sumOfSquare - (populationSize * (mean * mean))) / (populationSize - 1)); //N-1 for "sample standard deviation"

                        // Don't bother to use this algorithm unless the number of values is large. 
                        int k = 0;
                        QuickMedian(pixelPopulation, populationSize, ref k);
                        median = pixelPopulation[k];

                        testVal = *(pOrigData + pixelIndex) - median;

                        if (testVal > 50)    // pixel counts rank threshold
                        {
                            *(pCopyOfData + pixelIndex) = (ushort)median;
                        }
                    }
                }
            } //end fixed

            srcImage.Unlock();
            cpyImage.Unlock();
            cpyBorder.Unlock();

            return cpyImage;
        }*/

        /// <summary>
        /// Rank filter for RGB and NIR capture.
        /// Replicate border and uses median in substitution.
        /// </summary>
        /// <param name="srcImage"></param>
        /// <param name="kernel">value of half the kernel</param>
        /// <returns></returns>
        public static unsafe WriteableBitmap RankRGBFilter(WriteableBitmap srcImage, int kernel)
        {
            if (kernel == 0)
            {
                return srcImage;
            }
            else
            {
                kernel = 2 * kernel + 1;
            }

            // Replicate border pixels
            int borderSize = kernel / 2;
            WriteableBitmap cpyBorder = CopyReplicateBorder(srcImage, borderSize);
            //WriteableBitmap cpyBorder = MVImage.CopyConstBorder(srcImage, borderSize, 0);

            // copy of the original source image
            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            int imageWidth = cpyBorder.PixelWidth;
            int imageHeight = cpyBorder.PixelHeight;
            int origImageWidth = srcImage.PixelWidth;
            //double mean = 0.0;
            //double stdDev = 0.0;
            //Int64 sumOfSquare = 0;
            //double testVal = 0.0;
            //ushort min = 65535;
            double median = 0.0;
            int populationSize = kernel * kernel - 1;
            ushort[] pixelPopulation = new ushort[populationSize];
            int localTargetIndex = populationSize / 2;
            int pixelIndex = 0;
            int popIndex = 0;
            bool bIsPixelIndex = false;

            // Reserve the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();
            cpyBorder.Lock();

            // Get a pointer to the back buffer.
            ushort* pOrigData = (ushort*)srcImage.BackBuffer.ToPointer();
            ushort* pCopyOfData = (ushort*)cpyImage.BackBuffer.ToPointer();
            ushort* pCpyBorder = (ushort*)cpyBorder.BackBuffer.ToPointer();

            fixed (void* pPtr = &pixelPopulation[0])
            {
                ushort* pPopulation = (ushort*)pPtr;

                for (int iRow = 1; iRow <= imageHeight - kernel; ++iRow)
                {
                    for (int iCol = 1; iCol <= imageWidth - kernel; ++iCol)
                    {
                        //
                        // Flatten the 2-D loop (the looping over kernelSize x kernelSize matrix) around target pixel into a 1-D array 
                        // Note: Use of pointers is quicker than array indexing due to the bounds checking .NET 
                        // languages use:
                        //
                        //*(pPopulation) = *(pOrigData + (iCol - 1) + imageWidth * (iRow - 1));
                        //*(pPopulation + 1) = *(pOrigData + iCol + imageWidth * (iRow - 1));
                        //*(pPopulation + 2) = *(pOrigData + (iCol + 1) + imageWidth * (iRow - 1));

                        //*(pPopulation + 3) = *(pOrigData + (iCol - 1) + imageWidth * iRow);
                        ////Filtered (target) pixel would go here if we were using it.
                        //*(pPopulation + 4) = *(pOrigData + (iCol + 1) + imageWidth * iRow);

                        //*(pPopulation + 5) = *(pOrigData + (iCol - 1) + imageWidth * (iRow + 1));
                        //*(pPopulation + 6) = *(pOrigData + iCol + imageWidth * (iRow + 1));
                        //*(pPopulation + 7) = *(pOrigData + (iCol + 1) + imageWidth * (iRow + 1));

                        pixelIndex = 0;
                        popIndex = 0;
                        bIsPixelIndex = false;

                        for (int i = 1; i <= kernel; i++)
                        {
                            for (int j = 1; j <= kernel; j++)
                            {
                                if (popIndex != localTargetIndex || (popIndex == localTargetIndex && bIsPixelIndex))
                                {
                                    *(pPopulation + popIndex++) = *(pCpyBorder + (iCol + (j - 2) + imageWidth * ((iRow - 1) + (i - 1))));
                                    bIsPixelIndex = false;
                                }
                                else
                                {
                                    pixelIndex = iCol - 1 + origImageWidth * (iRow - 1);
                                    bIsPixelIndex = true;
                                }
                            }
                        }

                        //mean = 0.0;
                        //sumOfSquare = 0;
                        //min = 65535;
                        median = 0.0;

                        //for (int i = 0; i < populationSize; ++i)
                        //{
                        //    mean += *(pPopulation + i);
                        //    sumOfSquare += (Int64)(*(pPopulation + i)) * (*(pPopulation + i));
                        //    if (*(pPopulation + i) < min)
                        //    {
                        //        min = *(pPopulation + i);
                        //    }
                        //}

                        //mean = mean / populationSize;
                        //Array.Sort(pixelPopulation);
                        //int mid = populationSize / 2;
                        //median = (double)(pixelPopulation[mid - 1] + pixelPopulation[mid]) / 2.0;    // zero index
                        //double min = pixelPopulation[0];
                        //stdDev = Math.Sqrt((sumOfSquare - (populationSize * (mean * mean))) / (populationSize - 1)); //N-1 for "sample standard deviation"
                        //testVal = Math.Abs(*(pOrigData + targetPixelIndex) - median);

                        // Don't bother to use this algorithm unless the number of values is large. 
                        int k = 0;
                        QuickMedian(pixelPopulation, populationSize, ref k);
                        median = pixelPopulation[k];

                        if (*(pOrigData + pixelIndex) - median > 0)
                        {
                            *(pCopyOfData + pixelIndex) = (ushort)median;
                        }
                    }
                }
            } //end fixed

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();
            cpyBorder.Unlock();

            // Collect all generations of memory.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return cpyImage;
        }

        public static unsafe WriteableBitmap RankVisibleFilter(WriteableBitmap srcImage, int kernel)
        {
            if (kernel == 0)
            {
                return srcImage;
            }
            else
            {
                kernel = 2 * kernel + 1;
            }

            // Replicate border pixels
            int borderSize = kernel / 2;
            WriteableBitmap cpyBorder = CopyReplicateBorder(srcImage, borderSize);
            //WriteableBitmap cpyBorder = MVImage.CopyConstBorder(srcImage, borderSize, 0);

            // copy of the original source image
            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            int imageWidth = cpyBorder.PixelWidth;
            int imageHeight = cpyBorder.PixelHeight;
            int origImageWidth = srcImage.PixelWidth;
            //double mean = 0.0;
            //double stdDev = 0.0;
            //Int64 sumOfSquare = 0;
            //double testVal = 0.0;
            //ushort min = 65535;
            double median = 0.0;
            int populationSize = kernel * kernel - 1;
            ushort[] pixelPopulation = new ushort[populationSize];
            int localTargetIndex = populationSize / 2;
            int pixelIndex = 0;
            int popIndex = 0;
            bool bIsPixelIndex = false;

            // Reserve the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();
            cpyBorder.Lock();

            // Get a pointer to the back buffer.
            ushort* pOrigData = (ushort*)srcImage.BackBuffer.ToPointer();
            ushort* pCopyOfData = (ushort*)cpyImage.BackBuffer.ToPointer();
            ushort* pCpyBorder = (ushort*)cpyBorder.BackBuffer.ToPointer();

            fixed (void* pPtr = &pixelPopulation[0])
            {
                ushort* pPopulation = (ushort*)pPtr;

                for (int iRow = 1; iRow <= imageHeight - kernel; ++iRow)
                {
                    for (int iCol = 1; iCol <= imageWidth - kernel; ++iCol)
                    {
                        //
                        // Flatten the 2-D loop (the looping over kernelSize x kernelSize matrix) around target pixel into a 1-D array 
                        // Note: Use of pointers is quicker than array indexing due to the bounds checking .NET 
                        // languages use:
                        //
                        //*(pPopulation) = *(pOrigData + (iCol - 1) + imageWidth * (iRow - 1));
                        //*(pPopulation + 1) = *(pOrigData + iCol + imageWidth * (iRow - 1));
                        //*(pPopulation + 2) = *(pOrigData + (iCol + 1) + imageWidth * (iRow - 1));

                        //*(pPopulation + 3) = *(pOrigData + (iCol - 1) + imageWidth * iRow);
                        ////Filtered (target) pixel would go here if we were using it.
                        //*(pPopulation + 4) = *(pOrigData + (iCol + 1) + imageWidth * iRow);

                        //*(pPopulation + 5) = *(pOrigData + (iCol - 1) + imageWidth * (iRow + 1));
                        //*(pPopulation + 6) = *(pOrigData + iCol + imageWidth * (iRow + 1));
                        //*(pPopulation + 7) = *(pOrigData + (iCol + 1) + imageWidth * (iRow + 1));

                        pixelIndex = 0;
                        popIndex = 0;
                        bIsPixelIndex = false;

                        for (int i = 1; i <= kernel; i++)
                        {
                            for (int j = 1; j <= kernel; j++)
                            {
                                if (popIndex != localTargetIndex || (popIndex == localTargetIndex && bIsPixelIndex))
                                {
                                    *(pPopulation + popIndex++) = *(pCpyBorder + (iCol + (j - 2) + imageWidth * ((iRow - 1) + (i - 1))));
                                    bIsPixelIndex = false;
                                }
                                else
                                {
                                    pixelIndex = iCol - 1 + origImageWidth * (iRow - 1);
                                    bIsPixelIndex = true;
                                }
                            }
                        }

                        //mean = 0.0;
                        //sumOfSquare = 0;
                        //min = 65535;
                        median = 0.0;

                        //for (int i = 0; i < populationSize; ++i)
                        //{
                        //    mean += *(pPopulation + i);
                        //    sumOfSquare += (Int64)(*(pPopulation + i)) * (*(pPopulation + i));
                        //    if (*(pPopulation + i) < min)
                        //    {
                        //        min = *(pPopulation + i);
                        //    }
                        //}

                        //mean = mean / populationSize;
                        //Array.Sort(pixelPopulation);
                        //int mid = populationSize / 2;
                        //median = (double)(pixelPopulation[mid - 1] + pixelPopulation[mid]) / 2.0;    // zero index
                        //double min = pixelPopulation[0];
                        //stdDev = Math.Sqrt((sumOfSquare - (populationSize * (mean * mean))) / (populationSize - 1)); //N-1 for "sample standard deviation"
                        //testVal = Math.Abs(*(pOrigData + targetPixelIndex) - median);

                        // Don't bother to use this algorithm unless the number of values is large. 
                        int k = 0;
                        QuickMedian(pixelPopulation, populationSize, ref k);
                        median = pixelPopulation[k];

                        if ((*(pOrigData + pixelIndex) - median) < 0)
                        {
                            *(pCopyOfData + pixelIndex) = (ushort)median;
                        }
                    }
                }
            } //end fixed

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();
            cpyBorder.Unlock();

            // Collect all generations of memory.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return cpyImage;
        }

        /// <summary>
        /// Copy with border replication
        /// </summary>
        /// <param name="srcImage"></param>
        /// <returns></returns>
        public static unsafe WriteableBitmap CopyReplicateBorder(WriteableBitmap srcImage, int borderSize)
        {
            if (srcImage == null)
                return null;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcImage.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int srcWidth = srcImage.PixelWidth;
            int srcHeight = srcImage.PixelHeight;
            double dDpiX = srcImage.DpiX;
            double dDpiY = srcImage.DpiY;
            int bitsPerPixel = srcImage.Format.BitsPerPixel;
            int srcStep = ((srcWidth * bitsPerPixel) + 31) / 32 * 4;

            int dstWidth = srcWidth + (borderSize * 2);
            int dstHeight = srcHeight + (borderSize * 2);
            int dstStep = ((dstWidth * bitsPerPixel) + 31) / 32 * 4;

            WriteableBitmap dstImage = new WriteableBitmap(dstWidth, dstHeight, dDpiX, dDpiY, srcImage.Format, srcImage.Palette);

            byte* pSrc = (byte*)srcImage.BackBuffer.ToPointer();
            byte* pDst = (byte*)dstImage.BackBuffer.ToPointer();

            IppiSize srcRoiSize = new IppiSize(srcWidth, srcHeight);
            IppiSize dstRoiSize = new IppiSize(dstWidth, dstHeight);
            int topBorderHeight = borderSize;
            int leftBorderWidth = borderSize;

            IppImaging.CopyReplicateBorder(pSrc, srcStep, srcRoiSize, pixelFormat, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return dstImage;
        }

        /// <summary>
        /// Copy with border fill with const value
        /// </summary>
        /// <param name="srcImage"></param>
        /// <returns></returns>
        public static unsafe WriteableBitmap CopyConstBorder(WriteableBitmap srcImage, int borderSize, ushort value)
        {
            if (srcImage == null)
                return null;

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcImage.Format);
            if (!IsSupportedPixelFormat(pixelFormat))
            {
                throw new Exception("Image type currently not supported");
            }

            int srcWidth = srcImage.PixelWidth;
            int srcHeight = srcImage.PixelHeight;
            double dDpiX = srcImage.DpiX;
            double dDpiY = srcImage.DpiY;
            int bitsPerPixel = srcImage.Format.BitsPerPixel;
            int srcStep = ((srcWidth * bitsPerPixel) + 31) / 32 * 4;

            int dstWidth = srcWidth + (borderSize * 2);
            int dstHeight = srcHeight + (borderSize * 2);
            int dstStep = ((dstWidth * bitsPerPixel) + 31) / 32 * 4;
            WriteableBitmap dstImage = new WriteableBitmap(dstWidth, dstHeight, dDpiX, dDpiY, srcImage.Format, srcImage.Palette);

            byte* pSrc = (byte*)srcImage.BackBuffer.ToPointer();
            byte* pDst = (byte*)dstImage.BackBuffer.ToPointer();

            IppiSize srcRoiSize = new IppiSize(srcWidth, srcHeight);
            IppiSize dstRoiSize = new IppiSize(dstWidth, dstHeight);
            int topBorderHeight = borderSize;
            int leftBorderWidth = borderSize;

            IppImaging.CopyConstBorder(pSrc, srcStep, srcRoiSize, pixelFormat, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, value);

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return dstImage;
        }

        /// <summary>
        /// Lens curvature/distortion correction
        /// </summary>
        /// <param name="srcImage"></param>
        /// <param name="paramA"></param>
        /// <param name="paramB"></param>
        /// <param name="paramC"></param>
        /// <returns></returns>
        /***public static unsafe WriteableBitmap DistortionCorrection(WriteableBitmap srcImage, double paramA, double paramB, double paramC)
        {
            if (srcImage == null || (paramA == 0.0 && paramB == 0.0 && paramC == 0.0))
            {
                return srcImage;
            }

            int width = srcImage.PixelWidth;
            int height = srcImage.PixelHeight;
            int origWidth = width;
            int origHeight = height;
            bool bIsImageResized = false;
           
            // HACK: prevent a skewed on the image when the captured image width/height is an odd number
            if (origWidth % 2 != 0 || origHeight % 2 != 0)
            {
                if (origWidth % 2 != 0) { width--; }
                if (origHeight % 2 != 0) { height--; }
                srcImage = MVImage.ImageResize(srcImage, new System.Drawing.Size(width, height));
                bIsImageResized = true;
            }

            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            // Get a pointer to the back buffer.
            ushort* pOrigData = (ushort*)srcImage.BackBuffer.ToPointer();
            ushort* pCopyOfData = (ushort*)cpyImage.BackBuffer.ToPointer();

            // parameters for correction
            //double paramA = 0.0; // affects only the outermost pixels of the image
            //double paramB = 0.013; // most cases only require b optimization
            //double paramC = 0.0; // most uniform correction
            double paramD = 1.0 - paramA - paramB - paramC; // describes the linear scaling of the image

            // Reserve the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int d = Math.Min(width, height) / 2;    // radius of the circle

                    // center of dst image
                    double centerX = (width - 1) / 2.0;
                    double centerY = (height - 1) / 2.0;

                    // cartesian coordinates of the destination point (relative to the centre of the image)
                    double deltaX = (x - centerX) / d;
                    double deltaY = (y - centerY) / d;

                    // distance or radius of dst image
                    double dstR = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                    // distance or radius of src image (with formula)
                    double srcR = (paramA * dstR * dstR * dstR + paramB * dstR * dstR + paramC * dstR + paramD) * dstR;

                    // comparing old and new distance to get factor
                    double factor = Math.Abs(dstR / srcR);

                    // coordinates in source image
                    double srcXd = centerX + (deltaX * factor * d);
                    double srcYd = centerY + (deltaY * factor * d);

                    int srcX = (int)srcXd;
                    int srcY = (int)srcYd;
                    double rX = srcXd % srcX;
                    double rY = srcYd % srcY;

                    if (srcX >= 0 && srcY >= 0 && srcX < width && srcY - 1 < height - 1)
                    {
                        int dstPos = y * width + x;
                        double value = (1 - rX) * (1 - rY) * (*(pOrigData + srcY * width + srcX)) +
                                       rX * (1 - rY) * (*(pOrigData + srcY * width + srcX + 1)) +
                                       (1 - rX) * rY * (*(pOrigData + (srcY + 1) * width + srcX)) +
                                       rX * rY * (*(pOrigData + (srcY + 1) * width + srcX + 1));
                        *(pCopyOfData + dstPos) = (ushort)value;
                    }
                }
            }

            // Release the back buffer and make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();

            // HACK: resize to the original image size.
            if (bIsImageResized)
            {
                cpyImage = MVImage.ImageResize(cpyImage, new System.Drawing.Size(origWidth, origHeight));
            }

            return cpyImage;
        }***/

        /// <summary>
        /// Remove the CCD bad column
        /// </summary>
        /// <param name="srcImage"></param>
        /// <returns></returns>
        /***public static unsafe WriteableBitmap CCDBadColumnRemoval(WriteableBitmap srcImage)
        {
            // copy of the original source image
            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            int imageWidth = cpyImage.PixelWidth;
            int imageHeight = cpyImage.PixelHeight;
            int origImageWidth = srcImage.PixelWidth;

            double mean = 0.0;
            double stdDev = 0.0;
            double sumOfSquare = 0;
            double[] meanArray = new double[imageWidth];
            List<int> badColumnList = new List<int>();

            // Reserve the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();

            // Get a pointer to the back buffer.
            ushort* pOrigData = (ushort*)srcImage.BackBuffer.ToPointer();
            ushort* pCopyOfData = (ushort*)cpyImage.BackBuffer.ToPointer();

            // find the mean of each column of the whole image
            fixed (void* pPtr = &meanArray[0])
            {
                double* pMeanArray = (double*)pPtr;
                for (int iCol = 0; iCol < imageWidth; ++iCol)
                {
                    mean = 0;
                    for (int iRow = 0; iRow < imageHeight; ++iRow)
                    {
                        mean += *(pOrigData + iCol + imageWidth * iRow);
                    }
                    mean = mean / imageHeight;
                    *(pMeanArray + iCol) = mean;
                }
            }

            // find the bad column
            int populationSize = 6;
            fixed (void* pPtr = &meanArray[0])
            {
                double* pMeanArray = (double*)pPtr;
                for (int iCol = 3; iCol < imageWidth-3; iCol++)
                {
                    mean = 0;
                    sumOfSquare = 0;
                    stdDev = 0;

                    mean += *(pMeanArray + iCol - 3) + *(pMeanArray + iCol - 2) + *(pMeanArray + iCol - 1);
                    mean += *(pMeanArray + iCol + 1) + *(pMeanArray + iCol + 2) + *(pMeanArray + iCol + 3);

                    sumOfSquare += (*(pMeanArray + iCol - 3)) * (*(pMeanArray + iCol - 3));
                    sumOfSquare += (*(pMeanArray + iCol - 2)) * (*(pMeanArray + iCol - 2));
                    sumOfSquare += (*(pMeanArray + iCol - 1)) * (*(pMeanArray + iCol - 1));
                    //sumOfSquare += (*(pMeanArray + iCol)) * (*(pMeanArray + iCol));
                    sumOfSquare += (*(pMeanArray + iCol + 1)) * (*(pMeanArray + iCol + 1));
                    sumOfSquare += (*(pMeanArray + iCol + 2)) * (*(pMeanArray + iCol + 2));
                    sumOfSquare += (*(pMeanArray + iCol + 3)) * (*(pMeanArray + iCol + 3));

                    mean = mean / populationSize;
                    stdDev = Math.Sqrt((sumOfSquare - (populationSize * (mean * mean))) / (populationSize - 1)); //N-1 for "sample standard deviation"

                    if (Math.Abs(*(pMeanArray + iCol) - mean) > 4 * stdDev)
                    {
                        badColumnList.Add(iCol);
                    }
                }

                // fill the bad column with the average of its 2 neighbor
                double neighborAvg = 0;
                for (int i = 0; i < badColumnList.Count; i++)
                {
                    for (int iRow = 0; iRow < imageHeight; iRow++)
                    {
                        neighborAvg = (*(pOrigData + (badColumnList[i] - 1) + (imageWidth * iRow)) * 0.5) + (*(pOrigData + (badColumnList[i] + 1) + (imageWidth * iRow)) * 0.5);
                        *(pCopyOfData + badColumnList[i] + (imageWidth * iRow)) = (ushort)neighborAvg;
                    }
                }
            }

            meanArray = null;
            badColumnList.Clear();

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();

            // Collect all generations of memory.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return cpyImage;
        }***/

        /// <summary>
        /// Remove the CCD bad column
        /// </summary>
        /// <param name="srcImage"></param>
        /// <returns></returns>
        /***public static unsafe WriteableBitmap CCDBadColumnRemoval(WriteableBitmap srcImage)
        {
            // copy of the original source image
            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            int imageWidth = cpyImage.PixelWidth;
            int imageHeight = cpyImage.PixelHeight;
            int origImageWidth = srcImage.PixelWidth;

            double mean = 0.0;
            double stdDev = 0.0;
            double sumOfSquare = 0;
            double[] meanArray = new double[imageWidth];
            List<int> badColumnList = new List<int>();

            // Reserve the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();

            // Get a pointer to the back buffer.
            ushort* pOrigData = (ushort*)srcImage.BackBuffer.ToPointer();
            ushort* pCopyOfData = (ushort*)cpyImage.BackBuffer.ToPointer();

            // find the mean of each column of the whole image
            fixed (void* pPtr = &meanArray[0])
            {
                double* pMeanArray = (double*)pPtr;
                for (int iCol = 0; iCol < imageWidth; ++iCol)
                {
                    mean = 0;
                    for (int iRow = 0; iRow < imageHeight; ++iRow)
                    {
                        mean += *(pOrigData + iCol + imageWidth * iRow);
                    }
                    mean = mean / imageHeight;
                    *(pMeanArray + iCol) = mean;
                }
            }

            // find the bad column
            List<double> populationList = new List<double>();
            double min = 0;
            double max = 0;
            int populationCount = 0;
            double median = 0;
            double[] arrNeighbors = new double[4];
            double sum = 0;
            fixed (void* pPtr = &meanArray[0])
            {
                double* pMeanArray = (double*)pPtr;
                for (int iCol = 3; iCol < imageWidth - 3; iCol++)
                {
                    mean = 0;
                    sumOfSquare = 0;
                    stdDev = 0;
                    median = 0;
                    sum = 0;

                    populationList.Add(*(pMeanArray + iCol - 3));
                    populationList.Add(*(pMeanArray + iCol - 2));
                    populationList.Add(*(pMeanArray + iCol - 1));
                    //populationList.Add(*(pMeanArray + iCol));
                    populationList.Add(*(pMeanArray + iCol + 1));
                    populationList.Add(*(pMeanArray + iCol + 2));
                    populationList.Add(*(pMeanArray + iCol + 3));
                    populationList.Sort();
                    min = populationList[0];
                    max = populationList[5];

                    // Remove min and max from the list
                    for (int i = 0; i < populationList.Count; i++)
                    {
                        if (populationList[i] == min || populationList[i] == max)
                        {
                            populationList.Remove(populationList[i]);
                        }
                    }

                    // find the median
                    populationCount = populationList.Count;
                    if (populationCount == 1)
                    {
                        median = populationList[0];
                    }
                    else if (populationCount > 1)
                    {
                        median = populationList[populationCount / 2];
                    }

                    // find the sum and the squared sum
                    for (int i = 0; i < populationCount; i++)
                    {
                        sumOfSquare += populationList[i] * populationList[i];
                        sum += populationList[i];
                    }

                    mean = sum / populationCount;

                    stdDev = Math.Sqrt((sumOfSquare - (populationCount * (mean * mean))) / (populationCount - 1)); //N-1 for "sample standard deviation"

                    if (Math.Abs(*(pMeanArray + iCol) - mean) > 6 * stdDev)
                    {
                        badColumnList.Add(iCol);
                    }

                    populationList.Clear();
                }

                // fill the bad column with the average of its 2 neighbor
                double neighborMedian = 0;
                for (int i = 0; i < badColumnList.Count; i++)
                {
                    for (int iRow = 0; iRow < imageHeight; iRow++)
                    {
                         arrNeighbors[0] = *(pOrigData + (badColumnList[i] - 2) + (imageWidth * iRow));
                         arrNeighbors[1] = *(pOrigData + (badColumnList[i] - 1) + (imageWidth * iRow));
                         arrNeighbors[2] = *(pOrigData + (badColumnList[i] + 1) + (imageWidth * iRow));
                         arrNeighbors[3] = *(pOrigData + (badColumnList[i] + 2) + (imageWidth * iRow));
                         Array.Sort(arrNeighbors);
                         neighborMedian = arrNeighbors[2];
                         *(pCopyOfData + badColumnList[i] + (imageWidth * iRow)) = (ushort)neighborMedian;
                    }
                }
            }

            meanArray = null;
            badColumnList.Clear();
            populationList.Clear();

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();

            return cpyImage;
        }***/


        public static unsafe WriteableBitmap SignalMask(WriteableBitmap srcImage)
        {
            if (srcImage == null) { return srcImage; }

            int imageWidth = srcImage.PixelWidth;
            int imageHeight = srcImage.PixelHeight;

            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            // Get a pointer to the back buffer.
            ushort* pCpyData = (ushort*)cpyImage.BackBuffer.ToPointer();

            // Reserve the back buffer for updates.
            cpyImage.Lock();

            for (int iRow = 0; iRow < imageHeight; ++iRow)
            {
                for (int iCol = 0; iCol < imageWidth; ++iCol)
                {
                    if (*(pCpyData + iCol + imageWidth * iRow) != 0)
                    {
                        *(pCpyData + iCol + imageWidth * iRow) = 1;
                    }
                }
            }

            // Release the back buffer and make it available for display.
            cpyImage.Unlock();

            return cpyImage;
        }

        public static unsafe WriteableBitmap BackgroundNoiseMask(WriteableBitmap srcImage)
        {
            if (srcImage == null) { return srcImage; }

            int imageWidth = srcImage.PixelWidth;
            int imageHeight = srcImage.PixelHeight;

            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            // Get a pointer to the back buffer.
            ushort* pCpyData = (ushort*)cpyImage.BackBuffer.ToPointer();

            // Reserve the back buffer for updates.
            cpyImage.Lock();

            for (int iRow = 0; iRow < imageHeight; ++iRow)
            {
                for (int iCol = 0; iCol < imageWidth; ++iCol)
                {
                    *(pCpyData + iCol + imageWidth * iRow) = (ushort)Math.Abs(1.0 - (double)*(pCpyData + iCol + imageWidth * iRow));
                }
            }

            // Release the back buffer and make it available for display.
            cpyImage.Unlock();

            return cpyImage;
        }

        /*public static WriteableBitmap ChemiBackgroundCorrection(WriteableBitmap srcImage, WriteableBitmap despeckledImage, int chemiRank)
        {
            if (srcImage == null || despeckledImage == null || chemiRank == 0)
            {
                return srcImage;
            }

            WriteableBitmap flattedImage = (WriteableBitmap)srcImage.Clone();
            WriteableBitmap rankedImage = null;
            ImageArithmetic imageArith = new ImageArithmetic();

            // image resize (keeping aspect ratio)
            int iOrigWidth = despeckledImage.PixelWidth;
            int iOrigHeight = despeckledImage.PixelHeight;
            int iWidth = 600;   // new width
            double factor = (double)iWidth / despeckledImage.PixelWidth;
            int iHeight = (int)Math.Round(despeckledImage.PixelHeight * factor);   // new height
            srcImage = MVImage.ImageResize(despeckledImage, new System.Drawing.Size(iWidth, iHeight));
            rankedImage = MVImage.RankFilter(srcImage, chemiRank);  // kernelsize = 2 * chemiRank + 1


            srcImage = imageArith.SubtractImage(srcImage, rankedImage);

            srcImage = imageArith.Multiply(srcImage, 65535);

            DespeckleFilter despeckledFilter = new DespeckleFilter(new Size(5, 5));
            srcImage = despeckledFilter.Execute(srcImage);

            // blur the image with gaussian blur
            //FastGaussianFilter gaussianFilter = new FastGaussianFilter(new System.Drawing.Size(25, 25));
            //gaussianFilter.Apply(ref srcImage);

            // resize image back to original size
            srcImage = MVImage.ImageResize(srcImage, new System.Drawing.Size(iOrigWidth, iOrigHeight));

            WriteableBitmap signalImage = MVImage.SignalMask(srcImage);

            WriteableBitmap noiseImage = MVImage.BackgroundNoiseMask(signalImage);

            signalImage = imageArith.MultiplyImage(ref flattedImage, ref signalImage);

            noiseImage = imageArith.MultiplyImage(ref despeckledImage, ref noiseImage);

            srcImage = imageArith.AddImage(ref signalImage, ref noiseImage);

            return srcImage;
        }*/

        public static WriteableBitmap RGBBackgroundCorrection(WriteableBitmap srcImage, int rgbRank, ref WriteableBitmap borderImage)
        {
            borderImage = null;

            if (srcImage == null || rgbRank == 0)
            {
                return srcImage;
            }

            WriteableBitmap flattedImage = (WriteableBitmap)srcImage.Clone();    // image1

            WriteableBitmap rankedImage = null;
            ImageArithmetic imageArith = new ImageArithmetic();

            // image resize to 600 x n (keeping aspect ratio)
            int iOrigWidth = srcImage.PixelWidth;
            int iOrigHeight = srcImage.PixelHeight;
            int iWidth = 600;   // new width
            double factor = (double)iWidth / srcImage.PixelWidth;
            int iHeight = (int)Math.Round(srcImage.PixelHeight * factor);   // new height

            srcImage = Resize(srcImage, new Size(iWidth, iHeight));  // image2

            // apply rank filter
            rankedImage = RankRGBFilter(srcImage, rgbRank);  // image3

            // resize image back to original size
            rankedImage = Resize(rankedImage, new Size(iOrigWidth, iOrigHeight));    // image4

            srcImage = imageArith.SubtractImage(flattedImage, rankedImage);  // image5: image1-image4

            // clean up
            flattedImage = null;
            rankedImage = null;

            #region Scaled the image
            // scale the 16-bit image before converting to 8-bit
            const int nBins = 65535;
            const int nLevels = nBins + 1;
            int nLowerLevel = 0;
            int nUpperLevel = 65536;
            double dSum = 0;
            int totalPixels = srcImage.PixelWidth * srcImage.PixelHeight;
            int intensity = 0;

            // get the image histogram
            int[,] pHist = HistogramEven(srcImage, nLevels, nLowerLevel, nUpperLevel);

            for (int i = 0; i < nLevels; i++)
            {

                dSum += pHist[0, i];
                if ((dSum / (double)totalPixels) >= 0.995)
                {
                    intensity = i;
                    break;
                }
            }
            double dfactor = (double)(65535.0 / (double)intensity);
            WriteableBitmap scaledImage = srcImage.Clone();
            scaledImage = imageArith.Multiply(scaledImage, dfactor);

            // clean up
            pHist = null;
            #endregion

            AForge.Imaging.UnmanagedImage afUnmanaged = new AForge.Imaging.UnmanagedImage(
                scaledImage.BackBuffer,
                scaledImage.PixelWidth,
                scaledImage.PixelHeight,
                scaledImage.BackBufferStride,
                System.Drawing.Imaging.PixelFormat.Format16bppGrayScale);

            System.Drawing.Bitmap bitmap = afUnmanaged.ToManagedImage();
            System.Drawing.Bitmap bitmap8bpp = AForge.Imaging.Image.Convert16bppTo8bpp(bitmap);

            // clean up
            afUnmanaged = null;
            bitmap = null;
            scaledImage = null;

            // apply OtsuThreshold filter
            OtsuThreshold filter = new OtsuThreshold();
            filter.ApplyInPlace(bitmap8bpp);     // image8

            BlobCounter blobCounter = new BlobCounter();
            Blob[] blobs;
            blobCounter.FilterBlobs = true;
            blobCounter.BlobsFilter = null;
            //blobCounter.MinWidth = 100;
            //blobCounter.MinHeight = 100;
            blobCounter.ObjectsOrder = ObjectsOrder.Size;
            blobCounter.ProcessImage(bitmap8bpp);
            blobs = blobCounter.GetObjectsInformation();

            //List<WriteableBitmap> listBlobs = new List<WriteableBitmap>();
            WriteableBitmap blobsImage = null;

            for (int i = 0; i < blobs.Length; i++)
            {
                if (blobs[i].Rectangle.Width > 200 || blobs[i].Rectangle.Height > 200)
                {
                    blobCounter.ExtractBlobsImage(bitmap8bpp, blobs[i], true);

                    if (i == 0)
                    {
                        WriteableBitmap wbBitmap = new WriteableBitmap(bitmap8bpp.Width, bitmap8bpp.Height, 96, 96, PixelFormats.Gray8, null);
                        int iBufferSize = wbBitmap.PixelHeight * wbBitmap.BackBufferStride;
                        wbBitmap.WritePixels(new Int32Rect(0, 0, bitmap8bpp.Width, bitmap8bpp.Height), blobs[i].Image.ImageData, iBufferSize, wbBitmap.BackBufferStride);
                        blobsImage = wbBitmap;
                    }
                    else
                    {
                        AddBlobs(ref blobsImage, ref bitmap8bpp, ref blobs[i]);
                    }
                }
            }

            // clean up
            blobs = null;

            if (blobsImage != null)
            {
                // Convert 8bpp image to 16bpp
                blobsImage = Convert8bppTo16bpp(blobsImage);             // image10

                // scale the image
                blobsImage = imageArith.Multiply(blobsImage, 65535);

                //srcImage = BorderExclusion(srcImage, blobsImage);    // remove border on image5
                borderImage = blobsImage;
            }

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

            return srcImage;     // background correction image
        }

        #region public static WriteableBitmap CreateVisibleDynamicFlat(WriteableBitmap srcImage, int rankKernelSize)
        /// <summary>
        /// VISIBLE SOFTWARE WORK-AROUND: creating dynamic flat
        /// </summary>
        /// <param name="srcImage"></param>
        /// <param name="rgbRank"></param>
        /// <param name="borderImage"></param>
        /// <returns></returns>
        public static WriteableBitmap CreateVisibleDynamicFlat(WriteableBitmap srcImage, int rankKernelSize)
        {
            if (srcImage == null || rankKernelSize == 0)
            {
                return null;
            }

            ImageArithmetic imageArith = new ImageArithmetic();

            // Image resize to 600 x n (keeping aspect ratio) to speed up the rank filter
            int iOrigWidth = srcImage.PixelWidth;
            int iOrigHeight = srcImage.PixelHeight;
            int iWidth = 600;   // new width
            double factor = (double)iWidth / srcImage.PixelWidth;
            int iHeight = (int)Math.Round(srcImage.PixelHeight * factor);   // new height

            srcImage = Resize(srcImage, new Size(iWidth, iHeight));

            // Apply rank filter
            WriteableBitmap rankedImage = RankVisibleFilter(srcImage, rankKernelSize);

            // Apply gaussian filtering on the image with 7x7 kernel (to blur the image)
            //System.Drawing.Size kernelSize = new System.Drawing.Size(7, 7);
            //FastGaussianFilter gaussianFilter = new FastGaussianFilter(kernelSize);
            //gaussianFilter.Apply(ref rankedImage);

            // Breathing (shrink down then expand)
            iWidth = 50;   // new width
            factor = (double)iWidth / srcImage.PixelWidth;
            iHeight = (int)Math.Round(srcImage.PixelHeight * factor);   // new height
            rankedImage = Resize(rankedImage, new Size(iWidth, iHeight));

            // Apply gaussian filtering on the image with 3x3 kernel (to blur the image)
            System.Drawing.Size kernelSize = new System.Drawing.Size(3, 3);
            FastGaussianFilter gaussianFilter = new FastGaussianFilter(kernelSize);
            gaussianFilter.Apply(ref rankedImage);

            // Resize image back to original size
            rankedImage = Resize(rankedImage, new Size(iOrigWidth, iOrigHeight));

            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.Collect();

            return rankedImage;  // Flatten image
        }

        #endregion

        /// <summary>
        /// Convert 8bpp image to 16bpp image
        /// </summary>
        /// <param name="srcImage"></param>
        /// <returns></returns>
        public static unsafe WriteableBitmap Convert8bppTo16bpp(WriteableBitmap srcImage)
        {
            if (srcImage == null) { return null; }

            PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcImage.Format);

            if (pixelFormat != PixelFormatType.P8u_C1 && pixelFormat != PixelFormatType.P8u_C3)
            {
                throw new Exception("Pixel format not supported");
            }

            int width = srcImage.PixelWidth;
            int height = srcImage.PixelHeight;
            int srcStep = srcImage.BackBufferStride;
            double dDpiX = srcImage.DpiX;
            double dDpiY = srcImage.DpiY;

            WriteableBitmap dstImage = new WriteableBitmap(width, height, dDpiX, dDpiY, PixelFormats.Gray16, null);
            int dstStep = dstImage.BackBufferStride;

            byte* pSrc = (byte*)srcImage.BackBuffer.ToPointer();
            byte* pDst = (byte*)dstImage.BackBuffer.ToPointer();

            IppiSize roiSize = new IppiSize(width, height);

            IppImaging.Convert8bppTo16bpp(pSrc, srcStep, pixelFormat, pDst, dstStep, roiSize);

            return dstImage;
        }

        /// <summary>
        /// Convert AForge 8bpp UnmanagedImage to 16bpp WriteableBitmap 
        /// </summary>
        /// <param name="srcImage"></param>
        /// <returns></returns>
        public static unsafe WriteableBitmap Convert8bppTo16bpp(UnmanagedImage srcImage)
        {
            if (srcImage == null) { return null; }

            if (srcImage.PixelFormat != System.Drawing.Imaging.PixelFormat.Format8bppIndexed)
            {
                throw new ArgumentException();
            }

            int width = srcImage.Width;
            int height = srcImage.Height;
            int srcStep = srcImage.Stride;

            WriteableBitmap dstImage = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray16, null);
            int dstStep = dstImage.BackBufferStride;

            byte* pSrcData = (byte*)srcImage.ImageData.ToPointer();
            byte* pDstData = (byte*)dstImage.BackBuffer.ToPointer();

            IppiSize roiSize = new IppiSize(width, height);
            PixelFormatType pixelType = PixelFormatType.P8u_C1;

            IppImaging.Convert8bppTo16bpp(pSrcData, srcStep, pixelType, pDstData, dstStep, roiSize);

            return dstImage;
        }

        public static unsafe WriteableBitmap BorderExclusion(WriteableBitmap srcImage, WriteableBitmap borderImage)
        {
            if (srcImage == null || borderImage == null) { return srcImage; }

            int imageWidth = srcImage.PixelWidth;
            int imageHeight = srcImage.PixelHeight;

            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            // Get a pointer to the back buffer.
            ushort* pCpyData = (ushort*)cpyImage.BackBuffer.ToPointer();
            ushort* pBorderData = (ushort*)borderImage.BackBuffer.ToPointer();

            // Reserve the back buffer for updates.
            cpyImage.Lock();
            borderImage.Lock();

            for (int iRow = 0; iRow < imageHeight; ++iRow)
            {
                for (int iCol = 0; iCol < imageWidth; ++iCol)
                {
                    if (*(pBorderData + iCol + imageWidth * iRow) != 0)
                    {
                        *(pCpyData + iCol + imageWidth * iRow) = 0;
                    }

                    //if (*(pCpyData + iCol + imageWidth * iRow) != 0)
                    //{
                    //    *(pCpyData + iCol + imageWidth * iRow) = 1;
                    //}
                }
            }

            // Release the back buffer and make it available for display.
            cpyImage.Unlock();
            borderImage.Unlock();

            return cpyImage;
        }

        public static WriteableBitmap FindBlobs(WriteableBitmap srcImage)
        {
            #region Scaled the image
            // scale the 16-bit image before converting to 8-bit
            const int nBins = 65535;
            const int nLevels = nBins + 1;
            int nLowerLevel = 0;
            int nUpperLevel = 65536;
            double dSum = 0;
            int totalPixels = srcImage.PixelWidth * srcImage.PixelHeight;
            int intensity = 0;
            int srcWidth = srcImage.PixelWidth;
            int srcHeight = srcImage.PixelHeight;

            // get the image histogram
            int[,] pHist = HistogramEven(srcImage, nLevels, nLowerLevel, nUpperLevel);

            for (int i = 0; i < nLevels; i++)
            {
                dSum += pHist[0, i];
                if ((dSum / (double)totalPixels) >= 0.995)
                {
                    intensity = i;
                    break;
                }
            }

            ImageArithmetic imageArith = new ImageArithmetic();
            double dfactor = (double)(65535.0 / (double)intensity);
            WriteableBitmap scaledImage = srcImage.Clone();
            scaledImage = imageArith.Multiply(scaledImage, dfactor);

            // clean up
            pHist = null;
            #endregion

            AForge.Imaging.UnmanagedImage afUnmanaged = new AForge.Imaging.UnmanagedImage(
                scaledImage.BackBuffer,
                scaledImage.PixelWidth,
                scaledImage.PixelHeight,
                scaledImage.BackBufferStride,
                System.Drawing.Imaging.PixelFormat.Format16bppGrayScale);

            System.Drawing.Bitmap bitmap = afUnmanaged.ToManagedImage();
            System.Drawing.Bitmap bitmap8bpp = AForge.Imaging.Image.Convert16bppTo8bpp(bitmap);

            // clean up
            afUnmanaged = null;
            bitmap = null;
            scaledImage = null;

            // apply OtsuThreshold filter
            OtsuThreshold filter = new OtsuThreshold();
            filter.ApplyInPlace(bitmap8bpp);

            BlobCounter blobCounter = new BlobCounter();
            Blob[] blobs;
            blobCounter.FilterBlobs = true;
            blobCounter.BlobsFilter = null;
            //blobCounter.MinWidth = 100;
            //blobCounter.MinHeight = 100;
            blobCounter.ObjectsOrder = ObjectsOrder.Size;
            blobCounter.ProcessImage(bitmap8bpp);
            blobs = blobCounter.GetObjectsInformation();

            //List<WriteableBitmap> listBlobs = new List<WriteableBitmap>();
            WriteableBitmap blobsImage = null;
            int iBlobMin = 20;
            int iEdgeMin = 10;

            for (int i = 0; i < blobs.Length; i++)
            {
                if (blobs[i].Rectangle.Width > iBlobMin && blobs[i].Rectangle.Height > iBlobMin)
                {
                    if (blobs[i].Rectangle.Top > iEdgeMin && blobs[i].Rectangle.Left > iEdgeMin &&
                        blobs[i].Rectangle.Bottom < (srcHeight - iEdgeMin) && blobs[i].Rectangle.Right < (srcWidth - iEdgeMin))
                    {
                        blobCounter.ExtractBlobsImage(bitmap8bpp, blobs[i], true);

                        if (i == 0)
                        {
                            WriteableBitmap wbBitmap = new WriteableBitmap(bitmap8bpp.Width, bitmap8bpp.Height, 96, 96, PixelFormats.Gray8, null);
                            int iBufferSize = wbBitmap.PixelHeight * wbBitmap.BackBufferStride;
                            wbBitmap.WritePixels(new Int32Rect(0, 0, bitmap8bpp.Width, bitmap8bpp.Height), blobs[i].Image.ImageData, iBufferSize, wbBitmap.BackBufferStride);
                            blobsImage = wbBitmap;
                        }
                        else
                        {
                            AddBlobs(ref blobsImage, ref bitmap8bpp, ref blobs[i]);
                        }
                    }
                }
            }

            blobs = null;

            WriteableBitmap blobsImage16 = null;

            if (blobsImage != null)
            {
                // Convert 8bpp image to 16bpp
                blobsImage16 = Convert8bppTo16bpp(blobsImage);

                // scale the image
                blobsImage16 = imageArith.Multiply(blobsImage16, 65535);
            }

            /***
            if (blobsImage != null)
            {
                // Apply Erosion filter

                // WriteableBitmap to AForge UnmanagedImage
                UnmanagedImage afumBlobs = new UnmanagedImage(
                    blobsImage.BackBuffer,
                    blobsImage.PixelWidth,
                    blobsImage.PixelHeight,
                    blobsImage.BackBufferStride,
                    System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

                // create filter
                Erosion3x3 erosionFilter = new Erosion3x3();
                // apply the filter
                erosionFilter.ApplyInPlace(afumBlobs);

                // Convert 8bpp image to 16bpp
                blobsImage16 = Convert8bppTo16bpp(afumBlobs);

                // scale the image
                blobsImage16 = imageArith.Multiply(blobsImage16, 65535);
            }
            ***/

            return blobsImage16;
        }

        public static WriteableBitmap FindBlobs(WriteableBitmap srcImage, Size size)
        {
            #region Scaled the image
            // scale the 16-bit image before converting to 8-bit
            const int nBins = 65535;
            const int nLevels = nBins + 1;
            int nLowerLevel = 0;
            int nUpperLevel = 65536;
            double dSum = 0;
            int totalPixels = srcImage.PixelWidth * srcImage.PixelHeight;
            int intensity = 0;
            int srcWidth = srcImage.PixelWidth;
            int srcHeight = srcImage.PixelHeight;

            // get the image histogram
            int[,] pHist = HistogramEven(srcImage, nLevels, nLowerLevel, nUpperLevel);

            for (int i = 0; i < nLevels; i++)
            {
                dSum += pHist[0, i];
                if ((dSum / (double)totalPixels) >= 0.995)
                {
                    intensity = i;
                    break;
                }
            }

            ImageArithmetic imageArith = new ImageArithmetic();
            double dfactor = (double)(65535.0 / (double)intensity);
            WriteableBitmap scaledImage = srcImage.Clone();
            scaledImage = imageArith.Multiply(scaledImage, dfactor);

            // clean up
            pHist = null;
            //pLevels = null;
            #endregion

            AForge.Imaging.UnmanagedImage afUnmanaged = new AForge.Imaging.UnmanagedImage(
                scaledImage.BackBuffer,
                scaledImage.PixelWidth,
                scaledImage.PixelHeight,
                scaledImage.BackBufferStride,
                System.Drawing.Imaging.PixelFormat.Format16bppGrayScale);

            System.Drawing.Bitmap bitmap = afUnmanaged.ToManagedImage();
            System.Drawing.Bitmap bitmap8bpp = AForge.Imaging.Image.Convert16bppTo8bpp(bitmap);

            // clean up
            afUnmanaged = null;
            bitmap = null;
            scaledImage = null;

            // apply OtsuThreshold filter
            OtsuThreshold filter = new OtsuThreshold();
            filter.ApplyInPlace(bitmap8bpp);

            BlobCounter blobCounter = new BlobCounter();
            Blob[] blobs;
            blobCounter.FilterBlobs = true;
            blobCounter.BlobsFilter = null;
            //blobCounter.MinWidth = 100;
            //blobCounter.MinHeight = 100;
            blobCounter.ObjectsOrder = ObjectsOrder.Size;
            blobCounter.ProcessImage(bitmap8bpp);
            blobs = blobCounter.GetObjectsInformation();

            WriteableBitmap blobsImage = null;
            int iBlobWidth = (int)size.Width;
            int iBlobHeight = (int)size.Height;
            List<System.Drawing.Rectangle> rectList = new List<System.Drawing.Rectangle>();

            for (int i = 0; i < blobs.Length; i++)
            {
                if (blobs[i].Rectangle.Width > iBlobWidth && blobs[i].Rectangle.Height > iBlobHeight)
                {
                    blobCounter.ExtractBlobsImage(bitmap8bpp, blobs[i], true);

                    if (i == 0)
                    {
                        WriteableBitmap wbBitmap = new WriteableBitmap(bitmap8bpp.Width, bitmap8bpp.Height, 96, 96, PixelFormats.Gray8, null);
                        int iBufferSize = wbBitmap.PixelHeight * wbBitmap.BackBufferStride;
                        wbBitmap.WritePixels(new Int32Rect(0, 0, bitmap8bpp.Width, bitmap8bpp.Height), blobs[i].Image.ImageData, iBufferSize, wbBitmap.BackBufferStride);
                        blobsImage = wbBitmap;
                        rectList.Add(blobs[i].Rectangle);
                    }
                    else
                    {
                        AddBlobs(ref blobsImage, ref bitmap8bpp, ref blobs[i]);
                        rectList.Add(blobs[i].Rectangle);
                    }
                }
            }

            blobs = null;

            WriteableBitmap blobsImage16 = null;

            if (blobsImage != null)
            {
                // Convert 8bpp image to 16bpp
                blobsImage16 = Convert8bppTo16bpp(blobsImage);

                // Fill the blob within the blobs' rectangle
                FillBlobs(ref blobsImage16, rectList);

                // scale the image
                //blobsImage16 = imageArith.Multiply(blobsImage16, 65535);
            }

            return blobsImage16;
        }

        public static WriteableBitmap FindBlobs(WriteableBitmap srcImage, Size size, WriteableBitmap clippedImage, Rect clipRect)
        {
            #region Scaled the image
            // scale the 16-bit image before converting to 8-bit
            const int nBins = 65535;
            const int nLevels = nBins + 1;
            int nLowerLevel = 0;
            int nUpperLevel = 65536;
            double dSum = 0;
            int totalPixels = srcImage.PixelWidth * srcImage.PixelHeight;
            int intensity = 0;
            int srcWidth = srcImage.PixelWidth;
            int srcHeight = srcImage.PixelHeight;

            // Create empty image buffer
            WriteableBitmap srcClippedImage = new WriteableBitmap(srcWidth, srcHeight, srcImage.DpiX, srcImage.DpiY, srcImage.Format, srcImage.Palette);
            // Paste the clipped image to the empty image buffer
            srcClippedImage = ImageProcessingHelper.PasteImage(srcClippedImage, clippedImage, clipRect);

            // get the image histogram
            int[,] pHist = HistogramEven(srcImage, nLevels, nLowerLevel, nUpperLevel);

            for (int i = 0; i < nLevels; i++)
            {
                dSum += pHist[0, i];
                if ((dSum / (double)totalPixels) >= 0.995)
                {
                    intensity = i;
                    break;
                }
            }

            // scale the clipped image to the source image
            //
            ImageArithmetic imageArith = new ImageArithmetic();
            double dfactor = (double)(65535.0 / (double)intensity);
            //WriteableBitmap scaledImage = srcClippedImage.Clone();
            WriteableBitmap scaledImage = imageArith.Multiply(srcClippedImage, dfactor);

            // clean up
            pHist = null;
            //pLevels = null;
            #endregion

            AForge.Imaging.UnmanagedImage afUnmanaged = new AForge.Imaging.UnmanagedImage(
                scaledImage.BackBuffer,
                scaledImage.PixelWidth,
                scaledImage.PixelHeight,
                scaledImage.BackBufferStride,
                System.Drawing.Imaging.PixelFormat.Format16bppGrayScale);

            System.Drawing.Bitmap bitmap = afUnmanaged.ToManagedImage();
            System.Drawing.Bitmap bitmap8bpp = AForge.Imaging.Image.Convert16bppTo8bpp(bitmap);

            // clean up
            afUnmanaged = null;
            bitmap = null;
            srcClippedImage = null;
            scaledImage = null;

            // apply OtsuThreshold filter
            OtsuThreshold filter = new OtsuThreshold();
            filter.ApplyInPlace(bitmap8bpp);

            BlobCounter blobCounter = new BlobCounter();
            Blob[] blobs;
            blobCounter.FilterBlobs = true;
            blobCounter.BlobsFilter = null;
            //blobCounter.MinWidth = 100;
            //blobCounter.MinHeight = 100;
            blobCounter.ObjectsOrder = ObjectsOrder.Size;
            blobCounter.ProcessImage(bitmap8bpp);
            blobs = blobCounter.GetObjectsInformation();

            int iBlobWidth = (int)size.Width;
            int iBlobHeight = (int)size.Height;
            List<System.Drawing.Rectangle> rectList = new List<System.Drawing.Rectangle>();
            WriteableBitmap wbBitmap = null;
            WriteableBitmap blobsImage = null;

            if (iBlobWidth > clipRect.Width)
                iBlobWidth = (int)clipRect.Width;
            if (iBlobHeight > clipRect.Height)
                iBlobHeight = (int)clipRect.Height;

            for (int i = 0; i < blobs.Length; i++)
            {
                if (blobs[i].Rectangle.Width > iBlobWidth && blobs[i].Rectangle.Height > iBlobHeight)
                {
                    blobCounter.ExtractBlobsImage(bitmap8bpp, blobs[i], true);

                    if (blobsImage == null)
                    {
                        wbBitmap = new WriteableBitmap(bitmap8bpp.Width, bitmap8bpp.Height, 96, 96, PixelFormats.Gray8, null);
                        int iBufferSize = wbBitmap.PixelHeight * wbBitmap.BackBufferStride;
                        wbBitmap.WritePixels(new Int32Rect(0, 0, bitmap8bpp.Width, bitmap8bpp.Height), blobs[i].Image.ImageData, iBufferSize, wbBitmap.BackBufferStride);
                        blobsImage = wbBitmap;
                        rectList.Add(blobs[i].Rectangle);
                    }
                    else
                    {
                        AddBlobs(ref blobsImage, ref bitmap8bpp, ref blobs[i]);
                        rectList.Add(blobs[i].Rectangle);
                    }
                }
            }

            blobs = null;

            WriteableBitmap blobsImage16 = null;

            if (blobsImage != null)
            {
                // Convert 8bpp image to 16bpp
                blobsImage16 = Convert8bppTo16bpp(blobsImage);

                if (rectList.Count > 0)
                {
                    // Fill the blob within the blobs' rectangle
                    FillBlobs(ref blobsImage16, rectList);
                }
                else
                {
                    // Did not find any blob (make the whole image the blob).
                    //System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, (int)blobsImage.Width, (int)blobsImage.Height);

                    // Did not find any blob (make the selected region the blob).
                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle((int)clipRect.X, (int)clipRect.Y, (int)clipRect.Width, (int)clipRect.Height);
                    rectList.Add(rect);
                    FillBlobs(ref blobsImage16, rectList);
                }

                // scale the image
                //blobsImage16 = imageArith.Multiply(blobsImage16, 65535);
            }

            return blobsImage16;
        }

        private static void AddBlobs(ref WriteableBitmap blobsImage, ref System.Drawing.Bitmap bitmap8bpp, ref Blob blob)
        {
            ImageArithmetic imageArith = new ImageArithmetic();
            WriteableBitmap wbBitmap = new WriteableBitmap(bitmap8bpp.Width, bitmap8bpp.Height, 96, 96, PixelFormats.Gray8, null);
            int iBufferSize = wbBitmap.PixelHeight * wbBitmap.BackBufferStride;

            wbBitmap.WritePixels(new Int32Rect(0, 0, bitmap8bpp.Width, bitmap8bpp.Height), blob.Image.ImageData, iBufferSize, wbBitmap.BackBufferStride);

            blobsImage = imageArith.AddImage(ref blobsImage, ref wbBitmap);

            wbBitmap = null;
            // Without forcing garbage collection here, it would throw low memory exception
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        /// <summary>
        /// Fill the blobs's rectangle with pixel value 255 if not 255.
        /// </summary>
        /// <param name="srcImage"></param>
        /// <param name="rectList"></param>
        private static unsafe void FillBlobs(ref WriteableBitmap srcImage, List<System.Drawing.Rectangle> rectList)
        {
            if (srcImage == null && rectList.Count == 0)
            {
                return;
            }

            int bufferWidth = srcImage.BackBufferStride;
            byte* pSrcBuffer = (byte*)srcImage.BackBuffer.ToPointer();
            ushort* pSrc16 = null;

            foreach (var rect in rectList)
            {
                int x = rect.X;
                int y = rect.Y;
                int width = rect.Width;
                int height = rect.Height;

                for (int i = 0; i < height; i++)
                {
                    pSrc16 = (ushort*)(pSrcBuffer + ((i + y) * bufferWidth));
                    for (int j = 0; j < width; j++)
                    {
                        if (*(pSrc16 + x) != 255)
                        {
                            *(pSrc16 + x) = 255;
                        }
                        pSrc16++;
                    }
                }
            }


        }




        #region Helper methods
        //
        // Finding median without sorting
        // http://www.i-programmer.info/babbages-bag/505-quick-median.html
        //
        private static void split(
           ushort[] a,
           int n,
           ushort x,
           ref int i, ref int j)
        {
            // do the left and right scan until the pointers cross
            do
            {
                // scan from the left then scan from the right
                while (a[i] < x) i++;
                while (x < a[j]) j--;

                // now swap values if they are in the wrong part:
                if (i <= j)
                {
                    ushort t = a[i];
                    a[i] = a[j];
                    a[j] = t;
                    i++; j--;
                }

                // and continue the scan until the pointers cross:
            } while (i <= j);
        }

        public static void QuickMedian(
           ushort[] a,
           int n,
           ref int k)
        {
            int L = 0;
            int R = n - 1;
            k = n / 3;
            int i; int j;
            while (L < R)
            {
                ushort x = a[k];
                i = L; j = R;
                split(a, n, x, ref i, ref j);
                if (j < k) L = i;
                if (k < i) R = j;
            }
        }
        #endregion

        public static unsafe WriteableBitmap FrameToBitmap(ushort[] dataToConvert, Int32 width, Int32 height)
        {
            WriteableBitmap destBMP = new WriteableBitmap(width, height, 300, 300, PixelFormats.Gray16, null);
            //int iStride = ((width * 16) + 31) / 32 * 4;   //Getting "Buffer size is not sufficient" with this stride calculation
            int iStride = (width * 16 + 7) / 8;
            destBMP.WritePixels(new Int32Rect(0, 0, width, height), dataToConvert, iStride, 0);
            return destBMP;
        }

        public static unsafe void FrameToBitmap(ushort[] dataToConvert, ref WriteableBitmap wbBitmap)
        {
            int width = wbBitmap.PixelWidth;
            int height = wbBitmap.PixelHeight;
            //int iStride = ((width * 16) + 31) / 32 * 4;   //Getting "Buffer size is not sufficient" with this stride calculation
            int iStride = (width * 16 + 7) / 8;
            wbBitmap.WritePixels(new Int32Rect(0, 0, width, height), dataToConvert, iStride, 0);
        }

        #region pseudo color image processing
        /// <summary> 
        /// Pseudo color image processing x garden blog - first public date: 2014.2.14 
        /// </summary> 
        /// <param name="bmp">The gray image into</param> 
        /// <param name="method">What method is used, the strength of false layer, true grayscale color transformation method</param> 
        /// <param name="seg">Stratified strength stratification in number</param> 
        /// <returns>Return false color image</returns>
        private System.Drawing.Bitmap gcTrans(System.Drawing.Bitmap bmp, bool method, byte seg)
        {
            if (bmp != null)
            {
                if (System.Drawing.Imaging.PixelFormat.Format24bppRgb == bmp.PixelFormat)
                {
                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
                    System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, bmp.PixelFormat);
                    IntPtr ptr = bmpData.Scan0;
                    int bytes = bmp.Width * bmp.Height * 3;
                    byte[] grayValues = new byte[bytes];
                    System.Runtime.InteropServices.Marshal.Copy(ptr, grayValues, 0, bytes);
                    bmp.UnlockBits(bmpData);
                    byte[] rgbValues = new byte[bytes];
                    //Reset
                    Array.Clear(rgbValues, 0, bytes);
                    byte tempB;
                    if (method == false)
                    {
                        //The strength of stratification method
                        for (int i = 0; i < bytes; i += 3)
                        {
                            byte ser = (byte)(256 / seg);
                            tempB = (byte)(grayValues[i] / ser);
                            //The distribution of any color
                            rgbValues[i + 1] = (byte)(tempB * ser);
                            rgbValues[i] = (byte)((seg - 1 - tempB) * ser);
                            rgbValues[i + 2] = 0;
                        }
                    }
                    else
                    {
                        //Gray level-Color transformation method 
                        for (int i = 0; i < bytes; i += 3)
                        {
                            if (grayValues[i] < 64)
                            {
                                rgbValues[i + 2] = 0;
                                rgbValues[i + 1] = (byte)(4 * grayValues[i]);
                                rgbValues[i] = 255;
                            }
                            else if (grayValues[i] < 128)
                            {
                                rgbValues[i + 2] = 0;
                                rgbValues[i + 1] = 255;
                                rgbValues[i] = (byte)(-4 * grayValues[i] + 2 * 255);
                            }
                            else if (grayValues[i] < 192)
                            {
                                rgbValues[i + 2] = (byte)(4 * grayValues[i] - 2 * 255);
                                rgbValues[i + 1] = 255;
                                rgbValues[i] = 0;
                            }
                            else
                            {
                                rgbValues[i + 2] = 255;
                                rgbValues[i + 1] = (byte)(-4 * grayValues[i] + 4 * 255);
                                rgbValues[i] = 0;
                            }
                        }
                    }

                    bmp = new System.Drawing.Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, bmp.PixelFormat);
                    ptr = bmpData.Scan0;
                    System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
                    bmp.UnlockBits(bmpData);

                    return bmp;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        #endregion
        #endregion unused function
    }

    /// <summary>
    /// Helper class
    /// </summary>
    class Peaks
    {
        public int index;
        public int value;
        public Peaks(int index, int value)
        {
            this.index = index;
            this.value = value;
        }
    }

    public static class ImageHelpers
    {
        public static BitmapImage ToBitmapImage(this WriteableBitmap wbm)
        {
            BitmapImage bmImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(wbm));
                encoder.Save(stream);
                bmImage.BeginInit();
                bmImage.CacheOption = BitmapCacheOption.OnLoad;
                bmImage.StreamSource = stream;
                bmImage.EndInit();
                bmImage.Freeze();
            }
            return bmImage;
        }
    }

}

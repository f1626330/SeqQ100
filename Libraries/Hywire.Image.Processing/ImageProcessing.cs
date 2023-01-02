using Sequlite.ALF.Imaging;
using Sequlite.Ipp.Imaging;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hywire.Image.Processing
{
    public class ImageProcessing
    {
        /// <summary>
        /// load image file
        /// </summary>
        /// <param name="sFileName">file name</param>
        /// <returns></returns>
        //public static WriteableBitmap Load(String @sFilePath)
        //{
        //    WriteableBitmap wbBitmap = null;
        //    try
        //    {
        //        using (var fileStream = new FileStream(@sFilePath, FileMode.Open, FileAccess.Read))
        //        {
        //            // read from file or write to file
        //            wbBitmap = CreateWriteableBitmap(fileStream);
        //            //if (wbBitmap.CanFreeze)
        //            //    wbBitmap.Freeze();
        //            fileStream.Close();
        //        }
        //    }
        //    catch (Exception)
        //    {
        //        throw;
        //    }

        //    return wbBitmap;
        //}
        //public static void Save(string strFilePath, WriteableBitmap srcImage, ImageInfo srcImageInfo, bool bCompressed, bool bOverrideDefaultDpi = false)
        //{
        //    using (FileStream fileStream = new System.IO.FileStream(strFilePath, System.IO.FileMode.Create, FileAccess.ReadWrite))
        //    {
        //        WriteableBitmap imageToSave = null;
        //        BitmapMetadata metadata = null;

        //        if (srcImageInfo == null)
        //        {
        //            srcImageInfo = new ImageInfo();
        //        }
        //        else
        //        {
        //            //Invert chemi image.
        //            if (srcImageInfo.MixChannel.IsInvertChecked == true)
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
        //            double dDpiY = imageToSave.DpiY;

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

        //        TiffBitmapEncoder encoder = new TiffBitmapEncoder();
        //        try
        //        {
        //            if (bCompressed)
        //            {
        //                encoder.Compression = TiffCompressOption.Lzw;
        //            }
        //            else
        //            {
        //                encoder.Compression = TiffCompressOption.None;
        //            }

        //            if (imageToSave.Metadata != null)
        //            {
        //                metadata = (BitmapMetadata)imageToSave.Metadata.Clone();
        //            }
        //            else
        //            {
        //                metadata = new BitmapMetadata("tiff");
        //            }

        //            //uint paddingAmount = 4096; // 4Kb padding
        //            //metadata.SetQuery("/ifd/PaddingSchema:Padding", paddingAmount);
        //            //metadata.SetQuery("/ifd/exif/PaddingSchema:Padding", paddingAmount);
        //            //metadata.SetQuery("/xmp/PaddingSchema:Padding", paddingAmount);

        //            ImageChannelType currentSelected = srcImageInfo.SelectedChannel;

        //            srcImageInfo.SelectedChannel = ImageChannelType.Mix;

        //            metadata.ApplicationName = "Sequlite ALF";
        //            //metadata.SetQuery("/ifd/exif:{ushort=271}", "Azure Biosystems, Inc.");   // Make
        //            //metadata.DateTaken = srcImageInfo.DateTime;
        //            byte[] metadataByte = ObjectToByteArray(srcImageInfo);   // serialize image info
        //            metadata.SetQuery("/ifd/{ushort=40092}", metadataByte);  // save image info in metadata's comments tag

        //            BitmapFrame tifFrame = null;
        //            tifFrame = BitmapFrame.Create(imageToSave, null, metadata, null);
        //            encoder.Frames.Add(tifFrame);

        //            encoder.Save(fileStream);

        //            srcImageInfo.SelectedChannel = currentSelected;

        //            metadataByte = null;
        //            tifFrame = null;
        //        }
        //        catch (System.Exception ex)
        //        {
        //            throw ex;
        //        }

        //        fileStream.Close();
        //        fileStream.Dispose();
        //    }
        //}

        //public static unsafe WriteableBitmap Invert(WriteableBitmap srcBitmap)
        //{
        //    PixelFormatType pixelFormat = IppImaging.GetPixelFormatType(srcBitmap.Format);
        //    //if (!IsSupportedPixelFormat(pixelFormat))
        //    //{
        //    //    throw new Exception("Image type currently not supported");
        //    //}

        //    int width = srcBitmap.PixelWidth;
        //    int height = srcBitmap.PixelHeight;
        //    double dDpiX = srcBitmap.DpiX;
        //    double dDpiY = srcBitmap.DpiY;
        //    WriteableBitmap dstBitmap = new WriteableBitmap(width, height, dDpiX, dDpiY, srcBitmap.Format, null);

        //    byte* pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();
        //    byte* pDstData = (byte*)dstBitmap.BackBuffer.ToPointer();

        //    //int srcStep = ((srcBitmap.PixelWidth * srcBitmap.Format.BitsPerPixel) + 31) / 32 * 4;
        //    int srcStep = srcBitmap.BackBufferStride;
        //    int dstSetp = dstBitmap.BackBufferStride;

        //    IppiSize roiSize = new IppiSize(width, height);
        //    IppImaging.Invert(pSrcData, srcStep, roiSize, pixelFormat, pDstData, dstSetp);

        //    //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        //    //GC.WaitForPendingFinalizers();
        //    //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        //    //GC.Collect();

        //    return dstBitmap;
        //}

        //public static byte[] ObjectToByteArray(Object obj)
        //{
        //    if (obj == null) { return null; }

        //    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        //    MemoryStream ms = new MemoryStream();
        //    binFormatter.Serialize(ms, obj);

        //    return ms.ToArray();
        //}

        //public static Object ByteArrayToObject(byte[] arrBytes)
        //{
        //    if (arrBytes == null) { return null; }

        //    MemoryStream memStream = new MemoryStream();
        //    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        //    memStream.Write(arrBytes, 0, arrBytes.Length);
        //    memStream.Seek(0, SeekOrigin.Begin);
        //    Object obj = (Object)binFormatter.Deserialize(memStream);

        //    return obj;
        //}

        //public static ImageInfo ReadMetadata(string filename)
        //{
        //    byte[] byteArray = null;
        //    ImageInfo imageInfo = null;
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
        //                byteArray = (byte[])metadata.GetQuery("/ifd/{ushort=40092}");
        //                imageInfo = (ImageInfo)ByteArrayToObject(byteArray);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        imageInfo = null;
        //        //throw ex;
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

        //private static WriteableBitmap CreateWriteableBitmap(Stream stream)
        //{
        //    BitmapImage bi = new BitmapImage();

        //    // Begin initialization.
        //    bi.BeginInit();
        //    // Set properties.
        //    bi.CacheOption = BitmapCacheOption.OnLoad;
        //    bi.CreateOptions = BitmapCreateOptions.None | BitmapCreateOptions.PreservePixelFormat;
        //    bi.StreamSource = stream;
        //    // End initialization.
        //    bi.EndInit();
        //    bi.Freeze();

        //    PixelFormat pixelFormat;

        //    // Convert a BitmapSource to a Different PixelFormat
        //    if (bi.Format == PixelFormats.Bgr24 || bi.Format == PixelFormats.Bgr32 || bi.Format == PixelFormats.Bgra32)
        //        pixelFormat = PixelFormats.Rgb24;
        //    else
        //        pixelFormat = bi.Format;

        //    BitmapPalette palette = bi.Palette;

        //    BitmapSource source = new FormatConvertedBitmap(bi, pixelFormat, palette, 0);
        //    WriteableBitmap bmp = new WriteableBitmap(source);

        //    bi = null;

        //    return bmp;
        //}

    }
}

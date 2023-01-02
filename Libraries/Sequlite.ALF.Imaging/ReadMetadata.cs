using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Sequlite.ALF.Imaging
{
    public static class ReadMetadata
    {
        public static ImageInfo ReadImageInfo(string filename)
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
                        byteArray = (byte[])metadata.GetQuery("/ifd/{ushort=40092}");
                        imageInfo = (ImageInfo)ByteArrayToObject(byteArray);
                    }
                }
            }
            catch (Exception ex)
            {
                imageInfo = null;
                //throw ex;
            }
            finally
            {
                srcBitmap = null;
                metadata = null;
                byteArray = null;
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.Collect();
            }

            return imageInfo;
        }

        public static Sequlite.Image.Processing.ImageInfo ReadImageInfoAndConvert(string filename)
        {
            Sequlite.ALF.Imaging.ImageInfo imageinfo = ReadImageInfo(filename);
            if (imageinfo != null)
            {
                Sequlite.Image.Processing.ImageInfo _ImageInfo = new Sequlite.Image.Processing.ImageInfo();

                _ImageInfo.DateTime = imageinfo.DateTime;
                _ImageInfo.MixChannel.Exposure = imageinfo.MixChannel.Exposure;
                _ImageInfo.BinFactor = imageinfo.BinFactor;
                _ImageInfo.ReadoutSpeed = imageinfo.ReadoutSpeed;
                _ImageInfo.GainValue = imageinfo.GainValue;
                _ImageInfo.MixChannel.LightSource = imageinfo.MixChannel.LightSource;
                _ImageInfo.MixChannel.LightIntensity = imageinfo.MixChannel.LightIntensity;
                _ImageInfo.SoftwareVersion = imageinfo.SoftwareVersion;
                _ImageInfo.MixChannel.FocusPosition = imageinfo.MixChannel.FocusPosition;
                _ImageInfo.MixChannel.YPosition = imageinfo.MixChannel.YPosition;
                _ImageInfo.MixChannel.FilterPosition = imageinfo.MixChannel.FilterPosition;
                _ImageInfo.MixChannel.ROI = imageinfo.MixChannel.ROI;
                _ImageInfo.MixChannel.PDValue = imageinfo.MixChannel.PDValue;
                return _ImageInfo;
            }
            else
            {
                return null;
            }
        }

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
    }
}

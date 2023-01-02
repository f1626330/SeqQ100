using System.Windows.Media.Imaging;

namespace Sequlite.Image.Processing
{
    public static class BadImageIdentifier
    {
        #region Private Fields
        #endregion Private Fields

        #region Public Functions
        public static unsafe bool IsBadImage(WriteableBitmap img)
        {
            int pixelBitsWidth = img.Format.BitsPerPixel / 8;
            byte* ptr = (byte*)img.BackBuffer.ToPointer();

            int[] columnSum = new int[img.PixelWidth];

            for (int i = 0; i < img.PixelWidth; i++)
            {
                columnSum[i] = 0;
                for (int j = 0; j < img.PixelHeight; j++)
                {
                    int pixelVal = 0;
                    if (pixelBitsWidth == 2)
                    {
                        pixelVal = *((ushort*)(ptr + j * img.BackBufferStride + i * 2));
                    }
                    else
                    {
                        pixelVal = *(ptr + j * img.BackBufferStride + i);
                    }

                    // check if any zero pixels
                    if (pixelVal == 0)
                    {
                        return true;
                    }

                    columnSum[i] += pixelVal;
                }
            }

            return false;
        }

        public static bool IsBadImage(byte[] dataarray, int width, int pixelformat)
        {
            int dataperpixel = pixelformat /8;
            int numpixel = dataarray.Length / dataperpixel;
            int zeropcount = 0;
            for(int i = 0; i < numpixel; i += width * dataperpixel)
            {
                if(dataarray[i] + dataarray[i+1] == 0) 
                {
                    return true;
                }
                if(zeropcount > 5) { return true; }
            }
            return false;
        }
        #endregion Public Functions
    }
}

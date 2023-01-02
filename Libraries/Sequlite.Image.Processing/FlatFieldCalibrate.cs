using System.Drawing;
using System.Windows.Media.Imaging;

namespace Sequlite.Image.Processing
{
    /// <summary>
    /// Helper class to apply flat field calibration operation on an image, direct or "reversed"
    /// </summary>
    public class FlatFieldCalibrate
    {
        /// <summary>
        /// Applies flat field calibration operation on an image, direct or "reversed"
        /// </summary>
        /// <param name="sourceImage">Image to calibrate</param>
        /// <param name="flatImage">Flat field referense image</param>
        /// <param name="bReversed">Operation reverse flag</param>
        /// <returns>true if successful, false if fails</returns>
        public static unsafe bool Apply(ref WriteableBitmap theSrcImage, ref WriteableBitmap theFlatImage, bool bReversed)
        {
            ushort width = (ushort)theSrcImage.PixelWidth;
            ushort height = (ushort)theSrcImage.PixelHeight;

            if ((theFlatImage.PixelWidth != width) || (theFlatImage.PixelHeight != height))
            {
                return false;
            }

            Rectangle rectROI = new Rectangle(0, 0, theSrcImage.PixelWidth, theSrcImage.PixelHeight);

            ImageStatistics imageStatistics = new ImageStatistics();
            double flatAvg = imageStatistics.GetAverage(theFlatImage);

            if (flatAvg == 0)
            {
                return false;
            }

            for (ushort i = 0; i < height; i++)
            {
                ushort* pImgWord = ((ushort*)theSrcImage.BackBuffer.ToPointer()) + i * theSrcImage.BackBufferStride / 2;
                ushort* pFlatWord = ((ushort*)theFlatImage.BackBuffer.ToPointer()) + i * theFlatImage.BackBufferStride / 2;

                if (!bReversed)
                    for (ushort j = 0; j < width; j++)
                    {
                        // Direct flat correction:

                        if (pFlatWord[j] != 0)
                        {
                            int value = (int)(0.5 + flatAvg * pImgWord[j] / pFlatWord[j]);

                            if (value > 65535)
                            {
                                value = 65535;
                            }
                            else if (value < 0)
                            {
                                value = 0;
                            }

                            pImgWord[j] = (ushort)value;
                        }
                    }
                else
                {
                    for (ushort j = 0; j < width; j++)
                    {
                        // Reversed flat correction:

                        int value = (int)(0.5 + (double)pImgWord[j] * pFlatWord[j] / flatAvg);

                        if (value > 65535)
                        {
                            value = 65535;
                        }
                        else if (value < 0)
                        {
                            value = 0;
                        }

                        pImgWord[j] = (ushort)value;
                    }
                }
            }

            //int stride = theSrcImage.BackBufferStride;
            //int bufferSize = height * stride;
            //theSrcImage = new WriteableBitmap(BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, (IntPtr)theSrcImage.BackBuffer, bufferSize, stride));

            return true;
        }
    }
}

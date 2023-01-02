using System;
using System.Windows.Media.Imaging;

namespace Sequlite.Image.Processing
{

    /// <summary>
    /// Non-linear 3x3 despeckle smoothing filter.
    /// </summary>
    public class Despeckle3x3Filter
    {
        #region Public Properties...
        #endregion


        /// <summary>
        /// Constructor.
        /// </summary>
        public Despeckle3x3Filter()
        {
        }

        /// <summary>
        /// Execute filter. The filter creates a copy of the source image and filters this
        /// copy. The source image is not changed by this method call.
        /// </summary>
        /// <param name="sourceImage"></param>
        /// <returns>The filtered image</returns>
        public WriteableBitmap Execute(WriteableBitmap sourceImage)
        {
            if (sourceImage == null || sourceImage.PixelWidth == 0 || sourceImage.PixelHeight == 0)
            {
                throw new ArgumentNullException("Source image for filter cannot be null or empty.");
            }

            WriteableBitmap filteredCopy = null;

            switch (sourceImage.Format.BitsPerPixel)
            {
                case 8:
                    throw new NotImplementedException("8-bit image channel format is not yet implemented.");

                case 16:
                    filteredCopy = Process16BitChannel(sourceImage);
                    break;

                case 24:
                    throw new NotImplementedException("24-bit image channel format is not yet implemented.");

                case 32:
                    throw new NotImplementedException("32-bit image channel format is not yet implemented.");

                default:
                    throw new ArgumentException("Unsupported image channel format.");
            }

            return filteredCopy;
        }

        /// <summary>
        /// Process an 16-bit image.
        /// </summary>
        /// <param name="sourceImage"></param>
        /// <param name="coeffs"></param>
        /// <returns>The filtered image.</returns>
        private unsafe WriteableBitmap Process16BitChannel(WriteableBitmap srcImage)
        {
            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            int imageWidth = srcImage.PixelWidth;
            int imageHeight = srcImage.PixelHeight;
            double mean = 0.0;
            double median = 0.0;
            double stdDev = 0.0;
            Int64 sumOfSquare = 0;
            double testVal = 0.0;
            ushort[] pixelPopulation = new ushort[8];

            // Reserve the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();

            // Get a pointer to the back buffer.
            ushort* pOrigData = (ushort*)srcImage.BackBuffer.ToPointer();
            ushort* pCopyOfData = (ushort*)cpyImage.BackBuffer.ToPointer();

            fixed (void* pPtr = &pixelPopulation[0])
            {
                ushort* pPopulation = (ushort*)pPtr;
                //
                // Note: Border pixels are untouched (TODO)
                //
                for (int iRow = 1; iRow <= imageHeight - 2; ++iRow)
                {
                    for (int iCol = 1; iCol <= imageWidth - 2; ++iCol)
                    {
                        //
                        // Flatten the 2-D loop (the looping over 3x3 matrix) around target pixel into a 1-D array 
                        // Note: Use of pointers is quicker than array indexing due to the bounds checking .NET 
                        // languages use:
                        //
                        *(pPopulation) = *(pOrigData + (iCol - 1) + imageWidth * (iRow - 1));
                        *(pPopulation + 1) = *(pOrigData + iCol + imageWidth * (iRow - 1));
                        *(pPopulation + 2) = *(pOrigData + (iCol + 1) + imageWidth * (iRow - 1));

                        *(pPopulation + 3) = *(pOrigData + (iCol - 1) + imageWidth * iRow);
                        //Filtered (target) pixel would go here if we were using it.
                        *(pPopulation + 4) = *(pOrigData + (iCol + 1) + imageWidth * iRow);

                        *(pPopulation + 5) = *(pOrigData + (iCol - 1) + imageWidth * (iRow + 1));
                        *(pPopulation + 6) = *(pOrigData + iCol + imageWidth * (iRow + 1));
                        *(pPopulation + 7) = *(pOrigData + (iCol + 1) + imageWidth * (iRow + 1));

                        mean = 0.0;
                        median = 0.0;
                        sumOfSquare = 0;

                        for (int i = 0; i < 8; ++i)
                        {
                            mean += *(pPopulation + i);
                            sumOfSquare += (Int64)(*(pPopulation + i)) * (*(pPopulation + i));
                        }

                        mean = mean / 8.0;
                        //stdDev = Math.Sqrt((sumOfSquare - (8.0 * (mean * mean))) / 7.0); //N-1 for "sample standard deviation"
                        //testVal = Math.Abs(mean - *(pOrigData + iCol + imageWidth * iRow));
                        Array.Sort(pixelPopulation);
                        median = (pixelPopulation[3] + pixelPopulation[4]) / 2.0;
                        stdDev = Math.Sqrt((sumOfSquare - (8.0 * (mean * mean))) / 7.0); //N-1 for "sample standard deviation"
                        testVal = Math.Abs(median - *(pOrigData + iCol + imageWidth * iRow));

                        //
                        // If the pixel under test is not within 2 standard deviations of the average then replace 
                        // it with the average value (else, don't change it):
                        //
                        if (testVal > 2 * stdDev)
                        {
                            //*(pCopyOfData + iCol + imageWidth * iRow) = (ushort)mean;
                            *(pCopyOfData + iCol + imageWidth * iRow) = (ushort)median;
                        }
                    }
                }

            }//end fixed

            //int stride = srcImage.BackBufferStride;
            //int bufferSize = imageHeight * stride;
            //WriteableBitmap filteredImage = new WriteableBitmap(BitmapSource.Create(imageWidth, imageHeight, 96, 96, PixelFormats.Gray16, null, (IntPtr)cpyImage.BackBuffer, bufferSize, stride));

            //cpyImage.AddDirtyRect(new Int32Rect(0, 0, imageWidth, imageHeight));

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();

            //return filteredImage;
            return cpyImage;
        }

    }




}

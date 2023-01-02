using System;
using System.Windows.Media.Imaging;

namespace Sequlite.Image.Processing
{

    /// <summary>
    /// Rank filter.
    /// </summary>
    public class RankFilter
    {
        #region Public Properties...
        #endregion


        /// <summary>
        /// Constructor.
        /// </summary>
        public RankFilter()
        {
        }

        /// <summary>
        /// Execute filter. The filter creates a copy of the source image and filters this
        /// copy. The source image is not changed by this method call.
        /// </summary>
        /// <param name="sourceImage"></param>
        /// <returns>The filtered image</returns>
        public WriteableBitmap Execute(WriteableBitmap sourceImage, int kernelSize)
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
                    filteredCopy = Process16BitChannel(sourceImage, kernelSize);
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
        private unsafe WriteableBitmap Process16BitChannel(WriteableBitmap srcImage, int kernel)
        {
            if (kernel == 0)
            {
                return srcImage;
            }
            else
            {
                kernel = 2 * kernel + 1;
            }

            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            int imageWidth = srcImage.PixelWidth;
            int imageHeight = srcImage.PixelHeight;
            double mean = 0.0;
            double stdDev = 0.0;
            Int64 sumOfSquare = 0;
            //double testVal = 0.0;
            ushort min = 65535;
            int populationSize = kernel * kernel - 1;
            ushort[] pixelPopulation = new ushort[populationSize];

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

                        int middlePixelIndex = 0;
                        int localTargetIndex = populationSize / 2;
                        int popIndex = 0;
                        bool hasTarget = false;

                        for (int i = 1; i <= kernel; i++)
                        {
                            for (int j = 1; j <= kernel; j++)
                            {
                                if (popIndex != localTargetIndex || (popIndex == localTargetIndex && hasTarget))
                                {
                                    *(pPopulation + popIndex++) = *(pOrigData + (iCol + (j - 2) + imageWidth * ((iRow - 1) + (i - 1))));
                                    hasTarget = false;
                                }
                                else
                                {
                                    middlePixelIndex = iCol + (j - 2) + imageWidth * ((iRow - 1) + (i - 1));
                                    hasTarget = true;
                                }
                            }
                        }

                        mean = 0.0;
                        sumOfSquare = 0;
                        min = 65535;

                        for (int i = 0; i < populationSize; ++i)
                        {
                            mean += *(pPopulation + i);
                            sumOfSquare += (Int64)(*(pPopulation + i)) * (*(pPopulation + i));

                            if (*(pPopulation + i) < min)
                            {
                                min = *(pPopulation + i);
                            }
                        }

                        mean = mean / populationSize;
                        //Array.Sort(pixelPopulation);
                        //int mid = populationSize / 2;
                        //median = (double)(pixelPopulation[mid - 1] + pixelPopulation[mid]) / 2.0;    // zero index
                        //double min = pixelPopulation[0];
                        stdDev = Math.Sqrt((sumOfSquare - (populationSize * (mean * mean))) / (populationSize - 1)); //N-1 for "sample standard deviation"
                                                                                                                     //testVal = Math.Abs(*(pOrigData + targetPixelIndex) - median);

                        if (*(pOrigData + middlePixelIndex) - mean > 0)
                        {
                            *(pCopyOfData + middlePixelIndex) = (ushort)min;
                        }
                    }
                }

            }//end fixed

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();

            return cpyImage;
        }

    }




}

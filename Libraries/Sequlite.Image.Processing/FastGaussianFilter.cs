using System;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace Sequlite.Image.Processing
{
    /// <summary>
    /// This filter performs an average filter on an image. An average filter
    /// replaces pixel (i, j) with the average of pixels in rectangle of kernel
    /// size with (i, j) as the center.
    /// </summary>
    public class FastGaussianFilter
    {
        private Size kernel;

        public Size Kernel
        {
            get { return kernel; }
            set
            {
                if (value.Width < 3) { value.Width = 3; }
                if (value.Height < 3) { value.Height = 3; }

                kernel = value;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="kernel"></param>
        public FastGaussianFilter(Size kernel)
        {
            this.Kernel = kernel;
        }

        public unsafe bool Apply(ref WriteableBitmap srcImage)
        {
            if (srcImage.Format.BitsPerPixel != 16)
            {
                return false;
            }

            int kernelSize = this.Kernel.Width;
            kernelSize = (kernelSize / 2 * 2 + 1);

            if ((kernelSize > srcImage.PixelWidth - 3) || (kernelSize > srcImage.PixelHeight - 3))
            {
                return false;
            }

            int halfKernel = kernelSize / 2;

            double standardDev = (double)kernelSize / 5;

            double[] oneDkernel = new double[kernelSize];

            // Calculate 1D kernel array for fast gaussian:
            double k = kernelSize / (Math.Sqrt(Math.PI * 2) * standardDev);

            for (int i = 0; i < kernelSize; i++)
            {
                oneDkernel[i] = k * Math.Exp(
                        -((i - halfKernel) * (i - halfKernel) / (standardDev * standardDev * 2)));
            }

            // Copy of the original image will be used as source image for filtering
            WriteableBitmap cpyImage = (WriteableBitmap)srcImage.Clone();

            // Reserves the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();

            ushort* pResBuffer = (ushort*)srcImage.BackBuffer;
            ushort* pBuffer = (ushort*)cpyImage.BackBuffer;

            int bufWidth = (int)srcImage.BackBufferStride / 2;      // Width in words.

            int start = kernelSize / 2;
            int endX = srcImage.PixelWidth - kernelSize / 2;
            int endY = srcImage.PixelHeight - kernelSize / 2;

            int width = srcImage.PixelWidth;
            int height = srcImage.PixelHeight;

            // First pass - horizontal direction:
            for (int i = 0; i < height; i++)
            {
                ushort* pSrcRow = pBuffer + i * bufWidth;
                ushort* pResRow = pResBuffer + i * bufWidth;

                for (int j = 0; j < width; j++)
                {
                    int startKernel = 0;
                    if (j < halfKernel)
                    {
                        startKernel = halfKernel - j;
                    }

                    int endKernel = kernelSize;
                    if ((width - j - 1) < halfKernel)
                    {
                        endKernel = kernelSize - (halfKernel - (width - j - 1));
                    }

                    int pixelXpos = j - (halfKernel - startKernel);

                    double pixValue = 0.0;
                    double normFactor = 0.0;

                    for (int l = startKernel; l < endKernel; l++, pixelXpos++)
                    {
                        pixValue += ((double)pSrcRow[pixelXpos]) * oneDkernel[l];
                        normFactor += oneDkernel[l];
                    }

                    pResRow[j] = (ushort)(pixValue / normFactor);
                }
            }

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();
            cpyImage = null;
            cpyImage = (WriteableBitmap)srcImage.Clone();
            pBuffer = (ushort*)cpyImage.BackBuffer;
            // Reserves the back buffer for updates.
            srcImage.Lock();
            cpyImage.Lock();

            // Second pass - vertical direction (vertically oriented 1D kernel):
            for (int i = 0; i < height; i++)
            {
                ushort* pSrcRow = pBuffer + i * bufWidth;
                ushort* pResRow = pResBuffer + i * bufWidth;

                int startKernel = 0;
                if (i < halfKernel)
                {
                    startKernel = halfKernel - i;
                }

                int endKernel = kernelSize;
                if ((height - i - 1) < halfKernel)
                {
                    endKernel = kernelSize - (halfKernel - (height - i - 1));
                }

                for (int j = 0; j < width; j++)
                {
                    int pixelXpos = j - bufWidth * (halfKernel - startKernel);

                    double pixValue = 0.0;
                    double normFactor = 0.0;

                    for (int l = startKernel; l < endKernel; l++, pixelXpos += bufWidth)
                    {
                        pixValue += ((double)pSrcRow[pixelXpos]) * oneDkernel[l];
                        normFactor += oneDkernel[l];
                    }

                    pResRow[j] = (ushort)(pixValue / normFactor);
                }
            }

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();
            cpyImage.Unlock();
            cpyImage = null;

            //int stride = srcImage.BackBufferStride;
            //int bufferSize = height * stride;
            //srcImage = new WriteableBitmap(BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, (IntPtr)srcImage.BackBuffer, bufferSize, stride));

            return true;
        }


    }
}

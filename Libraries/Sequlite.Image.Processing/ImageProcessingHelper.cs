using Sequlite.Image.Processing.Imaging;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sequlite.Image.Processing
{
    public static class ImageProcessingHelper
    {
        /// <summary>
        /// Get pixel intensity at the specified coordinate
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="iCol"></param>
        /// <param name="iRow"></param>
        /// <param name="iRedData"></param>
        /// <param name="iGreenData"></param>
        /// <param name="iBlueData"></param>
        public static unsafe void GetPixelIntensity(WriteableBitmap srcImage, Point pt, ref int iRedData, ref int iGreenData, ref int iBlueData, ref int iGrayData)
        {
            if (srcImage == null)
            {
                iRedData = 0;
                iBlueData = 0;
                iGreenData = 0;
                iGrayData = 0;
                return;
            }

            int imageHeight = srcImage.PixelHeight;
            int imageWidth = srcImage.PixelWidth;
            long bufferWidth = srcImage.BackBufferStride;
            byte* pdata = (byte*)srcImage.BackBuffer.ToPointer();
            long x = (long)pt.X;
            long y = (long)pt.Y;

            if (x < 0) { x = 0; }
            if (y < 0) { y = 0; }
            if (x > imageWidth) { x = imageWidth; }
            if (y > imageHeight) { y = imageHeight; }

            try
            {
                if (srcImage.Format == PixelFormats.Gray8 ||
                    srcImage.Format == PixelFormats.Indexed8)
                {
                    iRedData = *(pdata + y * bufferWidth + x);
                    iGreenData = iBlueData = iRedData;
                }
                else if (srcImage.Format == PixelFormats.Rgb24)
                {
                    iRedData = *(pdata + y * bufferWidth + x * 3);
                    iGreenData = *(pdata + y * bufferWidth + x * 3 + 1);
                    iBlueData = *(pdata + y * bufferWidth + x * 3 + 2);
                }
                else if (srcImage.Format == PixelFormats.Bgr24)
                {
                    iBlueData = *(pdata + y * bufferWidth + x * 3);
                    iGreenData = *(pdata + y * bufferWidth + x * 3 + 1);
                    iRedData = *(pdata + y * bufferWidth + x * 3 + 2);
                }
                else if (srcImage.Format == PixelFormats.Gray16)
                {
                    iRedData = *((ushort*)(pdata + y * bufferWidth + x * 2));
                    iGreenData = iBlueData = iRedData;
                }
                else if (srcImage.Format == PixelFormats.Rgb48)
                {
                    iRedData = *((ushort*)(pdata + y * bufferWidth + x * 6));
                    iGreenData = *((ushort*)(pdata + y * bufferWidth + x * 6 + 2));
                    iBlueData = *((ushort*)(pdata + y * bufferWidth + x * 6 + 4));
                }
                else if (srcImage.Format == PixelFormats.Rgba64)
                {
                    iRedData = *((ushort*)(pdata + y * bufferWidth + x * 8));
                    iGreenData = *((ushort*)(pdata + y * bufferWidth + x * 8 + 2));
                    iBlueData = *((ushort*)(pdata + y * bufferWidth + x * 8 + 4));
                    iGrayData = *((ushort*)(pdata + y * bufferWidth + x * 8 + 6));  //stored gray channel image in the 4th (alpha) channel
                }
            }
            catch (Exception)
            {
            }
        }

        /*public static WriteableBitmap UpdateDisplayImage(WriteableBitmap srcimg, ImageInfo imginfo)
        {
            if (srcimg == null) { return srcimg; }

            WriteableBitmap dspBitmap = null;
            WriteableBitmap[] imageChannels = { null, null, null };
            int iBlackValue = 0;
            int iWhiteValue = 0;
            double dGammaValue = 1.0;
            int iColorGradation;

            try
            {
                if (srcimg.Format.BitsPerPixel == 24 || srcimg.Format.BitsPerPixel == 48)
                {
                    #region === multi-channel image contrast ===

                    if (imginfo.SelectedChannel == ImageChannelType.Mix)
                    {
                        #region === multi-channel: composite contrast ===

                        dspBitmap = srcimg;

                        iBlackValue = imginfo.MixChannel.BlackValue;
                        iWhiteValue = imginfo.MixChannel.WhiteValue;
                        dGammaValue = imginfo.MixChannel.GammaValue;

                        if (imginfo.MixChannel.IsAutoChecked == true)
                        {
                            ImageProcessing.GetAutoScaleValues(dspBitmap, ref iWhiteValue, ref iBlackValue);
                            imginfo.MixChannel.BlackValue = iBlackValue;
                            imginfo.MixChannel.WhiteValue = iWhiteValue;
                            imginfo.MixChannel.GammaValue = 1.0;
                            dGammaValue = 1.0;
                        }

                        //Set all channel to same contrast value
                        //Black value
                        imginfo.RedChannel.BlackValue = imginfo.MixChannel.BlackValue;
                        imginfo.GreenChannel.BlackValue = imginfo.MixChannel.BlackValue;
                        imginfo.BlueChannel.BlackValue = imginfo.MixChannel.BlackValue;
                        imginfo.GrayChannel.BlackValue = imginfo.MixChannel.BlackValue;
                        //White value
                        imginfo.RedChannel.WhiteValue = imginfo.MixChannel.WhiteValue;
                        imginfo.GreenChannel.WhiteValue = imginfo.MixChannel.WhiteValue;
                        imginfo.BlueChannel.WhiteValue = imginfo.MixChannel.WhiteValue;
                        imginfo.GrayChannel.WhiteValue = imginfo.MixChannel.WhiteValue;
                        //Gamma value
                        imginfo.RedChannel.GammaValue = imginfo.MixChannel.GammaValue;
                        imginfo.GreenChannel.GammaValue = imginfo.MixChannel.GammaValue;
                        imginfo.BlueChannel.GammaValue = imginfo.MixChannel.GammaValue; 
                        imginfo.GrayChannel.GammaValue = imginfo.MixChannel.GammaValue;

                        iColorGradation = 3;
                        dspBitmap = ImageProcessing.Scale(dspBitmap,
                                                          iBlackValue,
                                                          iWhiteValue,
                                                          dGammaValue,
                                                          imginfo.MixChannel.IsInvertChecked,
                                                          imginfo.MixChannel.IsSaturationChecked,
                                                          imginfo.SaturationThreshold,
                                                          iColorGradation);

                        #endregion
                    }
                    else
                    {
                        #region === multi-channel: selected channel contrasting ===

                        WriteableBitmap blueChImg = null;
                        WriteableBitmap greenChImg = null;
                        WriteableBitmap redChImg = null;

                        imageChannels = ImageProcessing.GetChannel(srcimg);
                        if (imageChannels != null)
                        {
                            // GetChannel returns RGB color order
                            redChImg = imageChannels[0];
                            greenChImg = imageChannels[1];
                            blueChImg = imageChannels[2];
                        }

                        if (imginfo.SelectedChannel == ImageChannelType.Red)
                        {
                            #region === Red channel ===

                            iBlackValue = imginfo.RedChannel.BlackValue;
                            iWhiteValue = imginfo.RedChannel.WhiteValue;
                            dGammaValue = imginfo.RedChannel.GammaValue;

                            if (imginfo.RedChannel.IsAutoChecked == true)
                            {
                                ImageProcessing.GetAutoScaleValues(redChImg, ref iWhiteValue, ref iBlackValue);
                                imginfo.RedChannel.BlackValue = iBlackValue;
                                imginfo.RedChannel.WhiteValue = iWhiteValue;
                                imginfo.RedChannel.GammaValue = 1.0;
                                dGammaValue = 1.0;
                            }

                            if (imginfo.RedChannel.IsSaturationChecked == true)
                            {
                                iColorGradation = 0;
                                redChImg = ImageProcessing.Scale(redChImg,
                                                                 iBlackValue,
                                                                 iWhiteValue,
                                                                 dGammaValue,
                                                                 imginfo.RedChannel.IsInvertChecked,
                                                                 imginfo.RedChannel.IsSaturationChecked,
                                                                 imginfo.SaturationThreshold,
                                                                 iColorGradation);
                                dspBitmap = redChImg;
                                blueChImg = null;
                                greenChImg = null;
                            }
                            else
                            {
                                redChImg = ImageProcessing.Scale_C1(redChImg,
                                                                    iBlackValue,
                                                                    iWhiteValue,
                                                                    imginfo.RedChannel.GammaValue,
                                                                    imginfo.RedChannel.IsInvertChecked);
                                blueChImg = null;
                                greenChImg = null;
                                // SetChannel expect BGR color order
                                dspBitmap =  ImageProcessing.SetChannel(blueChImg, greenChImg, redChImg);
                            }

                            #endregion
                        } // end red channel
                        else if (imginfo.SelectedChannel == ImageChannelType.Green)
                        {
                            #region === Green channel ===

                            iBlackValue = imginfo.GreenChannel.BlackValue;
                            iWhiteValue = imginfo.GreenChannel.WhiteValue;
                            dGammaValue = imginfo.GreenChannel.GammaValue;
                            if (imginfo.GreenChannel.IsAutoChecked == true)
                            {
                                ImageProcessing.GetAutoScaleValues(greenChImg, ref iWhiteValue, ref iBlackValue);
                                imginfo.GreenChannel.BlackValue = iBlackValue;
                                imginfo.GreenChannel.WhiteValue = iWhiteValue;
                                imginfo.GreenChannel.GammaValue = 1.0;
                                dGammaValue = 1.0;
                            }

                            if (imginfo.GreenChannel.IsSaturationChecked == true)
                            {
                                iColorGradation = 1;
                                greenChImg = ImageProcessing.Scale(greenChImg,
                                                                   iBlackValue,
                                                                   iWhiteValue,
                                                                   dGammaValue,
                                                                   imginfo.GreenChannel.IsInvertChecked,
                                                                   imginfo.GreenChannel.IsSaturationChecked,
                                                                   imginfo.SaturationThreshold,
                                                                   iColorGradation);
                                dspBitmap = greenChImg;
                                greenChImg = null;
                                redChImg = null;
                            }
                            else
                            {
                                greenChImg = ImageProcessing.Scale_C1(greenChImg,
                                                                      iBlackValue,
                                                                      iWhiteValue,
                                                                      imginfo.GreenChannel.GammaValue,
                                                                      imginfo.GreenChannel.IsInvertChecked);
                                blueChImg = null;
                                redChImg = null;
                                // SetChannel expect BGR color order
                                dspBitmap = ImageProcessing.SetChannel(blueChImg, greenChImg, redChImg);
                            }

                            #endregion
                        } // end of green channel
                        else if (imginfo.SelectedChannel == ImageChannelType.Blue)
                        {
                            #region === Blue channel ===

                            iBlackValue = imginfo.BlueChannel.BlackValue;
                            iWhiteValue = imginfo.BlueChannel.WhiteValue;
                            dGammaValue = imginfo.BlueChannel.GammaValue;

                            if (imginfo.BlueChannel.IsAutoChecked == true)
                            {
                                ImageProcessing.GetAutoScaleValues(blueChImg, ref iWhiteValue, ref iBlackValue);
                                imginfo.BlueChannel.BlackValue = iBlackValue;
                                imginfo.BlueChannel.WhiteValue = iWhiteValue;
                                imginfo.BlueChannel.GammaValue = 1.0;
                                dGammaValue = 1.0;
                            }

                            if (imginfo.BlueChannel.IsSaturationChecked == true)
                            {
                                iColorGradation = 2;
                                blueChImg = ImageProcessing.Scale(blueChImg,
                                                                  iBlackValue,
                                                                  iWhiteValue,
                                                                  dGammaValue,
                                                                  imginfo.BlueChannel.IsInvertChecked,
                                                                  imginfo.BlueChannel.IsSaturationChecked,
                                                                  imginfo.SaturationThreshold,
                                                                  iColorGradation);
                                dspBitmap = blueChImg;
                                greenChImg = null;
                                redChImg = null;
                            }
                            else
                            {
                                blueChImg = ImageProcessing.Scale_C1(blueChImg,
                                                                     iBlackValue,
                                                                     iWhiteValue,
                                                                     dGammaValue,
                                                                     imginfo.BlueChannel.IsInvertChecked);
                                greenChImg = null;
                                redChImg = null;
                                // SetChannel expect BGR color order
                                dspBitmap = ImageProcessing.SetChannel(blueChImg, greenChImg, redChImg);
                            }

                            #endregion
                        } // end blue channel

                        #endregion
                    }

                    #endregion
                }
                else
                {
                    #region === single channel image contrast ===

                    dspBitmap = srcimg;

                    if (imginfo.MixChannel.IsAutoChecked == true)
                    {
                        if (imginfo.CaptureType != null && 
                            ((imginfo.CaptureType.Contains("Chemi") && imginfo.MixChannel.LightSource == 10) ||
                            (imginfo.CaptureType != null && imginfo.IsChemiImage)))
                        {
                            // Light source: 10 (None)
                            ImageProcessing.GetChemiAutoScaleValues(dspBitmap, ref iBlackValue, ref iWhiteValue);
                            imginfo.MixChannel.BlackValue = iBlackValue;
                            imginfo.MixChannel.WhiteValue = iWhiteValue;
                            imginfo.MixChannel.GammaValue = 1.00;
                        }
                        else if (imginfo.RedChannel.LightSource == 11)
                        {
                            // Light source: 11 (Visible)
                            ImageProcessing.GetVisibleAutoScaleValues(dspBitmap, ref iBlackValue, ref iWhiteValue);
                            imginfo.MixChannel.BlackValue = iBlackValue;
                            imginfo.MixChannel.WhiteValue = iWhiteValue;
                            imginfo.MixChannel.GammaValue = 1.00;
                        }
                        else
                        {
                            ImageProcessing.GetAutoScaleValues(dspBitmap, ref iWhiteValue, ref iBlackValue);
                            imginfo.MixChannel.BlackValue = iBlackValue;
                            imginfo.MixChannel.WhiteValue = iWhiteValue;
                            imginfo.MixChannel.GammaValue = 1.00;
                        }
                    }

                    iColorGradation = 3;    // composite image
                    dspBitmap = ImageProcessing.Scale(dspBitmap,
                                                      imginfo.MixChannel.BlackValue,
                                                      imginfo.MixChannel.WhiteValue,
                                                      imginfo.MixChannel.GammaValue,
                                                      imginfo.MixChannel.IsInvertChecked,
                                                      imginfo.MixChannel.IsSaturationChecked,
                                                      imginfo.SaturationThreshold,
                                                      iColorGradation);

                    #endregion
                }
            }
            catch (OutOfMemoryException ex)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

                throw new Exception(string.Format("{0}\nPlease close some images to clear up some memory.", ex.Message));
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Error updating the display image.\n{0}", ex.Message));
            }
            finally
            {
                //imageChannels = null;
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

                // Retrieves the number of bytes currently thought to be allocated (true: force full collection)
                //GC.GetTotalMemory(true);
            }

            return dspBitmap;
        }*/

        public static unsafe void UpdateDisplayImage(ref WriteableBitmap srcimg, ImageInfo imginfo, ref WriteableBitmap dstimg)
        {
            if (srcimg == null) { return; }

            int nWidth = srcimg.PixelWidth;
            int nHeight = srcimg.PixelHeight;
            PixelFormat srcPixelFormat = srcimg.Format;
            int nBlackValue = 0;
            int nWhiteValue = 0;
            double dGammaValue = 1.0;
            int nColorGradation;

            try
            {
                if (srcimg.Format.BitsPerPixel == 24 ||
                    srcimg.Format.BitsPerPixel == 48 ||
                    srcPixelFormat.BitsPerPixel == 64)
                {
                    #region === multi-channel image contrast ===

                    if (imginfo.SelectedChannel == ImageChannelType.Mix)
                    {
                        #region === multi-channel: composite contrast ===

                        if (imginfo.MixChannel.IsAutoChecked == true)
                        {
                            ImageProcessing.GetAutoScaleValues(srcimg, ref nWhiteValue, ref nBlackValue, ImageChannelType.Mix);
                            imginfo.MixChannel.BlackValue = nBlackValue;
                            imginfo.MixChannel.WhiteValue = nWhiteValue;
                            imginfo.MixChannel.GammaValue = dGammaValue = 1.0;
                        }
                        else
                        {
                            nBlackValue = imginfo.MixChannel.BlackValue;
                            nWhiteValue = imginfo.MixChannel.WhiteValue;
                            dGammaValue = imginfo.MixChannel.GammaValue;
                        }

                        // If chemi + marker contrast and merge the each channel with its own lookup table.
                        bool bIsMergeChannels = false;
                        PixelFormatType srcFormat = IppImaging.GetPixelFormatType(srcimg.Format);
                        if (srcFormat == PixelFormatType.P16u_C4 && imginfo.GrayChannel.IsInvertChecked)
                        {
                            if (imginfo.CaptureType.Contains("Chemi") && imginfo.CaptureType.Contains("Marker"))
                            {
                                bIsMergeChannels = true;
                            }
                        }

                        // Set all channel to same contrast value
                        //
                        // Black value
                        imginfo.RedChannel.BlackValue = imginfo.MixChannel.BlackValue;
                        imginfo.GreenChannel.BlackValue = imginfo.MixChannel.BlackValue;
                        imginfo.BlueChannel.BlackValue = imginfo.MixChannel.BlackValue;
                        imginfo.GrayChannel.BlackValue = imginfo.MixChannel.BlackValue;
                        // White value
                        imginfo.RedChannel.WhiteValue = imginfo.MixChannel.WhiteValue;
                        imginfo.GreenChannel.WhiteValue = imginfo.MixChannel.WhiteValue;
                        imginfo.BlueChannel.WhiteValue = imginfo.MixChannel.WhiteValue;
                        imginfo.GrayChannel.WhiteValue = imginfo.MixChannel.WhiteValue;
                        // Gamma value
                        imginfo.RedChannel.GammaValue = imginfo.MixChannel.GammaValue;
                        imginfo.GreenChannel.GammaValue = imginfo.MixChannel.GammaValue;
                        imginfo.BlueChannel.GammaValue = imginfo.MixChannel.GammaValue;
                        imginfo.GrayChannel.GammaValue = imginfo.MixChannel.GammaValue;

                        if (srcFormat == PixelFormatType.P8u_C4 ||
                            srcFormat == PixelFormatType.P16u_C4)
                        {
                            nColorGradation = 4;
                            ImageProcessing.Scale_16u8u_C4C3(ref srcimg, ref dstimg, imginfo, nColorGradation, bIsMergeChannels);
                        }
                        else
                        {
                            //System.Diagnostics.Stopwatch stopwatch1 = new System.Diagnostics.Stopwatch();
                            //stopwatch1.Start();

                            nColorGradation = 3;
                            //ImageProcessing.Scale(ref srcimg, ref dstimg,
                            //                      nBlackValue, nWhiteValue, dGammaValue,
                            //                      imginfo.MixChannel.IsInvertChecked,
                            //                      imginfo.MixChannel.IsSaturationChecked,
                            //                      imginfo.SaturationThreshold,
                            //                      nColorGradation, false);
                            ImageProcessing.Scale_16u8u_C3C3(ref srcimg, ref dstimg, imginfo, nColorGradation);

                            //stopwatch1.Stop();
                            //string elapsed = stopwatch1.ElapsedMilliseconds.ToString();
                            //System.Diagnostics.Debug.WriteLine(elapsed);
                        }

                        #endregion
                    }
                    else
                    {
                        #region === multi-channel: selected channel contrasting ===

                        if (imginfo.RedChannel.IsAutoChecked ||
                            imginfo.GreenChannel.IsAutoChecked ||
                            imginfo.BlueChannel.IsAutoChecked ||
                            imginfo.GrayChannel.IsAutoChecked)
                        {
                            nBlackValue = 0;
                            nWhiteValue = 0;
                            if (imginfo.RedChannel.IsAutoChecked)
                            {
                                ImageProcessing.GetAutoScaleValues(srcimg, ref nWhiteValue, ref nBlackValue, ImageChannelType.Red);
                                imginfo.RedChannel.BlackValue = nBlackValue;
                                imginfo.RedChannel.WhiteValue = nWhiteValue;
                                imginfo.RedChannel.GammaValue = dGammaValue = 1.0;
                            }
                            if (imginfo.GreenChannel.IsAutoChecked)
                            {
                                ImageProcessing.GetAutoScaleValues(srcimg, ref nWhiteValue, ref nBlackValue, ImageChannelType.Green);
                                imginfo.GreenChannel.BlackValue = nBlackValue;
                                imginfo.GreenChannel.WhiteValue = nWhiteValue;
                                imginfo.GreenChannel.GammaValue = dGammaValue = 1.0;
                            }
                            if (imginfo.BlueChannel.IsAutoChecked)
                            {
                                ImageProcessing.GetAutoScaleValues(srcimg, ref nWhiteValue, ref nBlackValue, ImageChannelType.Blue);
                                imginfo.BlueChannel.BlackValue = nBlackValue;
                                imginfo.BlueChannel.WhiteValue = nWhiteValue;
                                imginfo.BlueChannel.GammaValue = dGammaValue = 1.0;
                            }
                            if (imginfo.GrayChannel.IsAutoChecked)
                            {
                                ImageProcessing.GetAutoScaleValues(srcimg, ref nWhiteValue, ref nBlackValue, ImageChannelType.Gray);
                                imginfo.GrayChannel.BlackValue = nBlackValue;
                                imginfo.GrayChannel.WhiteValue = nWhiteValue;
                                imginfo.GrayChannel.GammaValue = dGammaValue = 1.0;
                            }
                        }   //Close: Autocontrast

                        if (imginfo.SelectedChannel == ImageChannelType.Red)
                        {
                            #region === Red channel ===

                            nBlackValue = imginfo.RedChannel.BlackValue;
                            nWhiteValue = imginfo.RedChannel.WhiteValue;
                            dGammaValue = imginfo.RedChannel.GammaValue;

                            //System.Diagnostics.Stopwatch stopwatch1 = new System.Diagnostics.Stopwatch();
                            //stopwatch1.Start();

                            nColorGradation = 0;
                            if (imginfo.NumOfChannels == 3)
                                ImageProcessing.Scale_16u8u_C3C3(ref srcimg, ref dstimg, imginfo, nColorGradation);
                            else if (imginfo.NumOfChannels == 4)
                                ImageProcessing.Scale_16u8u_C4C3(ref srcimg, ref dstimg, imginfo, nColorGradation);

                            //stopwatch1.Stop();
                            //string elapsed = "Elapsed time: " + stopwatch1.ElapsedMilliseconds.ToString();
                            //System.Diagnostics.Debug.WriteLine(elapsed);

                            #endregion
                        }
                        if (imginfo.SelectedChannel == ImageChannelType.Green)
                        {
                            #region === Green channel ===

                            nBlackValue = imginfo.GreenChannel.BlackValue;
                            nWhiteValue = imginfo.GreenChannel.WhiteValue;
                            dGammaValue = imginfo.GreenChannel.GammaValue;

                            nColorGradation = 1;
                            if (imginfo.NumOfChannels == 3)
                                ImageProcessing.Scale_16u8u_C3C3(ref srcimg, ref dstimg, imginfo, nColorGradation);
                            else if (imginfo.NumOfChannels == 4)
                                ImageProcessing.Scale_16u8u_C4C3(ref srcimg, ref dstimg, imginfo, nColorGradation);

                            #endregion
                        }
                        if (imginfo.SelectedChannel == ImageChannelType.Blue)
                        {
                            #region === Blue channel ===

                            nBlackValue = imginfo.BlueChannel.BlackValue;
                            nWhiteValue = imginfo.BlueChannel.WhiteValue;
                            dGammaValue = imginfo.BlueChannel.GammaValue;

                            nColorGradation = 2;
                            if (imginfo.NumOfChannels == 3)
                                ImageProcessing.Scale_16u8u_C3C3(ref srcimg, ref dstimg, imginfo, nColorGradation);
                            else if (imginfo.NumOfChannels == 4)
                                ImageProcessing.Scale_16u8u_C4C3(ref srcimg, ref dstimg, imginfo, nColorGradation);

                            #endregion
                        }
                        if (imginfo.SelectedChannel == ImageChannelType.Gray)
                        {
                            #region === Gray channel ===

                            nBlackValue = imginfo.GrayChannel.BlackValue;
                            nWhiteValue = imginfo.GrayChannel.WhiteValue;
                            dGammaValue = imginfo.GrayChannel.GammaValue;

                            nColorGradation = 3;
                            if (imginfo.NumOfChannels == 3)
                                ImageProcessing.Scale_16u8u_C3C3(ref srcimg, ref dstimg, imginfo, nColorGradation);
                            else if (imginfo.NumOfChannels == 4)
                                ImageProcessing.Scale_16u8u_C4C3(ref srcimg, ref dstimg, imginfo, nColorGradation);

                            #endregion
                        }

                        #endregion
                    }

                    #endregion
                }
                else if (srcimg.Format.BitsPerPixel == 8 || srcimg.Format.BitsPerPixel == 16)
                {
                    #region === single channel image contrast ===

                    if (imginfo.MixChannel.IsAutoChecked == true)
                    {
                        ImageProcessing.GetAutoScaleValues(srcimg, ref nWhiteValue, ref nBlackValue);
                        imginfo.MixChannel.BlackValue = nBlackValue;
                        imginfo.MixChannel.WhiteValue = nWhiteValue;
                        imginfo.MixChannel.GammaValue = dGammaValue = 1.0;
                    }
                    else
                    {
                        nBlackValue = imginfo.MixChannel.BlackValue;
                        nWhiteValue = imginfo.MixChannel.WhiteValue;
                        dGammaValue = imginfo.MixChannel.GammaValue;
                    }

                    // Work-around: very large image > 20000 x 20000 sometimes crashes the application
                    //              when loading with the new image contrasting display scheme
                    //
                    if (srcimg != null && srcimg.PixelHeight * srcimg.PixelWidth > (20000 * 20000))
                    {
                        dstimg = ImageProcessing.Scale_16u8u_C1_Indexed(srcimg, nBlackValue, nWhiteValue, dGammaValue,
                                                                        imginfo.MixChannel.IsSaturationChecked,
                                                                        imginfo.SaturationThreshold,
                                                                        imginfo.MixChannel.IsInvertChecked);
                    }
                    else
                    {
                        ImageProcessing.Scale_16u8u_C1_Indexed(ref srcimg, ref dstimg, nBlackValue, nWhiteValue, dGammaValue,
                                                               imginfo.MixChannel.IsSaturationChecked,
                                                               imginfo.SaturationThreshold,
                                                               imginfo.MixChannel.IsInvertChecked);
                    }
                    #endregion
                }
            }
            catch (OutOfMemoryException oome)
            {
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

                throw new Exception(string.Format("{0}\nPlease close some images to clear up some memory.", oome.Message));
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

                // Retrieves the number of bytes currently thought to be allocated (true: force full collection)
                //GC.GetTotalMemory(true);
            }
        }

        /*public static WriteableBitmap UpdateDisplayImage(WriteableBitmap srcimg, ImageInfo imageInfo)
        {
            if (srcimg == null) { return srcimg; }

            WriteableBitmap dspBitmap = null;
            WriteableBitmap[] imageChannels = { null, null, null, null };
            int iBlackValue = 0;
            int iWhiteValue = 0;
            //double dGammaValue = 1.0;
            int iColorGradation;
            int bpp = srcimg.Format.BitsPerPixel;

            if (bpp == 24 || bpp == 48 || bpp == 64)
            {
                WriteableBitmap blueChImage = null;
                WriteableBitmap greenChImage = null;
                WriteableBitmap redChImage = null;
                WriteableBitmap grayChImage = null;

                //GetChannel returns RGB color order
                imageChannels = ImageProcessing.GetChannel(srcimg);

                if (imageChannels != null)
                {
                    #region === Red Channel ===

                    if (imageInfo.RedChannel.IsChannelChecked)
                    {
                        // Channel swapping?
                        if (imageInfo.RedChannel.SelectedColorChannel != imageInfo.RedChannel.ColorChannel)
                        {
                            if (imageInfo.RedChannel.SelectedColorChannel == ImageChannelType.Blue)
                            {
                                redChImage = imageChannels[2];
                            }
                            else if (imageInfo.RedChannel.SelectedColorChannel == ImageChannelType.Green)
                            {
                                redChImage = imageChannels[1];
                            }
                            else if (imageInfo.RedChannel.SelectedColorChannel == ImageChannelType.Gray)
                            {
                                redChImage = imageChannels[3];
                            }
                        }
                        else
                        {
                            redChImage = imageChannels[0];
                        }

                        if (imageInfo.RedChannel.IsAutoChecked == true)
                        {
                            ImageProcessing.GetAutoScaleValues(redChImage, ref iWhiteValue, ref iBlackValue);
                            imageInfo.RedChannel.BlackValue = iBlackValue;
                            imageInfo.RedChannel.WhiteValue = iWhiteValue;
                            imageInfo.RedChannel.GammaValue = 1.0;
                        }

                        if (imageInfo.RedChannel.IsSaturationChecked == true)
                        {
                            iColorGradation = 0;
                            redChImage = ImageProcessing.Scale(redChImage,
                                                               imageInfo.RedChannel.BlackValue,
                                                               imageInfo.RedChannel.WhiteValue,
                                                               imageInfo.RedChannel.GammaValue,
                                                               imageInfo.RedChannel.IsInvertChecked,
                                                               imageInfo.RedChannel.IsSaturationChecked,
                                                               imageInfo.SaturationThreshold,
                                                               iColorGradation);
                        }
                        else
                        {
                            redChImage = ImageProcessing.Scale_C1(redChImage,
                                                                  imageInfo.RedChannel.BlackValue,
                                                                  imageInfo.RedChannel.WhiteValue,
                                                                  imageInfo.RedChannel.GammaValue,
                                                                  imageInfo.RedChannel.IsInvertChecked);
                        }
                    }
                    else
                    {
                        redChImage = null;
                    }

                    #endregion

                    #region === Green Channel ===

                    if (imageInfo.GreenChannel.IsChannelChecked)
                    {
                        if (imageInfo.GreenChannel.SelectedColorChannel != imageInfo.GreenChannel.ColorChannel)
                        {
                            if (imageInfo.GreenChannel.SelectedColorChannel == ImageChannelType.Blue)
                            {
                                greenChImage = imageChannels[2];
                            }
                            else if (imageInfo.GreenChannel.SelectedColorChannel == ImageChannelType.Red)
                            {
                                greenChImage = imageChannels[0];
                            }
                            else if (imageInfo.GreenChannel.SelectedColorChannel == ImageChannelType.Gray)
                            {
                                greenChImage = imageChannels[3];
                            }
                        }
                        else
                        {
                            greenChImage = imageChannels[1];
                        }

                        if (imageInfo.GreenChannel.IsAutoChecked == true)
                        {
                            ImageProcessing.GetAutoScaleValues(greenChImage, ref iWhiteValue, ref iBlackValue);
                            imageInfo.GreenChannel.BlackValue = iBlackValue;
                            imageInfo.GreenChannel.WhiteValue = iWhiteValue;
                            imageInfo.GreenChannel.GammaValue = 1.0;
                        }

                        if (imageInfo.GreenChannel.IsSaturationChecked == true)
                        {
                            iColorGradation = 0;
                            greenChImage = ImageProcessing.Scale(greenChImage,
                                                                 imageInfo.GreenChannel.BlackValue,
                                                                 imageInfo.GreenChannel.WhiteValue,
                                                                 imageInfo.GreenChannel.GammaValue,
                                                                 imageInfo.GreenChannel.IsInvertChecked,
                                                                 imageInfo.GreenChannel.IsSaturationChecked,
                                                                 imageInfo.SaturationThreshold,
                                                                 iColorGradation);
                        }
                        else
                        {
                            greenChImage = ImageProcessing.Scale_C1(greenChImage,
                                                                    imageInfo.GreenChannel.BlackValue,
                                                                    imageInfo.GreenChannel.WhiteValue,
                                                                    imageInfo.GreenChannel.GammaValue,
                                                                    imageInfo.GreenChannel.IsInvertChecked);
                        }
                    }
                    else
                    {
                        greenChImage = null;
                    }

                    #endregion

                    #region === Blue Channel ===

                    if (imageInfo.BlueChannel.IsChannelChecked)
                    {
                        if (imageInfo.BlueChannel.SelectedColorChannel != imageInfo.BlueChannel.ColorChannel)
                        {
                            if (imageInfo.BlueChannel.SelectedColorChannel == ImageChannelType.Red)
                            {
                                blueChImage = imageChannels[0];
                            }
                            else if (imageInfo.BlueChannel.SelectedColorChannel == ImageChannelType.Green)
                            {
                                blueChImage = imageChannels[1];
                            }
                            else if (imageInfo.BlueChannel.SelectedColorChannel == ImageChannelType.Gray)
                            {
                                blueChImage = imageChannels[3];
                            }
                        }
                        else
                        {
                            blueChImage = imageChannels[2];
                        }

                        if (imageInfo.BlueChannel.IsAutoChecked == true)
                        {
                            ImageProcessing.GetAutoScaleValues(blueChImage, ref iWhiteValue, ref iBlackValue);
                            imageInfo.BlueChannel.BlackValue = iBlackValue;
                            imageInfo.BlueChannel.WhiteValue = iWhiteValue;
                            imageInfo.BlueChannel.GammaValue = 1.0;
                        }

                        if (imageInfo.BlueChannel.IsSaturationChecked == true)
                        {
                            iColorGradation = 0;
                            blueChImage = ImageProcessing.Scale(blueChImage,
                                                                imageInfo.BlueChannel.BlackValue,
                                                                imageInfo.BlueChannel.WhiteValue,
                                                                imageInfo.BlueChannel.GammaValue,
                                                                imageInfo.BlueChannel.IsInvertChecked,
                                                                imageInfo.BlueChannel.IsSaturationChecked,
                                                                imageInfo.SaturationThreshold,
                                                                iColorGradation);
                        }
                        else
                        {
                            blueChImage = ImageProcessing.Scale_C1(blueChImage,
                                                                   imageInfo.BlueChannel.BlackValue,
                                                                   imageInfo.BlueChannel.WhiteValue,
                                                                   imageInfo.BlueChannel.GammaValue,
                                                                   imageInfo.BlueChannel.IsInvertChecked);
                        }
                    }
                    else
                    {
                        blueChImage = null;
                    }

                    #endregion

                    #region === Gray Channel ===

                    if (imageInfo.GrayChannel.IsChannelChecked)
                    {
                        if (imageInfo.GrayChannel.SelectedColorChannel != imageInfo.GrayChannel.ColorChannel)
                        {
                            if (imageInfo.GrayChannel.SelectedColorChannel == ImageChannelType.Red)
                            {
                                grayChImage = imageChannels[0];
                            }
                            else if (imageInfo.GrayChannel.SelectedColorChannel == ImageChannelType.Green)
                            {
                                grayChImage = imageChannels[1];
                            }
                            else if (imageInfo.GrayChannel.SelectedColorChannel == ImageChannelType.Blue)
                            {
                                grayChImage = imageChannels[2];
                            }
                        }
                        else
                        {
                            grayChImage = imageChannels[3];
                        }

                        if (imageInfo.GrayChannel.IsAutoChecked == true)
                        {
                            ImageProcessing.GetAutoScaleValues(grayChImage, ref iWhiteValue, ref iBlackValue);
                            imageInfo.GrayChannel.BlackValue = iBlackValue;
                            imageInfo.GrayChannel.WhiteValue = iWhiteValue;
                            imageInfo.GrayChannel.GammaValue = 1.0;
                        }

                        if (imageInfo.GreenChannel.IsSaturationChecked == true)
                        {
                            iColorGradation = 0;
                            grayChImage = ImageProcessing.Scale(grayChImage,
                                                                imageInfo.GrayChannel.BlackValue,
                                                                imageInfo.GrayChannel.WhiteValue,
                                                                imageInfo.GrayChannel.GammaValue,
                                                                imageInfo.GrayChannel.IsInvertChecked,
                                                                imageInfo.GrayChannel.IsSaturationChecked,
                                                                imageInfo.SaturationThreshold,
                                                                iColorGradation);
                        }
                        else
                        {
                            grayChImage = ImageProcessing.Scale_C1(grayChImage,
                                                                   imageInfo.GrayChannel.BlackValue,
                                                                   imageInfo.GrayChannel.WhiteValue,
                                                                   imageInfo.GrayChannel.GammaValue,
                                                                   imageInfo.GrayChannel.IsInvertChecked);
                        }
                    }
                    else
                    {
                        grayChImage = null;
                    }

                    #endregion

                    //IPP ippiCopy_16u_P3C3R expect BGR color order
                    dspBitmap = ImageProcessing.SetChannel(blueChImage, greenChImage, redChImage);
                }

            }
            else
            {
                #region === single channel image contrast ===

                dspBitmap = srcimg;

                WriteableBitmap redChImage = null;
                WriteableBitmap greenChImage = null;
                WriteableBitmap blueChImage = null;
                WriteableBitmap grayChImage = null;

                if (imageInfo.CaptureType != null &&
                    imageInfo.CaptureType.Contains("Chemi") &&
                    imageInfo.RedChannel.LightSource == 10)
                {
                    // Light source: 10 (None)
                    ImageProcessing.GetChemiAutoScaleValues(dspBitmap, ref iBlackValue, ref iWhiteValue);
                    imageInfo.RedChannel.BlackValue = iBlackValue;
                    imageInfo.RedChannel.WhiteValue = iWhiteValue;
                    imageInfo.RedChannel.GammaValue = 1.00;
                }
                else if (imageInfo.RedChannel.LightSource == 11)
                {
                    // Light source: 11 (Visible)
                    ImageProcessing.GetVisibleAutoScaleValues(dspBitmap, ref iBlackValue, ref iWhiteValue);
                    imageInfo.RedChannel.BlackValue = iBlackValue;
                    imageInfo.RedChannel.WhiteValue = iWhiteValue;
                    imageInfo.RedChannel.GammaValue = 1.00;
                }
                else
                {
                    ImageProcessing.GetAutoScaleValues(dspBitmap, ref iWhiteValue, ref iBlackValue);
                    imageInfo.RedChannel.BlackValue = iBlackValue;
                    imageInfo.RedChannel.WhiteValue = iWhiteValue;
                    imageInfo.RedChannel.GammaValue = 1.00;
                }

                dspBitmap = ImageProcessing.Scale_C1(dspBitmap,
                                                     imageInfo.RedChannel.BlackValue,
                                                     imageInfo.RedChannel.WhiteValue,
                                                     imageInfo.RedChannel.GammaValue,
                                                     imageInfo.RedChannel.IsInvertChecked);

                // Channel swapping?
                if (imageInfo.RedChannel.SelectedColorChannel != imageInfo.RedChannel.ColorChannel)
                {
                    if (imageInfo.RedChannel.SelectedColorChannel == ImageChannelType.Blue)
                    {
                        blueChImage = dspBitmap;
                    }
                    else if (imageInfo.RedChannel.SelectedColorChannel == ImageChannelType.Green)
                    {
                        greenChImage = dspBitmap;
                    }
                    else if (imageInfo.RedChannel.SelectedColorChannel == ImageChannelType.Gray)
                    {
                        grayChImage = dspBitmap;
                    }
                }
                else
                {
                    redChImage = dspBitmap;
                }

                //IPP ippiCopy_16u_P3C3R expect BGR color order
                dspBitmap = ImageProcessing.SetChannel(blueChImage, greenChImage, redChImage);

                #endregion
            }

            return dspBitmap;
        }*/


        /// <summary>
        /// Image contrasting: Expected color order: R/G/B/Gray
        /// </summary>
        /// <param name="srcImages"></param>
        /// <param name="imageInfo"></param>
        /// <returns></returns>
        /*public static WriteableBitmap UpdateDisplayImage(WriteableBitmap[] srcImages, ImageInfo imageInfo)
        {
            if (srcImages == null || srcImages.Length == 0)
            {
                return null;
            }

            if (imageInfo == null)
            {
                imageInfo = new ImageInfo();
            }

            WriteableBitmap dspBitmap = null;
            int nBlackValue = imageInfo.MixChannel.BlackValue;
            int nWhiteValue = imageInfo.MixChannel.WhiteValue;
            double dGammaValue = imageInfo.MixChannel.GammaValue;
            bool bIsSaturation = imageInfo.MixChannel.IsSaturationChecked;
            bool bIsInverted = imageInfo.MixChannel.IsInvertChecked;
            int nSaturatonThreshold = imageInfo.SaturationThreshold;
            int nColorGradation = imageInfo.NumOfChannels;
            int channelCount = imageInfo.NumOfChannels;

            try
            {
                if (channelCount > 1)
                {
                    dspBitmap = ImageProcessing.Scale_16u8uC1_C3(srcImages,
                                                                 nBlackValue, nWhiteValue, dGammaValue,
                                                                 bIsSaturation, nSaturatonThreshold, bIsInverted);
                }
            }
            catch (Exception)
            {
                throw;
            }
            return dspBitmap;
        }*/


        /// <summary>
        /// Image contrasting: Expected color order: R/G/B/Gray
        /// </summary>
        /// <param name="srcImages"></param>
        /// <param name="imageInfo"></param>
        /// <returns></returns>
        /*public static void UpdateDisplayImage(WriteableBitmap[] srcImages, ImageInfo imageInfo, ref WriteableBitmap dstBitmap)
        {
            if (srcImages == null || srcImages.Length == 0)
            {
                return;
            }

            if (imageInfo == null)
            {
                imageInfo = new ImageInfo();
            }

            int nBlackValue = imageInfo.MixChannel.BlackValue;
            int nWhiteValue = imageInfo.MixChannel.WhiteValue;
            double dGammaValue = imageInfo.MixChannel.GammaValue;
            bool bIsSaturation = imageInfo.MixChannel.IsSaturationChecked;
            bool bIsInverted = imageInfo.MixChannel.IsInvertChecked;
            int nSaturatonThreshold = imageInfo.SaturationThreshold;
            int nColorGradation = imageInfo.NumOfChannels;
            int channelCount = imageInfo.NumOfChannels;

            try
            {
                if (channelCount > 1)
                {
                    ImageProcessing.Scale_16u8uC1_C3(srcImages,
                                                     nBlackValue, nWhiteValue, dGammaValue,
                                                     bIsSaturation, nSaturatonThreshold, bIsInverted, ref dstBitmap);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }*/

        /*public static void UpdateDisplayImage(WriteableBitmap srcimage, ImageInfo imageinfo, ref WriteableBitmap dstimage)
        {
            if (srcimage == null) { return; }

            int nBlackValue = imageinfo.MixChannel.BlackValue;
            int nWhiteValue = imageinfo.MixChannel.WhiteValue;
            double dGammaValue = imageinfo.MixChannel.GammaValue;
            bool bIsSaturation = imageinfo.MixChannel.IsSaturationChecked;
            bool bIsInverted = imageinfo.MixChannel.IsInvertChecked;
            int nSatThreshold = imageinfo.SaturationThreshold;
            //int nColorGradation = imageinfo.NumOfChannels;
            //int channelCount = imageinfo.NumOfChannels;

            if (srcimage.Format == PixelFormats.Gray16 || srcimage.Format == PixelFormats.Gray8)
            {
                // Use an indexed bitmap format.
                ImageProcessing.Scale_16u8u_C1_Indexed(ref srcimage, nBlackValue, nWhiteValue, dGammaValue, bIsSaturation, nSatThreshold, bIsInverted, ref dstimage);
            }
        }*/

        public unsafe static void UpdateDisplayImage(byte*[] srcImages, int nWidth, int nHeight, int nStride,
                                                     PixelFormat pixelFormat, ImageInfo imageInfo,
                                                     byte* pDstImageData, int nDstStride)
        {
            if (srcImages == null || srcImages.Length == 0)
            {
                return;
            }

            if (imageInfo == null)
            {
                imageInfo = new ImageInfo();
            }

            int nBlackValue = imageInfo.MixChannel.BlackValue;
            int nWhiteValue = imageInfo.MixChannel.WhiteValue;
            double dGammaValue = imageInfo.MixChannel.GammaValue;
            bool bIsSaturation = imageInfo.MixChannel.IsSaturationChecked;
            bool bIsInverted = imageInfo.MixChannel.IsInvertChecked;
            int nSaturatonThreshold = imageInfo.SaturationThreshold;
            int channelCount = imageInfo.NumOfChannels;

            try
            {
                if (channelCount > 1)
                {
                    ImageProcessing.Scale_16u8uC1_C4(srcImages, nWidth, nHeight, nStride, pixelFormat,
                                                     pDstImageData, nDstStride,
                                                     nBlackValue, nWhiteValue, dGammaValue,
                                                     bIsSaturation, nSaturatonThreshold, bIsInverted);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public unsafe static void UpdateDisplayImage(byte* pSrcimage, int nWidth, int nHeight, int nStride,
                                                     PixelFormat srcPixelFormat, ImageInfo imageinfo,
                                                     byte* pDstImageData, int nDstStride)
        {
            if (pSrcimage == null) { return; }

            int nBlackValue = imageinfo.MixChannel.BlackValue;
            int nWhiteValue = imageinfo.MixChannel.WhiteValue;
            double dGammaValue = imageinfo.MixChannel.GammaValue;
            bool bIsSaturation = imageinfo.MixChannel.IsSaturationChecked;
            bool bIsInverted = imageinfo.MixChannel.IsInvertChecked;
            int nSatThreshold = imageinfo.SaturationThreshold;

            if (srcPixelFormat == PixelFormats.Gray16 || srcPixelFormat == PixelFormats.Gray8)
            {
                // Use an indexed bitmap format.
                ImageProcessing.Scale_16u8u_C1_Indexed(pSrcimage, nWidth, nHeight, nStride, srcPixelFormat,
                                                       pDstImageData, nDstStride,
                                                       nBlackValue, nWhiteValue, dGammaValue, bIsSaturation,
                                                       nSatThreshold, bIsInverted);
            }
        }


        /***public static WriteableBitmap CreateDisplayImage(WriteableBitmap srcimg, ImageInfo imageInfo, bool bIsCropped, bool bIsGetMinMax = true)
        {
            if (srcimg == null) { return srcimg; }

            WriteableBitmap dspBitmap = srcimg;
            WriteableBitmap[] arrImageChannels = { null, null, null };

            if (bIsCropped)
            {
                if (srcimg.Format.BitsPerPixel == 16 ||
                    srcimg.Format.BitsPerPixel == 48)
                {
                    int iColorGradation = 3;
                    dspBitmap = MVImage.Scale(
                        imageInfo.WhiteValue_Mix,
                        imageInfo.BlackValue_Mix,
                        imageInfo.GammaValue_Mix,
                        dspBitmap,
                        srcimg,
                        false,
                        imageInfo.SaturationThreshold,
                        iColorGradation,
                        imageInfo.IsInvertChecked_Mix);
                }
            }
            else
            {
                int iWhiteValue = imageInfo.WhiteValue_Mix;
                int iBlackValue = imageInfo.BlackValue_Mix;
                double dGammaValue = 1.0;
                int iColorGradation = 3;

                if (srcimg.Format.BitsPerPixel == 16 ||
                    srcimg.Format.BitsPerPixel == 48)
                {
                    // Chemi cumulative: use the same initial black and white value.
                    if (bIsGetMinMax)
                    {
                        iBlackValue = MVImage.Min(dspBitmap);
                        iWhiteValue = MVImage.Max(dspBitmap);
                    }

                    imageInfo.BlackValue_Mix = iBlackValue;
                    imageInfo.WhiteValue_Mix = iWhiteValue;
                    imageInfo.GammaValue_Mix = dGammaValue;

                    if (srcimg.Format.BitsPerPixel == 48)
                    {
                        arrImageChannels = MVImage.RGBExtractSingleChannel(srcimg);
                        imageInfo.BlackValue_BChannel = MVImage.Min(arrImageChannels[0]);
                        imageInfo.WhiteValue_BChannel = MVImage.Max(arrImageChannels[0]);
                        imageInfo.GammaValue_BChannel = 1.0;
                        imageInfo.BlackValue_GChannel = MVImage.Min(arrImageChannels[1]);
                        imageInfo.WhiteValue_GChannel = MVImage.Max(arrImageChannels[1]);
                        imageInfo.GammaValue_GChannel = 1.0;
                        imageInfo.BlackValue_RChannel = MVImage.Min(arrImageChannels[2]);
                        imageInfo.WhiteValue_RChannel = MVImage.Max(arrImageChannels[2]);
                        imageInfo.GammaValue_RChannel = 1.0;
                    }

                    dspBitmap = MVImage.Scale(
                        imageInfo.WhiteValue_Mix,
                        imageInfo.BlackValue_Mix,
                        dGammaValue,
                        dspBitmap,
                        srcimg,
                        false,
                        imageInfo.SaturationThreshold,
                        iColorGradation,
                        imageInfo.IsInvertChecked_Mix);
                }
            }

            arrImageChannels = null;

            GC.Collect();
            // Retrieves the number of bytes currently thought to be allocated (true: force full collection)
            //GC.GetTotalMemory(true);

            return dspBitmap;
        }***/

        public static unsafe WriteableBitmap PasteMarkerImage(WriteableBitmap chemiImage, WriteableBitmap blobsImage, WriteableBitmap clippedImage, Rect clippedRect)
        {
            WriteableBitmap destImage = (WriteableBitmap)chemiImage.Clone();
            WriteableBitmap cpyBlobsImage = null;

            if (blobsImage == null)
            {
                string caption = "Find blobs error...";
                string message = "Error finding the blobs on the selected region.\n" +
                                 "Please select another region on the marker image, and try again.";
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Stop);
                return null;
            }
            else
            {
                cpyBlobsImage = (WriteableBitmap)blobsImage.Clone();
            }

            // Findbobs returns an image that's 0's and 255's
            // Make pixels 0's and 1's
            ImageArithmetic imageArith = new ImageArithmetic();
            cpyBlobsImage = imageArith.Divide(cpyBlobsImage, 255);

            double dDpiX = chemiImage.DpiX;
            double dDpiY = chemiImage.DpiY;

            // create empty image
            WriteableBitmap roiImage = new WriteableBitmap(chemiImage.PixelWidth, chemiImage.PixelHeight, dDpiX, dDpiY, PixelFormats.Gray16, null);

            // Add the ROI region to the empty image
            roiImage = PasteImage(roiImage, clippedImage, clippedRect);

            // Clear the background around the blot
            roiImage = imageArith.MultiplyImage(ref roiImage, ref cpyBlobsImage);

            // Make new blobs image
            cpyBlobsImage = MakeNonZeroPixelValueOnes(roiImage);

            // Invert the ROI
            roiImage = ImageProcessing.Invert(roiImage);

            // Reserve the back buffer for updates.
            //destImage.Lock();
            //cpyClippedImage.Lock();

            // scale the image
            const int nBins = 3000;
            const int nLevels = nBins + 1;
            //int[] pHist = new int[nLevels];
            //int[] pLevels = new int[nLevels];
            int nLowerLevel = 0;
            int nUpperLevel = 65536;

            // Get the histogram of the ROI image
            int[,] pHist = ImageProcessing.HistogramEven(roiImage, nLevels, nLowerLevel, nUpperLevel);

            // Get histogram highest peak
            int histoMarkerMax = 0;
            int histoMarkerMaxIndex = 0;
            for (int i = 0; i < pHist.Length; i++)
            {
                if (pHist[0, i] > histoMarkerMax)
                {
                    // Length of the arrays pLevels and pHist is defined by the nLevels parameter.
                    // Since nLevels is the number of levels, the number of histogram bins is nLevels - 1.
                    // Similarly, the number of values in the array pHist is also nLevels - 1.
                    // The array is zero index so the last 'real' value is nBins - 1.
                    if (i != (nBins - 1))
                    {
                        histoMarkerMax = pHist[0, i];
                        histoMarkerMaxIndex = i;
                    }
                }
            }

            // Scale the histogram peak to 65535
            histoMarkerMaxIndex = (int)(((double)histoMarkerMaxIndex + 0.5) * ((double)65535.0 / (double)nBins));

            // Subtract histogram peak
            roiImage = imageArith.Subtract(roiImage, histoMarkerMaxIndex);

            // Clear the background around the blot
            roiImage = imageArith.MultiplyImage(ref roiImage, ref cpyBlobsImage);

            // Add ROI to the chemi image
            destImage = imageArith.AddImage(ref destImage, ref roiImage);

            // Releases the back buffer to make it available for display.
            //destImage.Unlock();
            //cpyClippedImage.Unlock();

            // Forces an immediate garbage collection.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            // Force garbage collection.
            //GC.Collect();
            // Wait for all finalizers to complete before continuing.
            //GC.WaitForPendingFinalizers();

            return destImage;
        }

        public static unsafe WriteableBitmap PasteRgbMarkerImage(WriteableBitmap chemiImage, WriteableBitmap blobsImage, WriteableBitmap clippedImage, Rect clippedRect)
        {
            WriteableBitmap[] destImages = new WriteableBitmap[3];
            WriteableBitmap[] markerImages = null;

            // GetChannel return RGB color order
            markerImages = ImageProcessing.GetChannel(clippedImage);

            for (int index = 0; index < 3; index++)
            {
                WriteableBitmap cpyChemiImage = (WriteableBitmap)chemiImage.Clone();
                WriteableBitmap cpyBlobsImage = (WriteableBitmap)blobsImage.Clone();
                destImages[index] = PasteMarkerImage(cpyChemiImage, cpyBlobsImage, markerImages[index], clippedRect);
            }

            // SetChannel expects RGB color order
            WriteableBitmap destImage = ImageProcessing.SetChannel(destImages[0], destImages[1], destImages[2]);

            // Forces an immediate garbage collection.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            // Force garbage collection.
            //GC.Collect();
            // Wait for all finalizers to complete before continuing.
            //GC.WaitForPendingFinalizers();

            return destImage;
        }

        internal static unsafe WriteableBitmap PasteImage(WriteableBitmap srcImage, WriteableBitmap clippedImage, Rect clippedRect)
        {
            if (srcImage == null || clippedImage == null || clippedRect.Width == 0 || clippedRect.Height == 0)
            {
                return null;
            }

            WriteableBitmap destImage = (WriteableBitmap)srcImage.Clone();

            int x = (int)clippedRect.X;
            int y = (int)clippedRect.Y;
            int width = clippedImage.PixelWidth;
            int height = clippedImage.PixelHeight;
            int destBufferWidth = destImage.BackBufferStride;
            int clipBufferWidth = clippedImage.BackBufferStride;

            ushort* pDest16 = null;
            ushort* pClipped16 = null;

            for (int i = 0; i < height; i++)
            {
                pDest16 = (ushort*)((byte*)(void*)destImage.BackBuffer.ToPointer() + ((y + i) * destBufferWidth));
                pClipped16 = (ushort*)((byte*)(void*)clippedImage.BackBuffer.ToPointer() + (i * clipBufferWidth));
                for (int j = 0; j < width; j++)
                {
                    *(pDest16 + x + j) += *(pClipped16++);
                }
            }

            // Forces an immediate garbage collection.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            // Force garbage collection.
            //GC.Collect();
            // Wait for all finalizers to complete before continuing.
            //GC.WaitForPendingFinalizers();

            return destImage;
        }

        private static unsafe WriteableBitmap MakeNonZeroPixelValueOnes(WriteableBitmap srcImage)
        {
            int width = srcImage.PixelWidth;
            int height = srcImage.PixelHeight;

            WriteableBitmap destImage = (WriteableBitmap)srcImage.Clone();
            int bufferWidth = destImage.BackBufferStride;
            byte* pDestBuffer = (byte*)destImage.BackBuffer.ToPointer();

            for (int i = 0; i < height; i++)
            {
                ushort* pDest = (ushort*)(pDestBuffer + (i * bufferWidth));

                for (int y = 0; y < width; y++)
                {
                    if (*pDest > 0)
                    {
                        *pDest = 1;
                    }
                    pDest++;
                }
            }

            // Forces an immediate garbage collection.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            // Force garbage collection.
            //GC.Collect();
            // Wait for all finalizers to complete before continuing.
            //GC.WaitForPendingFinalizers();

            return destImage;
        }

        public static unsafe WriteableBitmap Paste24bppImage(BitmapSource srcImage, WriteableBitmap clippedImage, Rect clippedRect)
        {
            if ((srcImage.Format != PixelFormats.Bgr24 && clippedImage.Format != PixelFormats.Bgr24) &&
                (srcImage.Format != PixelFormats.Rgb24 && clippedImage.Format != PixelFormats.Rgb24))
            {
                throw new Exception("Image type not supported");
            }

            WriteableBitmap destImage = (WriteableBitmap)srcImage.Clone();
            WriteableBitmap cpyClippedImage = clippedImage.Clone();

            // Reserve the back buffer for updates.
            destImage.Lock();
            cpyClippedImage.Lock();

            byte* pDest = null;
            byte* pClipped = null;

            int x = (int)clippedRect.X;
            int y = (int)clippedRect.Y;
            int width = cpyClippedImage.PixelWidth;
            int height = cpyClippedImage.PixelHeight;
            int destBufferWidth = destImage.BackBufferStride;
            int clipBufferWidth = cpyClippedImage.BackBufferStride;

            for (int i = 0; i < height; i++)
            {
                pDest = (byte*)(void*)destImage.BackBuffer.ToPointer() + (3 * x) + ((y + i) * destBufferWidth);
                pClipped = (byte*)(void*)cpyClippedImage.BackBuffer.ToPointer() + (i * clipBufferWidth);
                for (int j = 0; j < width; j++)
                {
                    (*pDest++) = (*pClipped++);
                    (*pDest++) = (*pClipped++);
                    (*pDest++) = (*pClipped++);
                }
            }

            // Releases the back buffer to make it available for display.
            destImage.Unlock();
            cpyClippedImage.Unlock();

            // Forces an immediate garbage collection.
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            //GC.WaitForPendingFinalizers();
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            // Force garbage collection.
            //GC.Collect();
            // Wait for all finalizers to complete before continuing.
            //GC.WaitForPendingFinalizers();

            return destImage;
        }


        /*public static unsafe WriteableBitmap InvertPixels(WriteableBitmap srcImage)
        {
            if (srcImage.Format.BitsPerPixel != 16) { return srcImage; }

            // Reserve the back buffer for updates.
            srcImage.Lock();

            // source image max pixel intensity
            double pixelMax = MVImage.Max(srcImage);

            // Get a pointer to the back buffer.
            ushort* pSrcData = (ushort*)srcImage.BackBuffer.ToPointer();

            int width = srcImage.PixelWidth;
            int height = srcImage.PixelHeight;

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    *(pSrcData++) = (ushort)(65535.0 - (((double)(*(pSrcData)) / pixelMax) * 65535.0));
                }
            }

            // Releases the back buffer to make it available for display.
            srcImage.Unlock();

            return srcImage;
        }*/

    }
}

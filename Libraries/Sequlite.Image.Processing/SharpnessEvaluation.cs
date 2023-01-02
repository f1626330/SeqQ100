using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Sequlite.Image.Processing
{
    public static class SharpnessEvaluation
    {
        private static ISeqLog Logger = SeqLogFactory.GetSeqFileLog("sharpnessevaluation");

        /// <summary>
        /// Gradient of image
        /// </summary>
        /// <param name="img"></param>
        /// <param name="roi"> rectangle of interested region, each dimension is scaled to 1.</param>
        /// <returns></returns>
        public static unsafe double Gradient(ref WriteableBitmap img, Rect roi)
        {
            double gradient = 0;
            if (roi.Left + roi.Width > 1)
            {
                roi.Width = 1 - roi.Left;
            }
            if (roi.Top + roi.Height > 1)
            {
                roi.Height = 1 - roi.Top;
            }
            uint roiLeft = (uint)(roi.Left * img.PixelWidth);
            uint roiTop = (uint)(roi.Top * img.PixelHeight);
            uint roiWidth = (uint)(roi.Width * img.PixelWidth);
            uint roiHeight = (uint)(roi.Height * img.PixelHeight);
            double maxg = 0;
            double ming = 0;
            for (uint j = roiTop; j < roiTop + roiHeight - 1; j++)
            {
                ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                for (uint i = roiLeft; i < roiLeft + roiWidth - 1; i++)
                {
                    ushort Ixy = ptr[i];
                    ushort Ix_1y = ptr[i + 1];
                    ushort Ixy_1 = ptr[i + img.BackBufferStride / 2];
                    int Gx = Ix_1y - Ixy;
                    int Gy = Ixy_1 - Ixy;
                    //if (Ixy_1 < Ixy) { MessageBox.Show(String.Format("Gy:{0}, {1}, {2}", Gy, Ixy_1, Ixy)); }


                    //gradient += Math.Abs(Gx) + Math.Abs(Gy);
                    double grad = Math.Sqrt(Math.Pow(Gx, 2) + Math.Pow(Gy, 2));
                    if (grad > maxg) { maxg = grad; }
                    if (grad < ming) { ming = grad; }
                    //if (grad > 200)
                    //{
                    //    gradient += grad;
                    //}
                    gradient += grad;

                }
            }
            double avg = gradient / (roiHeight - 1) / (roiWidth - 1);
            //MessageBox.Show(string.Format("avg:{0}, min{1}, max{2}", avg.ToString(), ming.ToString(), maxg.ToString()));
            return gradient;
        }
        /// <summary>
        /// Sharpness calculation that fit certain fiducial pattern: vertical short discontinued strip 
        /// calculate the averaged intensity each column of image matrix first to reduce noise
        /// </summary>
        /// <param name="img"></param>
        /// <param name="roi"></param> rectangle of interested region, each dimension is scaled to 1.
        /// <returns> standard deviation of those averaged intensities </returns>
        public static unsafe double VerticalAveragedStdDev(ref WriteableBitmap img, Rect roi)
        {
            double stdDev = 0;

            if (roi.Left + roi.Width > 1)
            {
                roi.Width = 1 - roi.Left;
            }
            if (roi.Top + roi.Height > 1)
            {
                roi.Height = 1 - roi.Top;
            }
            uint roiLeft = (uint)(roi.Left * img.PixelWidth);
            uint roiTop = (uint)(roi.Top * img.PixelHeight);
            uint roiWidth = (uint)(roi.Width * img.PixelWidth);
            uint roiHeight = (uint)(roi.Height * img.PixelHeight);

            int[] horizontalLine = new int[roiWidth];
            for (int i = 0; i < roiWidth; i++)
            {
                horizontalLine[i] = 0;
            }

            for (uint i = roiLeft; i < roiLeft + roiWidth; i++)
            {
                for (uint j = roiTop; j < roiTop + roiHeight; j++)
                {
                    ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                    horizontalLine[i - roiLeft] += ptr[i];
                }
            }

            for (int i = 0; i < roiWidth; i++)
            {
                horizontalLine[i] = (int)(horizontalLine[i] / roiHeight);
            }

            double avg = horizontalLine.Average();
            double sum = horizontalLine.Sum(i => Math.Pow(i - avg, 2));
            stdDev = Math.Sqrt(sum / horizontalLine.Count());

            return stdDev;
        }

        /// <summary>
        /// Sharpness calculation that fit certain fiducial pattern: horizontal short discontinued strip 
        /// calculate the averaged intensity each row of image matrix first to reduce noise
        /// </summary>
        /// <param name="img"></param>
        /// <param name="roi"></param> rectangle of interested region, each dimension is scaled to 1.
        /// <returns> standard deviation of those averaged intensities </returns>
        public static unsafe double HorizontalAveragedStdDev(ref WriteableBitmap img, Rect roi)
        {
            double stdDev = 0;

            if (roi.Left + roi.Width > 1)
            {
                roi.Width = 1 - roi.Left;
            }
            if (roi.Top + roi.Height > 1)
            {
                roi.Height = 1 - roi.Top;
            }
            uint roiLeft = (uint)(roi.Left * img.PixelWidth);
            uint roiTop = (uint)(roi.Top * img.PixelHeight);
            uint roiWidth = (uint)(roi.Width * img.PixelWidth);
            uint roiHeight = (uint)(roi.Height * img.PixelHeight);

            int[] verticalLine = new int[roiHeight];
            for (int i = 0; i < roiHeight; i++)
            {
                verticalLine[i] = 0;
            }

            for (uint j = roiTop; j < roiTop + roiHeight; j++)
            {
                ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                for (uint i = roiLeft; i < roiLeft + roiWidth; i++)
                {
                    //ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                    verticalLine[j - roiTop] += ptr[i];
                }
            }

            for (int i = 0; i < roiHeight; i++)
            {
                verticalLine[i] = (int)(verticalLine[i] / roiWidth);
            }

            double avg = verticalLine.Average();
            double sum = verticalLine.Sum(i => Math.Pow(i - avg, 2));
            stdDev = Math.Sqrt(sum / verticalLine.Count());

            return stdDev;
        }

        /// <summary>
        /// Sharpness calculation that fit titled fiducial pattern: Horizontal short continued light strip, expected cross Y direction 
        /// for each sliding window with input height and steps
        /// calculate the averaged intensity each row of image matrix first to reduce noise
        /// then calcuate the standard deviation averaged intensity
        /// </summary>
        /// <param name="img"></param>
        /// <param name="height"></param> Height of ROI
        /// <param name="step"></param> Steps betweens each window, may or maynot have overlapping
        /// <returns> a list of STD for different windows cross whole feature </returns>
        public static unsafe List<double> MovingWindHStdDev(ref WriteableBitmap img, double height, double step)
        {
            List<double> stdcurve = new List<double>();
            for (double windowtop = 17*step; windowtop < 1 - height; windowtop += step)
            {
                double stdDev = 0;

                
                uint roiLeft = (uint)(0 * img.PixelWidth);
                uint roiTop = (uint)(windowtop * img.PixelHeight);
                uint roiWidth = (uint)(1 * img.PixelWidth);
                uint roiHeight = (uint)(height * img.PixelHeight);

                int[] verticalLine = new int[roiHeight];
                for (int i = 0; i < roiHeight; i++)
                {
                    verticalLine[i] = 0;
                }

                for (uint j = roiTop; j < roiTop + roiHeight; j++)
                {
                    ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                    for (uint i = roiLeft; i < roiLeft + roiWidth; i++)
                    {
                        //ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                        verticalLine[j - roiTop] += ptr[i];
                    }
                }

                for (int i = 0; i < roiHeight; i++)
                {
                    verticalLine[i] = (int)(verticalLine[i] / roiWidth);
                }

                double avg = verticalLine.Average();
                double sum = verticalLine.Sum(i => Math.Pow(i - avg, 2));
                stdDev = Math.Sqrt(sum / verticalLine.Count());
                //double avgint = avg / roiHeight;
                stdcurve.Add(stdDev);
            }
            return stdcurve;
        }
        /// <summary>
        /// Sharpness calculation that fit titled fiducial pattern: Horizontal short continued light strip, expected cross Y direction 
        /// for each sliding window with input height and steps
        /// calculate the sum of intensity each row of image matrix
        /// then calcuate the standard deviation of sum of intensity
        /// normalize the number by average pixel intensity of this window
        /// </summary>
        /// <param name="img"></param>
        /// <param name="height"></param> Height of ROI
        /// <param name="step"></param> Steps betweens each window, may or maynot have overlapping
        /// <returns> a list of normalized STD for different windows cross whole feature </returns>
        public static unsafe List<double> MovingWindHNStdDev(ref WriteableBitmap img, double height, double step)
        {
            List<double> stdcurve = new List<double>();
            for (double windowtop = 0; windowtop < 1 - height; windowtop += step)
            {
                double stdDev = 0;


                uint roiLeft = (uint)(0 * img.PixelWidth);
                uint roiTop = (uint)(windowtop * img.PixelHeight);
                uint roiWidth = (uint)(1 * img.PixelWidth);
                uint roiHeight = (uint)(height * img.PixelHeight);

                int[] verticalLine = new int[roiHeight];
                for (int i = 0; i < roiHeight; i++)
                {
                    verticalLine[i] = 0;
                }

                for (uint j = roiTop; j < roiTop + roiHeight; j++)
                {
                    ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                    for (uint i = roiLeft; i < roiLeft + roiWidth; i++)
                    {
                        //ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                        verticalLine[j - roiTop] += ptr[i];
                    }
                }

                //for (int i = 0; i < roiHeight; i++)
                //{
                //    verticalLine[i] = (int)(verticalLine[i] / roiWidth);
                //}

                double avg = verticalLine.Average();
                double sum = verticalLine.Sum(i => Math.Pow(i - avg, 2));
                stdDev = Math.Sqrt(sum / verticalLine.Count());
                double avgint = avg / roiWidth;
                stdcurve.Add(stdDev/avgint);
            }
            return stdcurve;
        }
        /// <summary>
        /// Sharpness calculation that fit titled fiducial pattern: Horizontal short continued light strip, expected cross Y direction 
        /// for each sliding window with height 20, step 1,
        /// calculate the average of intensity each row of image matrix
        /// then calcuate the mtf with equation: (max - min) / (max + min)
        /// max/min: maximum/minmum of averaged intensity within this sliding window
        /// average the numbers with windows size 100; smooth the curve
        /// </summary>
        /// <param name="img"></param>
        /// <returns> smoothed MTF curve for different windows cross whole feature </returns>
        public static unsafe List<double> MTF (ref WriteableBitmap img)
        {
            uint roiLeft = (uint)(0 * img.PixelWidth);
            uint roiTop = (uint)(0 * img.PixelHeight);
            uint roiWidth = (uint)(1 * img.PixelWidth);
            uint roiHeight = (uint)(1 * img.PixelHeight);
            double[] premtf = new double[roiHeight - 19];
            List<double> mtf = new List<double>();
            double[] verticalLine = new double[roiHeight];
            for (int i = 0; i < roiHeight; i++)
            {
                verticalLine[i] = 0;
            }

            for (uint j = roiTop; j < roiTop + roiHeight; j++)
            {
                ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                for (uint i = roiLeft; i < roiLeft + roiWidth; i++)
                {
                    //ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                    verticalLine[j - roiTop] += ptr[i];
                }
            }

            for (int i = 0; i < roiHeight; i++)
            {
                verticalLine[i] = (int)(verticalLine[i] / roiWidth);
            }
            for (int i = 0; i < roiHeight-19; i++)
            {
                var subarrary = verticalLine.Skip(i).Take(19).ToArray();
                double maxVal = subarrary.Max();
                double minVal = subarrary.Min();
                premtf[i] = ((maxVal - minVal) / (maxVal + minVal));
            }
            for(int i = 0; i < premtf.Length - 100; i++)
            {
                var subarrray = premtf.Skip(i).Take(100).ToArray();
                double averg = subarrray.Average();
                mtf.Add(averg);
            }
            return mtf;
        }

        /// <summary>
        /// Used Laplace Filter to calculated overall sharpness of whole image with ROI
        /// </summary>
        /// <param name="img"></param>
        /// <param name="roi"></param>
        /// <returns> Sharpness score </returns>
        public static unsafe double LaplaceFilter(ref WriteableBitmap img, Rect roi)
        {
            int score = 0;

            if (roi.Left + roi.Width > 1)
            {
                roi.Width = 1 - roi.Left;
            }
            if (roi.Top + roi.Height > 1)
            {
                roi.Height = 1 - roi.Top;
            }
            uint roiLeft = (uint)(roi.Left * img.PixelWidth);
            uint roiTop = (uint)(roi.Top * img.PixelHeight);
            uint roiWidth = (uint)(roi.Width * img.PixelWidth);
            uint roiHeight = (uint)(roi.Height * img.PixelHeight);
            double minl = 0;
            double maxl = 0;
            for (uint j = roiTop + 1; j < roiTop + roiHeight - 1; j++)
            {
                ushort* ptr = ((ushort*)img.BackBuffer.ToPointer()) + j * img.BackBufferStride / 2;
                for (uint i = roiLeft + 1; i < roiLeft + roiWidth - 1; i++)
                {
                    ushort Ixy = ptr[i];
                    ushort Ixy1y = ptr[i + 1];
                    ushort Ixy_1y = ptr[i - 1];
                    ushort Ixy1x = ptr[i + img.BackBufferStride / 2];
                    ushort Ixy_1x = ptr[i - img.BackBufferStride / 2];
                    int Lfilter = Math.Abs(Ixy1x + Ixy1y + Ixy_1x + Ixy_1y - 4 * Ixy);
                    //if (Lfilter < 0) { Lfilter = 0; }
                    if (Lfilter > maxl) { maxl = Lfilter; }
                    if (Lfilter < minl) { minl = Lfilter; }
                    score += Lfilter;

                }
            }
            double avg = score / (roiHeight - 2) / (roiWidth - 2);
            //MessageBox.Show(String.Format("Avg:{0}, min{1}, max{2}", avg.ToString(), minl.ToString(), maxl.ToString()));
            return score;
        }

        /// <summary>
        /// For tilt fiducial method 1
        /// With given threshold of sharpness
        /// </summary>
        /// <param name="metricpattern"></param> Sharpness curve of titled fiducial at certain Z pos
        /// <param name="Threshold"></param>
        /// <returns> output with the input curve intersected. </returns>
        public static double CrossIndex(List<double> metricpattern, double Threshold)
        {
            double previousscore = 0;
            int metricindex = 0;
            List<double> crossindex = new List<double>();
            foreach (double metric in metricpattern)
            {
                if (metric > Threshold && previousscore < Threshold && previousscore !=0)
                {
                    crossindex.Add(metricindex);
                }
                else if (metric < Threshold && previousscore > Threshold && previousscore != 0)
                {
                    crossindex.Add(-metricindex);
                }
                previousscore = metric;
                metricindex++;
            }
            double index = 0.1; //no intersect and above threshold
            if(crossindex.Count() > 0)
            {
                index = crossindex[0];
            }
            else if(crossindex.Count() < 1 && metricpattern.Average() < Threshold) // no intersect and below threshold
            {
                index = -0.1;
            }
            return index;
        }

        /// <summary>
        /// For tile position method 1
        /// With given threshold intersected position of certain sharpness curve, compared with input pre-calculated intersectpatter
        /// </summary>
        /// <param name="crossindex"></param>
        /// <param name="intersectpattern"></param> pre-calculated intersectpatter, a list of paired number (intersected pos, relative position away from focus)
        /// <returns>relative position away from focused position</returns>
        public static double RelativeFocusCal(double crossindex, List<List<double>> intersectpattern)
        {
            double relativepos = 0.1;
            double mindistance = 100;
            foreach (List<double> zposcross in intersectpattern)
            {
                if (zposcross[1] != 0.1 && zposcross[1] != -0.1 ) //must have intesection with threshold
                {
                    if(crossindex * zposcross[1] > 0 && Math.Abs(zposcross[1] - crossindex) < mindistance) // same slope
                    {
                        relativepos = zposcross[0];
                        mindistance = Math.Abs(zposcross[1] - crossindex);
                    }
                }
            }
            return relativepos;
        }

        /// <summary>
        /// For tilt position method 2
        /// Calculate the "Likelihood" between a new sharpness curve with unknown focus 
        /// and a pre-calculated pattern has sharpness curve + relative pos of focus pair
        /// </summary>
        /// <param name="newpattern"></param> new sharpness curve with unknown focus 
        /// <param name="metricpatternWZ"></param> pre-calculated pattern has pairs of sharpness curve + relative pos of focus 
        /// <returns>the relative pos that has hightest likelihood score.</returns>
        public static double LikelihoodRelFoc(List<double> newpattern, Dictionary<double, List<double>>metricpatternWZ)
        {
            //Test abnormal value first
            if(newpattern.Max() > metricpatternWZ[0].Max() * 1.5)
            {
                Logger.LogError("Abnormal pattern detected");
                return double.NaN;
            }
            //construct metric pattern using sum and std of distance between new pattern and each reference pattern
            Dictionary<double, double> zposSum = new Dictionary<double, double>();
            Dictionary<double, double> zposStd = new Dictionary<double, double>();
            foreach(var pattern in metricpatternWZ)
            {
                double zpos = pattern.Key;
                List<double> refmetric = new List<double>();
                refmetric = pattern.Value;
                double[] distance = new double[refmetric.Count()];
                for (int i =0; i < refmetric.Count(); i++)
                {
                     distance[i] = newpattern[i] - refmetric[i];
                }
                double distanceSum = distance.Sum(i => Math.Abs(i));
                double avg = distance.Average();
                double sum = distance.Sum(i => Math.Pow(i - avg, 2));
                double distanceStdv = Math.Sqrt(sum / distance.Count());
                double[] metrics = new double[2] { distanceSum, distanceStdv };
                zposSum.Add(distanceSum, zpos);
                zposStd.Add(distanceStdv, zpos);
            }
            foreach(var zpossum in zposSum)
            {
                Logger.Log(string.Format("SUM: {0}: {1}", zpossum.Key, zpossum.Value));
            }
            foreach (var zpossum in zposStd)
            {
                Logger.Log(string.Format("STD: {0}: {1}", zpossum.Key, zpossum.Value));
            }
            //sort data by sum, need smallest sum + smallest std, which means two pattern are very similar.
            var sumlist = zposSum.Keys.ToList();
            sumlist.Sort();
            var stdlist = zposStd.Keys.ToList();
            stdlist.Sort();
            int minStdIndex = stdlist.Count();
            double zposresult = double.NaN;
            string sumdata = string.Join(", ", sumlist);
            string stddata = string.Join(", ", stdlist);
            Logger.Log($"Sum data-{sumdata}");
            Logger.Log($"std data-{stddata}");
            // chose the one: smallest 3 sum, with smallest std
            for(int i = 0; i < 3; i++)
            {
                double zpossum = zposSum[sumlist[i]];
                for(int k = 0; k<stdlist.Count(); k ++)
                {
                    if(zposStd[stdlist[k]] == zpossum && k < minStdIndex)
                    {
                        minStdIndex = k;
                        zposresult = zpossum;
                    }
                }
            }
            return -zposresult;
        }

    }
}

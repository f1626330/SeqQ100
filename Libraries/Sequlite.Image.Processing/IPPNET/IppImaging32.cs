using System.Runtime.InteropServices;   //DLLImport

namespace Sequlite.Image.Processing
{
    namespace Imaging
    {
        internal static class IppImaging32
        {
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_8u_C1R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_8u_C3R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_8u_C4R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_16u_C1R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_16u_C3R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_16u_C4R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_8u_C3P3R(byte* pSrc, int srcStep, byte*[] pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_8u_C4P4R(byte* pSrc, int srcStep, byte*[] pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_16u_C3P3R(byte* pSrc, int srcStep, byte*[] pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_16u_C4P4R(byte* pSrc, int srcStep, byte*[] pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_8u_P3C3R(byte*[] pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_8u_P4C4R(byte*[] pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_16u_P3C3R(byte*[] pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopy_16u_P4C4R(byte*[] pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMin_8u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, byte* pMin);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMin_8u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, byte[] min);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMin_8u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, byte[] min);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMin_16u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, ushort* pMin);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMin_16u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, ushort[] min);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMin_16u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, ushort[] min);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMax_8u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, byte* pMax);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMax_8u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, byte[] max);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMax_8u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, byte[] max);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMax_16u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, ushort* pMax);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMax_16u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, ushort[] max);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMax_16u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, ushort[] max);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMax_32f_C1R(float* pSrc, int srcStep, IppiSize roiSize, float* pMax);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMinMax_8u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, byte* pMin, byte* pMax);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMinMax_8u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, byte[] min, byte[] max);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMinMax_8u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, byte[] min, byte[] max);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMinMax_16u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, ushort* pMin, ushort* pMax);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMinMax_16u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, ushort[] min, ushort[] max);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMinMax_16u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, ushort[] min, ushort[] max);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiResize_8u_C1R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiSize dstRoiSize, double xFactor, double yFactor, int interpolation);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiResize_8u_C3R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiSize dstRoiSize, double xFactor, double yFactor, int interpolation);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiResize_8u_C4R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiSize dstRoiSize, double xFactor, double yFactor, int interpolation);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiResize_16u_C1R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiSize dstRoiSize, double xFactor, double yFactor, int interpolation);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiResize_16u_C3R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiSize dstRoiSize, double xFactor, double yFactor, int interpolation);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiResize_16u_C4R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiSize dstRoiSize, double xFactor, double yFactor, int interpolation);

            //
            // Sets pixels in the image buffer to a constant value
            //

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSet_8u_C1R(byte value, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSet_8u_C3R(byte[] value, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSet_8u_C4R(byte[] value, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSet_16u_C1R(ushort value, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSet_16u_C3R(ushort[] value, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSet_16u_C4R(ushort[] value, byte* pDst, int dstStep, IppiSize roiSize);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiRotate_8u_C1R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiRect dstRoi, double angle, double xShift, double yShift, int interpolation);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiRotate_8u_C3R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiRect dstRoi, double angle, double xShift, double yShift, int interpolation);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiRotate_16u_C1R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiRect dstRoi, double angle, double xShift, double yShift, int interpolation);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiRotate_16u_C3R(byte* pSrc, IppiSize srcSize, int srcStep, IppiRect srcRoi, byte* pDst, int dstStep, IppiRect dstRoi, double angle, double xShift, double yShift, int interpolation);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMirror_8u_C1R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize, IppiAxis flip);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMirror_8u_C3R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize, IppiAxis flip);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMirror_8u_C4R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize, IppiAxis flip);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMirror_16u_C1R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize, IppiAxis flip);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMirror_16u_C3R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize, IppiAxis flip);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMirror_16u_C4R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize, IppiAxis flip);

            //
            // Arithmetic Functions
            //

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiAdd_8u_C1RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int factor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiAdd_8u_C3RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int factor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiAdd_8u_C4RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int factor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiAdd_16u_C1RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int factor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiAdd_16u_C3RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int factor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiAdd_16u_C4RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int factor);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSub_8u_C1RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSub_8u_C3RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSub_8u_C4RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSub_16u_C1RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSub_16u_C3RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiSub_16u_C4RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMul_8u_C1RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMul_8u_C3RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMul_8u_C4RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMul_16u_C1RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMul_16u_C3RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMul_16u_C4RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiMulC_32f_C1R(float* pSrc, int srcStep, float value, float* pDst, int dstStep, IppiSize roiSize);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiDiv_8u_C1RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiDiv_8u_C3RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiDiv_8u_C4RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiDiv_16u_C1RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiDiv_16u_C3RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiDiv_16u_C4RSfs(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, int scaleFactor);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiDiv_32f_C1R(float* pSrc1, int src1Step, float* pSrc2, int src2Step, float* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippimx-6.0.dll")]
            public static unsafe extern IppStatus ippiDivC_32f_C1R(float* pSrc, int srcStep, float value, float* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippimx-6.0.dll")]
            public static unsafe extern IppStatus ippiAdd_32f_C1R(float* pSrc1, int src1Step, float* pSrc2, int src2Step, float* pDst, int dstStep, IppiSize roiSize);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiFilterMedian_8u_C1R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize dstRoiSize, IppiSize maskSize, IppiPoint anchor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiFilterMedian_8u_C3R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize dstRoiSize, IppiSize maskSize, IppiPoint anchor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiFilterMedian_8u_C4R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize dstRoiSize, IppiSize maskSize, IppiPoint anchor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiFilterMedian_16u_C1R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize dstRoiSize, IppiSize maskSize, IppiPoint anchor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiFilterMedian_16u_C3R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize dstRoiSize, IppiSize maskSize, IppiPoint anchor);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiFilterMedian_16u_C4R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize dstRoiSize, IppiSize maskSize, IppiPoint anchor);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramRange_8u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, int* pHist, int* pLevels, int nLevels);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramRange_8u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramRange_8u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramRange_16u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, int* pHist, int* pLevels, int nLevels);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramRange_16u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramRange_16u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramEven_8u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, int* pHist, int* pLevels, int nLevels, int lowerLevel, int upperLevel);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramEven_8u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels, int[] lowerLevel, int[] upperLevel);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramEven_8u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels, int[] lowerLevel, int[] upperLevel);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramEven_16u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, int* pHist, int* pLevels, int nLevels, int lowerLevel, int upperLevel);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramEven_16u_C3R(byte* pSrc, int srcStep, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels, int[] lowerLevel, int[] upperLevel);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiHistogramEven_16u_C4R(byte* pSrc, int srcStep, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels, int[] lowerLevel, int[] upperLevel);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyReplicateBorder_8u_C1R(byte* pSrc, int srcStep, IppiSize srcRoiSize, byte* pDst, int dstStep, IppiSize dstSize, int topBorderHeight, int leftBorderWidth);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyReplicateBorder_8u_C3R(byte* pSrc, int srcStep, IppiSize srcRoiSize, byte* pDst, int dstStep, IppiSize dstSize, int topBorderHeight, int leftBorderWidth);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyReplicateBorder_8u_C4R(byte* pSrc, int srcStep, IppiSize srcRoiSize, byte* pDst, int dstStep, IppiSize dstSize, int topBorderHeight, int leftBorderWidth);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyReplicateBorder_16u_C1R(byte* pSrc, int srcStep, IppiSize srcRoiSize, byte* pDst, int dstStep, IppiSize dstSize, int topBorderHeight, int leftBorderWidth);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyReplicateBorder_16u_C3R(byte* pSrc, int srcStep, IppiSize srcRoiSize, byte* pDst, int dstStep, IppiSize dstSize, int topBorderHeight, int leftBorderWidth);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyReplicateBorder_16u_C4R(byte* pSrc, int srcStep, IppiSize srcRoiSize, byte* pDst, int dstStep, IppiSize dstSize, int topBorderHeight, int leftBorderWidth);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiConvert_8u16u_C1R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiConvert_8u16u_C3R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiConvert_8u16u_C4R(byte* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize);

            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiConvert_16u32f_C1R(byte* pSrc, int srcStep, float* pDst, int dstStep, IppiSize roiSize);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiConvert_32f16u_C1R(float* pSrc, int srcStep, byte* pDst, int dstStep, IppiSize roiSize, IppRoundMode roundMode);


            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyConstBorder_8u_C1R(byte* pSrc, int srcStep, IppiSize srcRoiSize,
                                                                             byte* pDst, int dstStep, IppiSize dstRoiSize,
                                                                             int topBorderHeight, int leftBorderWidth, byte value);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyConstBorder_8u_C3R(byte* pSrc, int srcStep, IppiSize srcRoiSize,
                                                                             byte* pDst, int dstStep, IppiSize dstRoiSize,
                                                                             int topBorderHeight, int leftBorderWidth, byte[] value);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyConstBorder_8u_C4R(byte* pSrc, int srcStep, IppiSize srcRoiSize,
                                                                             byte* pDst, int dstStep, IppiSize dstRoiSize,
                                                                             int topBorderHeight, int leftBorderWidth, byte[] value);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyConstBorder_16u_C1R(byte* pSrc, int srcStep, IppiSize srcRoiSize,
                                                                             byte* pDst, int dstStep, IppiSize dstRoiSize,
                                                                             int topBorderHeight, int leftBorderWidth, ushort value);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyConstBorder_16u_C3R(byte* pSrc, int srcStep, IppiSize srcRoiSize,
                                                                             byte* pDst, int dstStep, IppiSize dstRoiSize,
                                                                             int topBorderHeight, int leftBorderWidth, ushort[] value);
            [DllImport(@"Ipp\ai32\ippipx-6.0.dll")]
            public static unsafe extern IppStatus ippiCopyConstBorder_16u_C4R(byte* pSrc, int srcStep, IppiSize srcRoiSize,
                                                                             byte* pDst, int dstStep, IppiSize dstRoiSize,
                                                                             int topBorderHeight, int leftBorderWidth, ushort[] value);

            [DllImport(@"Ipp\ai32\ippcvpx-6.0.dll")]
            public static unsafe extern IppStatus ippiMean_StdDev_8u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, double* pMean, double* pStdDev);
            [DllImport(@"Ipp\ai32\ippcvpx-6.0.dll")]
            public static unsafe extern IppStatus ippiMean_StdDev_8u_C3CR(byte* pSrc, int srcStep, IppiSize roiSize, int coi, double* pMean, double* pStdDev);
            [DllImport(@"Ipp\ai32\ippcvpx-6.0.dll")]
            public static unsafe extern IppStatus ippiMean_StdDev_8u_C4CR(byte* pSrc, int srcStep, IppiSize roiSize, int coi, double* pMean, double* pStdDev);
            [DllImport(@"Ipp\ai32\ippcvpx-6.0.dll")]
            public static unsafe extern IppStatus ippiMean_StdDev_16u_C1R(byte* pSrc, int srcStep, IppiSize roiSize, double* pMean, double* pStdDev);
            [DllImport(@"Ipp\ai32\ippcvpx-6.0.dll")]
            public static unsafe extern IppStatus ippiMean_StdDev_16u_C3CR(byte* pSrc, int srcStep, IppiSize roiSize, int coi, double* pMean, double* pStdDev);
            [DllImport(@"Ipp\ai32\ippcvpx-6.0.dll")]
            public static unsafe extern IppStatus ippiMean_StdDev_16u_C4CR(byte* pSrc, int srcStep, IppiSize roiSize, int coi, double* pMean, double* pStdDev);

        }
    }
}

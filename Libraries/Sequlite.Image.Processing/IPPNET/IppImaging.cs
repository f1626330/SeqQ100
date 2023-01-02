using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Sequlite.Image.Processing
{
    namespace Imaging
    {
        /// <summary>
        /// The following enumerator defines a status of IPP operations
        /// negative value means error
        /// </summary>
        public enum IppStatus
        {
            /* errors */
            ippStsNotSupportedModeErr = -9999,      /* The requested mode is currently not supported  */
            ippStsCpuNotSupportedErr = -9998,       /* The target cpu is not supported */

            ippStsRoundModeNotSupportedErr = -213,  /* Unsupported round mode*/
            ippStsDecimateFractionErr = -212,       /* Unsupported fraction in Decimate */
            ippStsWeightErr = -211,                 /* Wrong value of weight */

            ippStsQualityIndexErr = -210,           /* Quality Index can't be calculated for image filled with constant */
            ippStsIIRPassbandRippleErr = -209,      /* Ripple in passband for Chebyshev1 design is less zero, equal to zero or greater than 29*/
            ippStsFilterFrequencyErr = -208,        /* Cut of frequency of filter is less zero, equal to zero or greater than 0.5 */
            ippStsFIRGenOrderErr = -207,            /* Order of an FIR filter for design them is less than one                    */
            ippStsIIRGenOrderErr = -206,            /* Order of an IIR filter for design them is less than one or greater than 12 */

            ippStsConvergeErr = -205,               /* The algorithm does not converge*/
            ippStsSizeMatchMatrixErr = -204,        /* Unsuitable sizes of the source matrices*/
            ippStsCountMatrixErr = -203,            /* Count value is negative or equal to 0*/
            ippStsRoiShiftMatrixErr = -202,         /* RoiShift value is negative or not dividend to size of data type*/

            ippStsResizeNoOperationErr = -201,      /* One of the output image dimensions is less than 1 pixel */
            ippStsSrcDataErr = -200,                /* The source buffer contains unsupported data */
            ippStsMaxLenHuffCodeErr = -199,         /* Huff: Max length of Huffman code is more than expected one */
            ippStsCodeLenTableErr = -198,           /* Huff: Invalid codeLenTable */
            ippStsFreqTableErr = -197,              /* Huff: Invalid freqTable */

            ippStsIncompleteContextErr = -196,      /* Crypto: set up of context is'n complete */

            ippStsSingularErr = -195,               /* Matrix is singular */
            ippStsSparseErr = -194,                 /* Tap positions are not in ascending order, negative or repeated*/
            ippStsBitOffsetErr = -193,              /* Incorrect bit offset value */
            ippStsQPErr = -192,                     /* Incorrect quantization parameter */
            ippStsVLCErr = -191,                    /* Illegal VLC or FLC during stream decoding */
            ippStsRegExpOptionsErr = -190,          /* RegExp: Options for pattern are incorrect */
            ippStsRegExpErr = -189,                 /* RegExp: The structure pRegExpState contains wrong data */
            ippStsRegExpMatchLimitErr = -188,       /* RegExp: The match limit has been exhausted */
            ippStsRegExpQuantifierErr = -187,       /* RegExp: wrong quantifier */
            ippStsRegExpGroupingErr = -186,         /* RegExp: wrong grouping */
            ippStsRegExpBackRefErr = -185,          /* RegExp: wrong back reference */
            ippStsRegExpChClassErr = -184,          /* RegExp: wrong character class */
            ippStsRegExpMetaChErr = -183,           /* RegExp: wrong metacharacter */

            ippStsStrideMatrixErr = -182,           /* Stride value is not positive or not dividend to size of data type */

            ippStsCTRSizeErr = -181,                /* Wrong value for crypto CTR block size */

            ippStsJPEG2KCodeBlockIsNotAttached = -180,  /* codeblock parameters are not attached to the state structure */
            ippStsNotPosDefErr = -179,                  /* Not positive-definite matrix */

            ippStsEphemeralKeyErr = -178,           /* ECC: Bad ephemeral key   */
            ippStsMessageErr = -177,                /* ECC: Bad message digest  */
            ippStsShareKeyErr = -176,               /* ECC: Invalid share key   */
            ippStsIvalidPublicKey = -175,           /* ECC: Invalid public key  */
            ippStsIvalidPrivateKey = -174,          /* ECC: Invalid private key */
            ippStsOutOfECErr = -173,                /* ECC: Point out of EC     */
            ippStsECCInvalidFlagErr = -172,         /* ECC: Invalid Flag        */

            ippStsMP3FrameHeaderErr = -171,         /* Error in fields IppMP3FrameHeader structure */
            ippStsMP3SideInfoErr = -170,            /* Error in fields IppMP3SideInfo structure */

            ippStsBlockStepErr = -169,              /* Step for Block less than 8 */
            ippStsMBStepErr = -168,                 /* Step for MB less than 16 */

            ippStsAacPrgNumErr = -167,              /* AAC: Invalid number of elements for one program   */
            ippStsAacSectCbErr = -166,              /* AAC: Invalid section codebook                     */
            ippStsAacSfValErr = -164,               /* AAC: Invalid scalefactor value                    */
            ippStsAacCoefValErr = -163,             /* AAC: Invalid quantized coefficient value          */
            ippStsAacMaxSfbErr = -162,              /* AAC: Invalid coefficient index  */
            ippStsAacPredSfbErr = -161,             /* AAC: Invalid predicted coefficient index  */
            ippStsAacPlsDataErr = -160,             /* AAC: Invalid pulse data attributes  */
            ippStsAacGainCtrErr = -159,             /* AAC: Gain control not supported  */
            ippStsAacSectErr = -158,                /* AAC: Invalid number of sections  */
            ippStsAacTnsNumFiltErr = -157,          /* AAC: Invalid number of TNS filters  */
            ippStsAacTnsLenErr = -156,              /* AAC: Invalid TNS region length  */
            ippStsAacTnsOrderErr = -155,            /* AAC: Invalid order of TNS filter  */
            ippStsAacTnsCoefResErr = -154,          /* AAC: Invalid bit-resolution for TNS filter coefficients  */
            ippStsAacTnsCoefErr = -153,             /* AAC: Invalid TNS filter coefficients  */
            ippStsAacTnsDirectErr = -152,           /* AAC: Invalid TNS filter direction  */
            ippStsAacTnsProfileErr = -151,          /* AAC: Invalid TNS profile  */
            ippStsAacErr = -150,                    /* AAC: Internal error  */
            ippStsAacBitOffsetErr = -149,           /* AAC: Invalid current bit offset in bitstream  */
            ippStsAacAdtsSyncWordErr = -148,        /* AAC: Invalid ADTS syncword  */
            ippStsAacSmplRateIdxErr = -147,         /* AAC: Invalid sample rate index  */
            ippStsAacWinLenErr = -146,              /* AAC: Invalid window length (not short or long)  */
            ippStsAacWinGrpErr = -145,              /* AAC: Invalid number of groups for current window length  */
            ippStsAacWinSeqErr = -144,              /* AAC: Invalid window sequence range  */
            ippStsAacComWinErr = -143,              /* AAC: Invalid common window flag  */
            ippStsAacStereoMaskErr = -142,          /* AAC: Invalid stereo mask  */
            ippStsAacChanErr = -141,                /* AAC: Invalid channel number  */
            ippStsAacMonoStereoErr = -140,          /* AAC: Invalid mono-stereo flag  */
            ippStsAacStereoLayerErr = -139,         /* AAC: Invalid this Stereo Layer flag  */
            ippStsAacMonoLayerErr = -138,           /* AAC: Invalid this Mono Layer flag  */
            ippStsAacScalableErr = -137,            /* AAC: Invalid scalable object flag  */
            ippStsAacObjTypeErr = -136,             /* AAC: Invalid audio object type  */
            ippStsAacWinShapeErr = -135,            /* AAC: Invalid window shape  */
            ippStsAacPcmModeErr = -134,             /* AAC: Invalid PCM output interleaving indicator  */
            ippStsVLCUsrTblHeaderErr = -133,            /* VLC: Invalid header inside table */
            ippStsVLCUsrTblUnsupportedFmtErr = -132,    /* VLC: Unsupported table format */
            ippStsVLCUsrTblEscAlgTypeErr = -131,        /* VLC: Unsupported Ecs-algorithm */
            ippStsVLCUsrTblEscCodeLengthErr = -130,     /* VLC: Incorrect Esc-code length inside table header */
            ippStsVLCUsrTblCodeLengthErr = -129,        /* VLC: Unsupported code length inside table */
            ippStsVLCInternalTblErr = -128,             /* VLC: Invalid internal table */
            ippStsVLCInputDataErr = -127,               /* VLC: Invalid input data */
            ippStsVLCAACEscCodeLengthErr = -126,        /* VLC: Invalid AAC-Esc code length */
            ippStsNoiseRangeErr = -125,             /* Noise value for Wiener Filter is out range. */
            ippStsUnderRunErr = -124,               /* Data under run error */
            ippStsPaddingErr = -123,                /* Detected padding error shows the possible data corruption */
            ippStsOFBSizeErr = -122,                /* Wrong value for crypto OFB block size */
            ippStsCFBSizeErr = -122,                /* Wrong value for crypto CFB block size */
            ippStsPaddingSchemeErr = -121,          /* Invalid padding scheme  */
            ippStsInvalidCryptoKeyErr = -120,       /* A compromised key causes suspansion of requested cryptographic operation  */
            ippStsLengthErr = -119,                 /* Wrong value of string length */
            ippStsBadModulusErr = -118,             /* Bad modulus caused a module inversion failure */
            ippStsLPCCalcErr = -117,                /* Linear prediction could not be evaluated */
            ippStsRCCalcErr = -116,                 /* Reflection coefficients could not be computed */
            ippStsIncorrectLSPErr = -115,           /* Incorrect Linear Spectral Pair values */
            ippStsNoRootFoundErr = -114,            /* No roots are found for equation */
            ippStsJPEG2KBadPassNumber = -113,       /* Pass number exceeds allowed limits [0,nOfPasses-1] */
            ippStsJPEG2KDamagedCodeBlock = -112,    /* Codeblock for decoding is damaged */
            ippStsH263CBPYCodeErr = -111,           /* Illegal Huffman code during CBPY stream processing */
            ippStsH263MCBPCInterCodeErr = -110,     /* Illegal Huffman code during MCBPC Inter stream processing */
            ippStsH263MCBPCIntraCodeErr = -109,     /* Illegal Huffman code during MCBPC Intra stream processing */
            ippStsNotEvenStepErr = -108,            /* Step value is not pixel multiple */
            ippStsHistoNofLevelsErr = -107,         /* Number of levels for histogram is less than 2 */
            ippStsLUTNofLevelsErr = -106,           /* Number of levels for LUT is less than 2 */
            ippStsMP4BitOffsetErr = -105,           /* Incorrect bit offset value */
            ippStsMP4QPErr = -104,                  /* Incorrect quantization parameter */
            ippStsMP4BlockIdxErr = -103,            /* Incorrect block index */
            ippStsMP4BlockTypeErr = -102,           /* Incorrect block type */
            ippStsMP4MVCodeErr = -101,              /* Illegal Huffman code during MV stream processing */
            ippStsMP4VLCCodeErr = -100,             /* Illegal Huffman code during VLC stream processing */
            ippStsMP4DCCodeErr = -99,               /* Illegal code during DC stream processing */
            ippStsMP4FcodeErr = -98,                /* Incorrect fcode value */
            ippStsMP4AlignErr = -97,                /* Incorrect buffer alignment            */
            ippStsMP4TempDiffErr = -96,             /* Incorrect temporal difference         */
            ippStsMP4BlockSizeErr = -95,            /* Incorrect size of block or macroblock */
            ippStsMP4ZeroBABErr = -94,              /* All BAB values are zero             */
            ippStsMP4PredDirErr = -93,              /* Incorrect prediction direction        */
            ippStsMP4BitsPerPixelErr = -92,         /* Incorrect number of bits per pixel    */
            ippStsMP4VideoCompModeErr = -91,        /* Incorrect video component mode        */
            ippStsMP4LinearModeErr = -90,           /* Incorrect DC linear mode */
            ippStsH263PredModeErr = -83,            /* Prediction Mode value error                                       */
            ippStsH263BlockStepErr = -82,           /* Step value is less than 8                                         */
            ippStsH263MBStepErr = -81,              /* Step value is less than 16                                        */
            ippStsH263FrameWidthErr = -80,          /* Frame width is less then 8                                        */
            ippStsH263FrameHeightErr = -79,         /* Frame height is less than or equal to zero                        */
            ippStsH263ExpandPelsErr = -78,          /* Expand pixels number is less than 8                               */
            ippStsH263PlaneStepErr = -77,           /* Step value is less than the plane width                           */
            ippStsH263QuantErr = -76,               /* Quantizer value is less than or equal to zero, or greater than 31 */
            ippStsH263MVCodeErr = -75,              /* Illegal Huffman code during MV stream processing                  */
            ippStsH263VLCCodeErr = -74,             /* Illegal Huffman code during VLC stream processing                 */
            ippStsH263DCCodeErr = -73,              /* Illegal code during DC stream processing                          */
            ippStsH263ZigzagLenErr = -72,           /* Zigzag compact length is more than 64                             */
            ippStsFBankFreqErr = -71,               /* Incorrect value of the filter bank frequency parameter */
            ippStsFBankFlagErr = -70,               /* Incorrect value of the filter bank parameter           */
            ippStsFBankErr = -69,                   /* Filter bank is not correctly initialized"              */
            ippStsNegOccErr = -67,                  /* Negative occupation count                      */
            ippStsCdbkFlagErr = -66,                /* Incorrect value of the codebook flag parameter */
            ippStsSVDCnvgErr = -65,                 /* No convergence of SVD algorithm"               */
            ippStsJPEGHuffTableErr = -64,           /* JPEG Huffman table is destroyed        */
            ippStsJPEGDCTRangeErr = -63,            /* JPEG DCT coefficient is out of the range */
            ippStsJPEGOutOfBufErr = -62,            /* Attempt to access out of the buffer    */
            ippStsDrawTextErr = -61,                /* System error in the draw text operation */
            ippStsChannelOrderErr = -60,            /* Wrong order of the destination channels */
            ippStsZeroMaskValuesErr = -59,          /* All values of the mask are zero */
            ippStsQuadErr = -58,                    /* The quadrangle is nonconvex or degenerates into triangle, line or point */
            ippStsRectErr = -57,                    /* Size of the rectangle region is less than or equal to 1 */
            ippStsCoeffErr = -56,                   /* Unallowable values of the transformation coefficients   */
            ippStsNoiseValErr = -55,                /* Bad value of noise amplitude for dithering"             */
            ippStsDitherLevelsErr = -54,            /* Number of dithering levels is out of range"             */
            ippStsNumChannelsErr = -53,             /* Bad or unsupported number of channels                   */
            ippStsCOIErr = -52,                     /* COI is out of range */
            ippStsDivisorErr = -51,                 /* Divisor is equal to zero, function is aborted */
            ippStsAlphaTypeErr = -50,               /* Illegal type of image compositing operation                           */
            ippStsGammaRangeErr = -49,              /* Gamma range bounds is less than or equal to zero                      */
            ippStsGrayCoefSumErr = -48,             /* Sum of the conversion coefficients must be less than or equal to 1    */
            ippStsChannelErr = -47,                 /* Illegal channel number                                                */
            ippStsToneMagnErr = -46,                /* Tone magnitude is less than or equal to zero                          */
            ippStsToneFreqErr = -45,                /* Tone frequency is negative, or greater than or equal to 0.5           */
            ippStsTonePhaseErr = -44,               /* Tone phase is negative, or greater than or equal to 2*PI              */
            ippStsTrnglMagnErr = -43,               /* Triangle magnitude is less than or equal to zero                      */
            ippStsTrnglFreqErr = -42,               /* Triangle frequency is negative, or greater than or equal to 0.5       */
            ippStsTrnglPhaseErr = -41,              /* Triangle phase is negative, or greater than or equal to 2*PI          */
            ippStsTrnglAsymErr = -40,               /* Triangle asymmetry is less than -PI, or greater than or equal to PI   */
            ippStsHugeWinErr = -39,                 /* Kaiser window is too huge                                             */
            ippStsJaehneErr = -38,                  /* Magnitude value is negative                                           */
            ippStsStrideErr = -37,                  /* Stride value is less than the row length */
            ippStsEpsValErr = -36,                  /* Negative epsilon value error"            */
            ippStsWtOffsetErr = -35,                /* Invalid offset value of wavelet filter                                       */
            ippStsAnchorErr = -34,                  /* Anchor point is outside the mask                                             */
            ippStsMaskSizeErr = -33,                /* Invalid mask size                                                           */
            ippStsShiftErr = -32,                   /* Shift value is less than zero                                                */
            ippStsSampleFactorErr = -31,            /* Sampling factor is less than or equal to zero                                */
            ippStsSamplePhaseErr = -30,             /* Phase value is out of range: 0 <= phase < factor                             */
            ippStsFIRMRFactorErr = -29,             /* MR FIR sampling factor is less than or equal to zero                         */
            ippStsFIRMRPhaseErr = -28,              /* MR FIR sampling phase is negative, or greater than or equal to the sampling factor */
            ippStsRelFreqErr = -27,                 /* Relative frequency value is out of range                                     */
            ippStsFIRLenErr = -26,                  /* Length of a FIR filter is less than or equal to zero                         */
            ippStsIIROrderErr = -25,                /* Order of an IIR filter is not valid */
            ippStsDlyLineIndexErr = -24,            /* Invalid value of the delay line sample index */
            ippStsResizeFactorErr = -23,            /* Resize factor(s) is less than or equal to zero */
            ippStsInterpolationErr = -22,           /* Invalid interpolation mode */
            ippStsMirrorFlipErr = -21,              /* Invalid flip mode                                         */
            ippStsMoment00ZeroErr = -20,            /* Moment value M(0,0) is too small to continue calculations */
            ippStsThreshNegLevelErr = -19,          /* Negative value of the level in the threshold operation    */
            ippStsThresholdErr = -18,               /* Invalid threshold bounds */
            ippStsContextMatchErr = -17,            /* Context parameter doesn't match the operation */
            ippStsFftFlagErr = -16,                 /* Invalid value of the FFT flag parameter */
            ippStsFftOrderErr = -15,                /* Invalid value of the FFT order parameter */
            ippStsStepErr = -14,                    /* Step value is not valid */
            ippStsScaleRangeErr = -13,              /* Scale bounds are out of the range */
            ippStsDataTypeErr = -12,                /* Bad or unsupported data type */
            ippStsOutOfRangeErr = -11,              /* Argument is out of range or point is outside the image */
            ippStsDivByZeroErr = -10,               /* An attempt to divide by zero */
            ippStsMemAllocErr = -9,                 /* Not enough memory allocated for the operation */
            ippStsNullPtrErr = -8,                  /* Null pointer error */
            ippStsRangeErr = -7,                    /* Bad values of bounds: the lower bound is greater than the upper bound */
            ippStsSizeErr = -6,                     /* Wrong value of data size */
            ippStsBadArgErr = -5,                   /* Function arg/param is bad */
            ippStsNoMemErr = -4,                    /* Not enough memory for the operation */
            ippStsSAReservedErr3 = -3,              /*  */
            ippStsErr = -2,                         /* Unknown/unspecified error */
            ippStsSAReservedErr1 = -1,              /*  */
            /*  */
            /* no errors */
            /*  */
            ippStsNoErr = 0,   /* No error, it's OK */
            /*  */
            /* warnings */
            /*  */
            ippStsNoOperation = 1,                  /* No operation has been executed */
            ippStsMisalignedBuf = 2,                /* Misaligned pointer in operation in which it must be aligned */
            ippStsSqrtNegArg = 3,                   /* Negative value(s) of the argument in the function Sqrt */
            ippStsInvZero = 4,                      /* INF result. Zero value was met by InvThresh with zero level */
            ippStsEvenMedianMaskSize = 5,           /* Even size of the Median Filter mask was replaced by the odd one */
            ippStsDivByZero = 6,                    /* Zero value(s) of the divisor in the function Div */
            ippStsLnZeroArg = 7,                    /* Zero value(s) of the argument in the function Ln     */
            ippStsLnNegArg = 8,                     /* Negative value(s) of the argument in the function Ln */
            ippStsNanArg = 9,                       /* Not a Number argument value warning                  */
            ippStsJPEGMarker = 10,                  /* JPEG marker was met in the bitstream                 */
            ippStsResFloor = 11,                    /* All result values are floored                        */
            ippStsOverflow = 12,                    /* Overflow occurred in the operation                   */
            ippStsLSFLow = 13,                      /* Quantized LP syntethis filter stability check is applied at the low boundary of [0,pi] */
            ippStsLSFHigh = 14,                     /* Quantized LP syntethis filter stability check is applied at the high boundary of [0,pi] */
            ippStsLSFLowAndHigh = 15,               /* Quantized LP syntethis filter stability check is applied at both boundaries of [0,pi] */
            ippStsZeroOcc = 16,                     /* Zero occupation count */
            ippStsUnderflow = 17,                   /* Underflow occurred in the operation */
            ippStsSingularity = 18,                 /* Singularity occurred in the operation                                       */
            ippStsDomain = 19,                      /* Argument is out of the function domain                                      */
            ippStsNonIntelCpu = 20,                 /* The target cpu is not Genuine Intel                                         */
            ippStsCpuMismatch = 21,                 /* The library for given cpu cannot be set                                     */
            ippStsNoIppFunctionFound = 22,          /* Application does not contain IPP functions calls                            */
            ippStsDllNotFoundBestUsed = 23,         /* The newest version of IPP dll's not found by dispatcher                     */
            ippStsNoOperationInDll = 24,            /* The function does nothing in the dynamic version of the library             */
            ippStsInsufficientEntropy = 25,         /* Insufficient entropy in the random seed and stimulus bit string caused the prime/key generation to fail */
            ippStsOvermuchStrings = 26,             /* Number of destination strings is more than expected                         */
            ippStsOverlongString = 27,              /* Length of one of the destination strings is more than expected              */
            ippStsAffineQuadChanged = 28,           /* 4th vertex of destination quad is not equal to customer's one               */
            ippStsWrongIntersectROI = 29,           /* Wrong ROI that has no intersection with the source or destination ROI. No operation */
            ippStsWrongIntersectQuad = 30,          /* Wrong quadrangle that has no intersection with the source or destination ROI. No operation */
            ippStsSmallerCodebook = 31,             /* Size of created codebook is less than cdbkSize argument */
            ippStsSrcSizeLessExpected = 32,         /* DC: The size of source buffer is less than expected one */
            ippStsDstSizeLessExpected = 33,         /* DC: The size of destination buffer is less than expected one */
            ippStsStreamEnd = 34,                   /* DC: The end of stream processed */
            ippStsDoubleSize = 35,                  /* Sizes of image are not multiples of 2 */
            ippStsNotSupportedCpu = 36,             /* The cpu is not supported */
            ippStsUnknownCacheSize = 37,            /* The cpu is supported, but the size of the cache is unknown */
            ippStsSymKernelExpected = 38,           /* The Kernel is not symmetric*/
            ippStsEvenMedianWeight = 39             /* Even weight of the Weighted Median Filter was replaced by the odd one */
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct IppiPoint
        {
            public int x;
            public int y;
            public IppiPoint(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        };  //struct IppiPoint

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi)]
        public struct IppiRect
        {
            [FieldOffset(0)]
            public int x;
            [FieldOffset(4)]
            public int y;
            [FieldOffset(8)]
            public int width;
            [FieldOffset(12)]
            public int height;
        };  //struct IppiRect

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct IppiSize
        {
            public int width;
            public int height;
            public IppiSize(int width, int height)
            {
                this.width = width;
                this.height = height;
            }
        };  //struct IppiSize

        public enum IppInter
        {
            IPPI_INTER_NN = 1,
            IPPI_INTER_LINEAR = 2,
            IPPI_INTER_CUBIC = 4,
            IPPI_INTER_CUBIC2P_BSPLINE,     /* two-parameter cubic filter (B=1, C=0) */
            IPPI_INTER_CUBIC2P_CATMULLROM,  /* two-parameter cubic filter (B=0, C=1/2) */
            IPPI_INTER_CUBIC2P_B05C03,      /* two-parameter cubic filter (B=1/2, C=3/10) */
            IPPI_INTER_SUPER = 8,
            IPPI_INTER_LANCZOS = 16,
            IPPI_SMOOTH_EDGE = (1 << 31)
        };

        public enum IppiAxis
        {
            ippAxsHorizontal,
            ippAxsVertical,
            ippAxsBoth
        };

        public enum IppRoundMode
        {
            ippRndZero,
            ippRndNear,
            ippRndFinancial
        };

        public enum PixelFormatType
        {
            NotSupported,
            P8u_C1,
            P8u_C3,
            P8u_AC4,
            P8u_C4,
            P16s_C1,
            P16s_C3,
            P16s_AC4,
            P16s_C4,
            P16u_C1,
            P16u_C3,
            P16u_AC4,
            P16u_C4,
            P32f_C1,
            P32f_C3,
            P32f_AC4,
            P32f_C4,
        }


        public static class IppImaging
        {
            private static bool _Is64bit;

            /// <summary>
            /// A static constructor is called automatically to initialize the class before the first instance
            /// is created or any static members are referenced.
            /// </summary>
            static IppImaging()
            {
                if (IntPtr.Size == 4)
                {
                    _Is64bit = false;
                    //IppLoader.PreloadDll("ippipx-6.0.dll");
                }
                else if (IntPtr.Size == 8)
                {
                    _Is64bit = true;
                    //IppLoader.PreloadDll("ippimx-6.0.dll");
                }
                else
                {
                    throw new NotImplementedException("Unsupported bitness");
                }
            }

            public static PixelFormatType GetPixelFormatType(PixelFormat pixelFormat)
            {
                if (pixelFormat == PixelFormats.Gray8 ||
                    pixelFormat == PixelFormats.Indexed8)
                {
                    return PixelFormatType.P8u_C1;
                }
                else if (pixelFormat == PixelFormats.Gray16)
                {
                    return PixelFormatType.P16u_C1;
                }
                else if (pixelFormat == PixelFormats.Bgr24 ||
                        pixelFormat == PixelFormats.Rgb24)
                {
                    return PixelFormatType.P8u_C3;
                }
                else if (pixelFormat == PixelFormats.Rgb48)
                {
                    return PixelFormatType.P16u_C3;
                }
                else if (pixelFormat == PixelFormats.Cmyk32)
                {
                    return PixelFormatType.P8u_C4;
                }
                else if (pixelFormat == PixelFormats.Rgba64)
                {
                    return PixelFormatType.P16u_C4;
                }
                else
                {
                    return PixelFormatType.NotSupported;
                }
            }

            public static unsafe IppStatus Min(byte* pSrc, int srcStep, PixelFormatType pixelFormat, Rect roiRect, ref uint minValue)
            {
                IppStatus ippStatus = IppStatus.ippStsNoErr;

                int width = (int)roiRect.Width;
                int height = (int)roiRect.Height;

                byte* pSrcData = null;
                IppiSize roiSize = new IppiSize(width, height);

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            byte minValue8 = 0;
                            byte* pMinValue8 = &minValue8;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMin_8u_C1R(pSrcData, srcStep, roiSize, pMinValue8);
                            minValue = minValue8;
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] arrMinValue8 = new byte[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMin_8u_C3R(pSrcData, srcStep, roiSize, arrMinValue8);
                            minValue = arrMinValue8.Min();
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] arrMinValue8uC4 = new byte[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMin_8u_C4R(pSrcData, srcStep, roiSize, arrMinValue8uC4);
                            minValue = arrMinValue8uC4.Min();
                            break;
                        case PixelFormatType.P16u_C1:
                            ushort minValue16 = 0;
                            ushort* pMinValue16 = &minValue16;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMin_16u_C1R(pSrcData, srcStep, roiSize, pMinValue16);
                            minValue = minValue16;
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usMinValue = new ushort[3];
                            ushort[] usMaxValue = new ushort[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMin_16u_C3R(pSrcData, srcStep, roiSize, usMinValue);
                            minValue = usMinValue.Min();
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usMinValue16uC4 = new ushort[4];
                            ushort[] usMaxValue16uC4 = new ushort[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMin_16u_C4R(pSrcData, srcStep, roiSize, usMinValue16uC4);
                            minValue = usMinValue16uC4.Min();
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            byte minValue8 = 0;
                            byte* pMinValue8 = &minValue8;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMin_8u_C1R(pSrcData, srcStep, roiSize, pMinValue8);
                            minValue = minValue8;
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] arrMinValue8 = new byte[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMin_8u_C3R(pSrcData, srcStep, roiSize, arrMinValue8);
                            minValue = arrMinValue8.Min();
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] arrMinValue8uC4 = new byte[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMin_8u_C4R(pSrcData, srcStep, roiSize, arrMinValue8uC4);
                            minValue = arrMinValue8uC4.Min();
                            break;
                        case PixelFormatType.P16u_C1:
                            ushort minValue16 = 0;
                            ushort* pMinValue16 = &minValue16;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMin_16u_C1R(pSrcData, srcStep, roiSize, pMinValue16);
                            minValue = minValue16;
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usMinValue = new ushort[3];
                            ushort[] usMaxValue = new ushort[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMin_16u_C3R(pSrcData, srcStep, roiSize, usMinValue);
                            minValue = usMinValue.Min();
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usMinValue16uC4 = new ushort[4];
                            ushort[] usMaxValue16uC4 = new ushort[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMin_16u_C4R(pSrcData, srcStep, roiSize, usMinValue16uC4);
                            minValue = usMinValue16uC4.Min();
                            break;
                    }
                }
                return ippStatus;
            }

            public static unsafe IppStatus Max(byte* pSrc, int srcStep, PixelFormatType pixelFormat, Rect roiRect, ref uint minValue)
            {
                IppStatus ippStatus = IppStatus.ippStsNoErr;

                int width = (int)roiRect.Width;
                int height = (int)roiRect.Height;

                byte* pSrcData = null;
                IppiSize roiSize = new IppiSize(width, height);

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            byte minValue8 = 0;
                            byte* pMinValue8 = &minValue8;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMax_8u_C1R(pSrcData, srcStep, roiSize, pMinValue8);
                            minValue = minValue8;
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] arrMinValue8 = new byte[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMax_8u_C3R(pSrcData, srcStep, roiSize, arrMinValue8);
                            minValue = arrMinValue8.Min();
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] arrMinValue8uC4 = new byte[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMax_8u_C4R(pSrcData, srcStep, roiSize, arrMinValue8uC4);
                            minValue = arrMinValue8uC4.Min();
                            break;
                        case PixelFormatType.P16u_C1:
                            ushort minValue16 = 0;
                            ushort* pMinValue16 = &minValue16;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMax_16u_C1R(pSrcData, srcStep, roiSize, pMinValue16);
                            minValue = minValue16;
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usMinValue = new ushort[3];
                            ushort[] usMaxValue = new ushort[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMax_16u_C3R(pSrcData, srcStep, roiSize, usMinValue);
                            minValue = usMinValue.Min();
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usMinValue16uC4 = new ushort[4];
                            ushort[] usMaxValue16uC4 = new ushort[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMax_16u_C4R(pSrcData, srcStep, roiSize, usMinValue16uC4);
                            minValue = usMinValue16uC4.Min();
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            byte minValue8 = 0;
                            byte* pMinValue8 = &minValue8;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMax_8u_C1R(pSrcData, srcStep, roiSize, pMinValue8);
                            minValue = minValue8;
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] arrMinValue8 = new byte[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMax_8u_C3R(pSrcData, srcStep, roiSize, arrMinValue8);
                            minValue = arrMinValue8.Min();
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] arrMinValue8uC4 = new byte[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMax_8u_C4R(pSrcData, srcStep, roiSize, arrMinValue8uC4);
                            minValue = arrMinValue8uC4.Min();
                            break;
                        case PixelFormatType.P16u_C1:
                            ushort minValue16 = 0;
                            ushort* pMinValue16 = &minValue16;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMax_16u_C1R(pSrcData, srcStep, roiSize, pMinValue16);
                            minValue = minValue16;
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usMinValue = new ushort[3];
                            ushort[] usMaxValue = new ushort[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMax_16u_C3R(pSrcData, srcStep, roiSize, usMinValue);
                            minValue = usMinValue.Min();
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usMinValue16uC4 = new ushort[4];
                            ushort[] usMaxValue16uC4 = new ushort[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMax_16u_C4R(pSrcData, srcStep, roiSize, usMinValue16uC4);
                            minValue = usMinValue16uC4.Min();
                            break;
                    }
                }
                return ippStatus;
            }

            public static unsafe IppStatus Max(float* pSrc, int srcStep, IppiSize roiSize, ref float max)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;
                float* pMax;
                fixed (float* p = &max) pMax = p;

                if (_Is64bit)
                {
                    ippStat = IppImaging64.ippiMax_32f_C1R(pSrc, srcStep, roiSize, pMax);
                }
                else
                {
                    ippStat = IppImaging32.ippiMax_32f_C1R(pSrc, srcStep, roiSize, pMax);
                }

                return ippStat;
            }

            /// <summary>
            /// Computes the minimum and maximum of image pixel values
            /// </summary>
            public static unsafe IppStatus MinMax(byte* pSrc, int srcStep, PixelFormatType pixelFormat, Rect roiRect, ref uint minValue, ref uint maxValue)
            {
                IppStatus ippStatus = IppStatus.ippStsNoErr;

                byte* pSrcData = null;
                int width = (int)roiRect.Width;
                int height = (int)roiRect.Height;
                IppiSize roiSize = new IppiSize(width, height);

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            byte minValue8 = 0;
                            byte maxValue8 = 0;
                            byte* pMinValue8 = &minValue8;
                            byte* pMaxValue8 = &maxValue8;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMinMax_8u_C1R(pSrcData, srcStep, roiSize, pMinValue8, pMaxValue8);
                            minValue = minValue8;
                            maxValue = maxValue8;
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] arrMinValue8 = new byte[3];
                            byte[] arrMaxValue8 = new byte[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMinMax_8u_C3R(pSrcData, srcStep, roiSize, arrMinValue8, arrMaxValue8);
                            minValue = arrMinValue8.Min();
                            maxValue = arrMaxValue8.Max();
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] arrMinValue8uC4 = new byte[4];
                            byte[] arrMaxValue8uC4 = new byte[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMinMax_8u_C4R(pSrcData, srcStep, roiSize, arrMinValue8uC4, arrMaxValue8uC4);
                            minValue = arrMinValue8uC4.Min();
                            maxValue = arrMaxValue8uC4.Max();
                            break;
                        case PixelFormatType.P16u_C1:
                            ushort minValue16 = 0;
                            ushort maxValue16 = 0;
                            ushort* pMinValue16 = &minValue16;
                            ushort* pMaxValue16 = &maxValue16;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMinMax_16u_C1R(pSrcData, srcStep, roiSize, pMinValue16, pMaxValue16);
                            minValue = minValue16;
                            maxValue = maxValue16;
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usMinValue = new ushort[3];
                            ushort[] usMaxValue = new ushort[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMinMax_16u_C3R(pSrcData, srcStep, roiSize, usMinValue, usMaxValue);
                            minValue = usMinValue.Min();
                            maxValue = usMaxValue.Max();
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usMinValue16uC4 = new ushort[4];
                            ushort[] usMaxValue16uC4 = new ushort[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiMinMax_16u_C4R(pSrcData, srcStep, roiSize, usMinValue16uC4, usMaxValue16uC4);
                            minValue = usMinValue16uC4.Min();
                            maxValue = usMaxValue16uC4.Max();
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            byte minValue8 = 0;
                            byte maxValue8 = 0;
                            byte* pMinValue8 = &minValue8;
                            byte* pMaxValue8 = &maxValue8;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMinMax_8u_C1R(pSrcData, srcStep, roiSize, pMinValue8, pMaxValue8);
                            minValue = minValue8;
                            maxValue = maxValue8;
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] arrMinValue8 = new byte[3];
                            byte[] arrMaxValue8 = new byte[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMinMax_8u_C3R(pSrcData, srcStep, roiSize, arrMinValue8, arrMaxValue8);
                            minValue = arrMinValue8.Min();
                            maxValue = arrMaxValue8.Max();
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] arrMinValue8uC4 = new byte[4];
                            byte[] arrMaxValue8uC4 = new byte[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMinMax_8u_C4R(pSrcData, srcStep, roiSize, arrMinValue8uC4, arrMaxValue8uC4);
                            minValue = arrMinValue8uC4.Min();
                            maxValue = arrMaxValue8uC4.Max();
                            break;
                        case PixelFormatType.P16u_C1:
                            ushort minValue16 = 0;
                            ushort maxValue16 = 0;
                            ushort* pMinValue16 = &minValue16;
                            ushort* pMaxValue16 = &maxValue16;
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMinMax_16u_C1R(pSrcData, srcStep, roiSize, pMinValue16, pMaxValue16);
                            minValue = minValue16;
                            maxValue = maxValue16;
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usMinValue = new ushort[3];
                            ushort[] usMaxValue = new ushort[3];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMinMax_16u_C3R(pSrcData, srcStep, roiSize, usMinValue, usMaxValue);
                            minValue = usMinValue.Min();
                            maxValue = usMaxValue.Max();
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usMinValue16uC4 = new ushort[4];
                            ushort[] usMaxValue16uC4 = new ushort[4];
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiMinMax_16u_C4R(pSrcData, srcStep, roiSize, usMinValue16uC4, usMaxValue16uC4);
                            minValue = usMinValue16uC4.Min();
                            maxValue = usMaxValue16uC4.Max();
                            break;
                    }
                }
                return ippStatus;
            }

            /// <summary>
            /// Retrieves the red, green, blue channel of a 24- or 48-bit image.
            /// </summary>
            /// <param name="srcBitmap"></param>
            /// <returns></returns>
            /*public static unsafe WriteableBitmap[] GetChannel(WriteableBitmap srcBitmap)
            {
                if (srcBitmap == null)
                    return null;
                if (srcBitmap.Format != PixelFormats.Bgr24 && srcBitmap.Format != PixelFormats.Rgb48)
                    return null;

                int iWidth = srcBitmap.PixelWidth;
                int iHeight = srcBitmap.PixelHeight;
                int iBitsPerPixel = srcBitmap.Format.BitsPerPixel;
                PixelFormat srcPixelFormat = srcBitmap.Format;
                byte[] redChData = null;
                byte[] greenChData = null;
                byte[] blueChData = null;

                PixelFormat dstPixelFormat = PixelFormats.Gray16;

                if (srcPixelFormat == PixelFormats.Bgr24)
                    dstPixelFormat = PixelFormats.Gray8;
                else if (srcPixelFormat == PixelFormats.Rgb48)
                    dstPixelFormat = PixelFormats.Gray16;

                int iSrcStep = ((iWidth * iBitsPerPixel) + 31) / 32 * 4;
                int iDstStep = ((iWidth * dstPixelFormat.BitsPerPixel) + 31) / 32 * 4;
                byte* pSrcData = (byte*)srcBitmap.BackBuffer.ToPointer();
                byte*[] dst;
                WriteableBitmap[] dstBitmap = { null, null, null };
                IppiSize roiSize = new IppiSize(iWidth, iHeight);

                try
                {
                    redChData = new byte[iDstStep * iHeight];
                    greenChData = new byte[iDstStep * iHeight];
                    blueChData = new byte[iDstStep * iHeight];

                    dst = new byte*[3];
                    fixed (byte* p = &redChData[0]) dst[0] = p;
                    fixed (byte* p = &greenChData[0]) dst[1] = p;
                    fixed (byte* p = &blueChData[0]) dst[2] = p;

                    if (_Is64bit)
                    {
                        if (srcPixelFormat == PixelFormats.Bgr24)
                        {
                            IppImaging64.ippiCopy_8u_C3P3R(pSrcData, iSrcStep, dst, iDstStep, roiSize);
                        }
                        else if (srcPixelFormat == PixelFormats.Rgb48)
                        {
                            IppImaging64.ippiCopy_16u_C3P3R(pSrcData, iSrcStep, dst, iDstStep, roiSize);
                        }
                    }
                    else
                    {
                        if (srcPixelFormat == PixelFormats.Bgr24)
                        {
                            IppImaging32.ippiCopy_8u_C3P3R(pSrcData, iSrcStep, dst, iDstStep, roiSize);
                        }
                        else if (srcPixelFormat == PixelFormats.Rgb48)
                        {
                            IppImaging32.ippiCopy_16u_C3P3R(pSrcData, iSrcStep, dst, iDstStep, roiSize);
                        }
                    }

                    dstBitmap[0] = new WriteableBitmap(BitmapSource.Create(iWidth, iHeight, 96, 96, dstPixelFormat, null, redChData, iDstStep));
                    dstBitmap[1] = new WriteableBitmap(BitmapSource.Create(iWidth, iHeight, 96, 96, dstPixelFormat, null, greenChData, iDstStep));
                    dstBitmap[2] = new WriteableBitmap(BitmapSource.Create(iWidth, iHeight, 96, 96, dstPixelFormat, null, blueChData, iDstStep));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("GetChannel: " + ex.Message);
                    //throw new Exception("RGBExtract: " + ex.Message);
                    throw ex;
                }

                redChData = null;
                greenChData = null;
                blueChData = null;

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

                return dstBitmap;
            }*/

            public static unsafe IppStatus GetChannel(byte* pSrc, int srcStep, IppiSize roiSize, byte*[] pDst, int dstStep, PixelFormatType pixelFormat)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                int iWidth = roiSize.width;
                int iHeight = roiSize.height;

                try
                {
                    if (_Is64bit)
                    {
                        switch (pixelFormat)
                        {
                            case PixelFormatType.P8u_C3:
                                ippStat = IppImaging64.ippiCopy_8u_C3P3R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P16u_C3:
                                ippStat = IppImaging64.ippiCopy_16u_C3P3R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P8u_C4:
                                ippStat = IppImaging64.ippiCopy_8u_C4P4R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P16u_C4:
                                ippStat = IppImaging64.ippiCopy_16u_C4P4R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                        }
                    }
                    else
                    {
                        switch (pixelFormat)
                        {
                            case PixelFormatType.P8u_C3:
                                ippStat = IppImaging32.ippiCopy_8u_C3P3R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P16u_C3:
                                ippStat = IppImaging32.ippiCopy_16u_C3P3R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P8u_C4:
                                ippStat = IppImaging32.ippiCopy_8u_C4P4R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P16u_C4:
                                ippStat = IppImaging32.ippiCopy_16u_C4P4R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                        }
                    }
                }
                catch (Exception)
                {
                    //System.Diagnostics.Debug.WriteLine("GetChannel: " + ex.Message);
                    throw;
                }

                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

                return ippStat;
            }

            /// <summary>
            /// Insert 8-bit or 16-bit images into into a 24- or 48-bit image. All the images must have to same width and height.
            /// </summary>
            /// <param name="rChBitmap"></param>
            /// <param name="gChBitmap"></param>
            /// <param name="bChBitmap"></param>
            /// <returns></returns>
            /*public static unsafe WriteableBitmap SetChannel(WriteableBitmap rChBitmap, WriteableBitmap gChBitmap, WriteableBitmap bChBitmap)
            {
                if (rChBitmap == null && gChBitmap == null && bChBitmap == null)
                    return null;

                int iWidth = 0;
                int iHeight = 0;
                int iSrcStep = 0;
                int iDstStep = 0;
                int iBitsPerPixel = 0;
                byte* pSrcR = null;
                byte* pSrcG = null;
                byte* pSrcB = null;
                WriteableBitmap dstBitmap = null;
                PixelFormat srcPixelFormat = PixelFormats.Gray8;
                byte*[] pSrc;
                byte* pDstData;

                try
                {
                    if (rChBitmap != null)
                    {
                        // Reserve the back buffer for updates.
                        rChBitmap.Lock();

                        if (rChBitmap.Format.BitsPerPixel != 8 && rChBitmap.Format.BitsPerPixel != 16)
                        {
                            return null;
                        }

                        srcPixelFormat = rChBitmap.Format;

                        iWidth = rChBitmap.PixelWidth;
                        iHeight = rChBitmap.PixelHeight;
                        iBitsPerPixel = rChBitmap.Format.BitsPerPixel;
                        iSrcStep = ((iWidth * iBitsPerPixel) + 31) / 32 * 4;
                        pSrcR = (byte*)(void*)rChBitmap.BackBuffer.ToPointer();
                    }

                    if (gChBitmap != null)
                    {
                        // Reserve the back buffer for updates.
                        gChBitmap.Lock();

                        if (gChBitmap.Format.BitsPerPixel != 8 && gChBitmap.Format.BitsPerPixel != 16)
                        {
                            return null;
                        }

                        srcPixelFormat = gChBitmap.Format;

                        iWidth = gChBitmap.PixelWidth;
                        iHeight = gChBitmap.PixelHeight;
                        iBitsPerPixel = gChBitmap.Format.BitsPerPixel;
                        iSrcStep = ((iWidth * iBitsPerPixel) + 31) / 32 * 4;
                        pSrcG = (byte*)(void*)gChBitmap.BackBuffer.ToPointer();
                    }

                    if (bChBitmap != null)
                    {
                        // Reserve the back buffer for updates.
                        bChBitmap.Lock();

                        if (bChBitmap.Format.BitsPerPixel != 8 && bChBitmap.Format.BitsPerPixel != 16)
                        {
                            return null;
                        }

                        srcPixelFormat = bChBitmap.Format;

                        iWidth = bChBitmap.PixelWidth;
                        iHeight = bChBitmap.PixelHeight;
                        iBitsPerPixel = bChBitmap.Format.BitsPerPixel;
                        iSrcStep = ((iWidth * iBitsPerPixel) + 31) / 32 * 4;
                        pSrcB = (byte*)(void*)bChBitmap.BackBuffer.ToPointer();
                    }

                    //Allocate memory for the destination bitmap
                    if (iBitsPerPixel == 8)
                    {
                        iDstStep = ((iWidth * PixelFormats.Bgr24.BitsPerPixel) + 31) / 32 * 4;
                        dstBitmap = new WriteableBitmap(iWidth, iHeight, 96, 96, PixelFormats.Bgr24, null);
                    }
                    else
                    {
                        iDstStep = ((iWidth * PixelFormats.Rgb48.BitsPerPixel) + 31) / 32 * 4;
                        dstBitmap = new WriteableBitmap(iWidth, iHeight, 96, 96, PixelFormats.Rgb48, null);
                    }

                    //
                    //Ipp ippiCopy_8u_P3C3R and ippiCopy_16u_P3C3R source images can't be null
                    //
                    if (rChBitmap == null)
                    {
                        rChBitmap = new WriteableBitmap(iWidth, iHeight, 96, 96, srcPixelFormat, null);
                        rChBitmap.Lock();   // Reserve the back buffer for updates.
                        pSrcR = (byte*)(void*)rChBitmap.BackBuffer.ToPointer();
                    }
                    if (gChBitmap == null)
                    {
                        gChBitmap = new WriteableBitmap(iWidth, iHeight, 96, 96, srcPixelFormat, null);
                        gChBitmap.Lock();   // Reserve the back buffer for updates.
                        pSrcG = (byte*)(void*)gChBitmap.BackBuffer.ToPointer();
                    }
                    if (bChBitmap == null)
                    {
                        bChBitmap = new WriteableBitmap(iWidth, iHeight, 96, 96, srcPixelFormat, null);
                        bChBitmap.Lock();   // Reserve the back buffer for updates.
                        pSrcB = (byte*)(void*)bChBitmap.BackBuffer.ToPointer();
                    }

                    pSrc = new byte*[3];

                    pSrc[0] = pSrcB;
                    pSrc[1] = pSrcG;
                    pSrc[2] = pSrcR;

                    pDstData = (byte*)(void*)dstBitmap.BackBuffer.ToPointer();
                    IppiSize roiSize = new IppiSize(iWidth, iHeight);

                    if (_Is64bit)
                    {
                        if (iBitsPerPixel == 8)
                        {
                            IppImaging64.ippiCopy_8u_P3C3R(pSrc, iSrcStep, pDstData, iDstStep, roiSize);
                        }
                        else if (iBitsPerPixel == 16)
                        {
                            IppImaging64.ippiCopy_16u_P3C3R(pSrc, iSrcStep, pDstData, iDstStep, roiSize);
                        }
                    }
                    else
                    {
                        if (iBitsPerPixel == 8)
                        {
                            IppImaging32.ippiCopy_8u_P3C3R(pSrc, iSrcStep, pDstData, iDstStep, roiSize);
                        }
                        else if (iBitsPerPixel == 16)
                        {
                            IppImaging32.ippiCopy_16u_P3C3R(pSrc, iSrcStep, pDstData, iDstStep, roiSize);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    // Release the back buffer and make it available for display.
                    if (rChBitmap != null) { rChBitmap.Unlock(); }
                    if (gChBitmap != null) { gChBitmap.Unlock(); }
                    if (bChBitmap != null) { bChBitmap.Unlock(); }
                }

                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.Collect();

                return dstBitmap;
            }*/

            public static unsafe IppStatus SetChannel(byte* pSrcR, byte* pSrcG, byte* pSrcB, int srcStep, IppiSize roiSize, PixelFormatType pixelFormat, byte* pDst, int dstStep)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                byte*[] pSrc = new byte*[3] { pSrcR, pSrcG, pSrcB };

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiCopy_8u_P3C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiCopy_16u_P3C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiCopy_8u_P3C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiCopy_16u_P3C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                    }
                }

                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.Collect();

                return ippStat;
            }

            public static unsafe IppStatus SetChannel4C(byte* pSrcR, byte* pSrcG, byte* pSrcB, byte* pSrcGr, int srcStep, IppiSize roiSize, PixelFormatType pixelFormat, byte* pDst, int dstStep)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                byte*[] pSrc = new byte*[4] { pSrcR, pSrcG, pSrcB, pSrcGr };

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiCopy_8u_P4C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiCopy_16u_P4C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiCopy_8u_P4C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiCopy_16u_P4C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                    }
                }

                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.Collect();

                return ippStat;
            }

            public static unsafe IppStatus SetChannel(byte*[] pSrc, int srcStep, IppiSize roiSize, PixelFormatType pixelFormat, byte* pDst, int dstStep)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    if (pSrc.Length == 3)   // 3-channel merge
                    {
                        switch (pixelFormat)
                        {
                            case PixelFormatType.P8u_C1:
                                ippStat = IppImaging64.ippiCopy_8u_P3C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P16u_C1:
                                ippStat = IppImaging64.ippiCopy_16u_P3C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                        }
                    }
                    else if (pSrc.Length == 4)  // 4-channel merge
                    {
                        switch (pixelFormat)
                        {
                            case PixelFormatType.P8u_C1:
                                ippStat = IppImaging64.ippiCopy_8u_P4C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P16u_C1:
                                ippStat = IppImaging64.ippiCopy_16u_P4C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                        }
                    }
                }
                else
                {
                    if (pSrc.Length == 3)   // 3-channel merge
                    {
                        switch (pixelFormat)
                        {
                            case PixelFormatType.P8u_C1:
                                ippStat = IppImaging32.ippiCopy_8u_P3C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P16u_C1:
                                ippStat = IppImaging32.ippiCopy_16u_P3C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                        }
                    }
                    else if (pSrc.Length == 4)  // 4-channel merge
                    {
                        switch (pixelFormat)
                        {
                            case PixelFormatType.P8u_C1:
                                ippStat = IppImaging32.ippiCopy_8u_P4C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                            case PixelFormatType.P16u_C1:
                                ippStat = IppImaging32.ippiCopy_16u_P4C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                                break;
                        }
                    }
                }

                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.WaitForPendingFinalizers();
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                //GC.Collect();

                return ippStat;
            }

            public static unsafe IppStatus Crop(byte* pSrc, int srcStep, byte* pDst, int dstStep, Rect roiRect, PixelFormatType pixelType)
            {
                IppStatus ippStatus = IppStatus.ippStsNoErr;

                IppiSize roiSize = new IppiSize((int)roiRect.Width, (int)roiRect.Height);
                byte* pSrcData = null;

                if (_Is64bit)
                {
                    switch (pixelType)
                    {
                        case PixelFormatType.P8u_C1:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + (int)roiRect.X;
                            ippStatus = IppImaging64.ippiCopy_8u_C1R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P8u_C3:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiCopy_8u_C3R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P8u_C4:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiCopy_8u_C4R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C1:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiCopy_16u_C1R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C3:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiCopy_16u_C3R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C4:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging64.ippiCopy_16u_C4R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                    }
                }
                else
                {
                    switch (pixelType)
                    {
                        case PixelFormatType.P8u_C1:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + (int)roiRect.X;
                            ippStatus = IppImaging32.ippiCopy_8u_C1R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P8u_C3:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiCopy_8u_C3R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P8u_C4:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiCopy_8u_C4R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C1:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiCopy_16u_C1R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C3:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 3 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiCopy_16u_C3R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P16u_C4:
                            pSrcData = pSrc + srcStep * (int)roiRect.Y + 4 * 2 * (int)roiRect.X;
                            ippStatus = IppImaging32.ippiCopy_16u_C4R(pSrcData, srcStep, pDst, dstStep, roiSize);
                            break;
                    }
                }

                return ippStatus;
            }

            public static unsafe IppStatus Resize(byte* pSrc, IppiSize srcSize, int srcStep, PixelFormatType pixelFormat, byte* pDst, int dstStep, IppiSize dstSize)
            {
                IppStatus ippStatus = IppStatus.ippStsNoErr;
                IppiRect srcRoi;

                srcRoi.x = 0;
                srcRoi.y = 0;
                srcRoi.width = srcSize.width;
                srcRoi.height = srcSize.height;

                double xFactor = (double)dstSize.width / (double)srcSize.width;
                double yFactor = (double)dstSize.height / (double)srcSize.height;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStatus = IppImaging64.ippiResize_8u_C1R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStatus = IppImaging64.ippiResize_8u_C3R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStatus = IppImaging64.ippiResize_8u_C4R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStatus = IppImaging64.ippiResize_16u_C1R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStatus = IppImaging64.ippiResize_16u_C3R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStatus = IppImaging64.ippiResize_16u_C4R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStatus = IppImaging32.ippiResize_8u_C1R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStatus = IppImaging32.ippiResize_8u_C3R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStatus = IppImaging32.ippiResize_8u_C4R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStatus = IppImaging32.ippiResize_16u_C1R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStatus = IppImaging32.ippiResize_16u_C3R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStatus = IppImaging32.ippiResize_16u_C4R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, dstSize, xFactor, yFactor, (int)IppInter.IPPI_INTER_LINEAR);
                            break;
                    }
                }

                return ippStatus;
            }

            public static unsafe IppStatus Invert(byte* pSrc, int srcStep, IppiSize roiSize, PixelFormatType pixelFormat, byte* pDst, int dstStep)
            {
                IppStatus ippStatus = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStatus = IppImaging64.ippiSet_8u_C1R(255, pDst, dstStep, roiSize);
                            ippStatus = IppImaging64.ippiSub_8u_C1RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] values = new byte[] { 255, 255, 255 };
                            ippStatus = IppImaging64.ippiSet_8u_C3R(values, pDst, dstStep, roiSize);
                            ippStatus = IppImaging64.ippiSub_8u_C3RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] values8uC4 = new byte[] { 255, 255, 255, 255 };
                            ippStatus = IppImaging64.ippiSet_8u_C4R(values8uC4, pDst, dstStep, roiSize);
                            ippStatus = IppImaging64.ippiSub_8u_C4RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStatus = IppImaging64.ippiSet_16u_C1R(65535, pDst, dstStep, roiSize);
                            ippStatus = IppImaging64.ippiSub_16u_C1RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usValues = new ushort[] { 65535, 65535, 65535 };
                            ippStatus = IppImaging64.ippiSet_16u_C3R(usValues, pDst, dstStep, roiSize);
                            ippStatus = IppImaging64.ippiSub_16u_C3RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usValues16uC4 = new ushort[] { 65535, 65535, 65535, 65535 };
                            ippStatus = IppImaging64.ippiSet_16u_C4R(usValues16uC4, pDst, dstStep, roiSize);
                            ippStatus = IppImaging64.ippiSub_16u_C4RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStatus = IppImaging32.ippiSet_8u_C1R(255, pDst, dstStep, roiSize);
                            ippStatus = IppImaging32.ippiSub_8u_C1RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] values = new byte[] { 255, 255, 255 };
                            ippStatus = IppImaging32.ippiSet_8u_C3R(values, pDst, dstStep, roiSize);
                            ippStatus = IppImaging32.ippiSub_8u_C3RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] values8uC4 = new byte[] { 255, 255, 255, 255 };
                            ippStatus = IppImaging32.ippiSet_8u_C4R(values8uC4, pDst, dstStep, roiSize);
                            ippStatus = IppImaging32.ippiSub_8u_C4RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStatus = IppImaging32.ippiSet_16u_C1R(65535, pDst, dstStep, roiSize);
                            ippStatus = IppImaging32.ippiSub_16u_C1RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usValues = new ushort[] { 65535, 65535, 65535 };
                            ippStatus = IppImaging32.ippiSet_16u_C3R(usValues, pDst, dstStep, roiSize);
                            ippStatus = IppImaging32.ippiSub_16u_C3RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usValues16uC4 = new ushort[] { 65535, 65535, 65535, 65535 };
                            ippStatus = IppImaging32.ippiSet_16u_C4R(usValues16uC4, pDst, dstStep, roiSize);
                            ippStatus = IppImaging32.ippiSub_16u_C4RSfs(pSrc, srcStep, pDst, dstStep, pDst, dstStep, roiSize, 0);
                            break;
                    }
                }

                return ippStatus;
            }
            public static unsafe IppStatus Rotate(byte* pSrc, IppiSize srcSize, int srcStep, byte* pDst, int dstStep, double angle, double xShift, double yShift, int interpolation, PixelFormatType pixelFormat)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;
                IppiRect srcRoi;

                srcRoi.x = 0;
                srcRoi.y = 0;
                srcRoi.width = srcSize.width;
                srcRoi.height = srcSize.height;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiRotate_8u_C1R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, srcRoi, angle, xShift, yShift, interpolation);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiRotate_8u_C3R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, srcRoi, angle, xShift, yShift, interpolation);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiRotate_16u_C1R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, srcRoi, angle, xShift, yShift, interpolation);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiRotate_16u_C3R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, srcRoi, angle, xShift, yShift, interpolation);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiRotate_8u_C1R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, srcRoi, angle, xShift, yShift, interpolation);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiRotate_8u_C3R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, srcRoi, angle, xShift, yShift, interpolation);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiRotate_16u_C1R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, srcRoi, angle, xShift, yShift, interpolation);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiRotate_16u_C3R(pSrc, srcSize, srcStep, srcRoi, pDst, dstStep, srcRoi, angle, xShift, yShift, interpolation);
                            break;
                    }
                }

                return ippStat;
            }
            public static unsafe IppStatus Flip(byte* pSrc, int srcStep, PixelFormatType pixelFormat, byte* pDst, int dstStep, IppiSize roiSize, IppiAxis flip)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiMirror_8u_C1R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiMirror_8u_C3R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiMirror_8u_C4R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiMirror_16u_C1R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiMirror_16u_C3R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiMirror_16u_C4R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiMirror_8u_C1R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiMirror_8u_C3R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiMirror_8u_C4R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiMirror_16u_C1R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiMirror_16u_C3R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiMirror_16u_C4R(pSrc, srcStep, pDst, dstStep, roiSize, flip);
                            break;
                    }
                }

                return ippStat;
            }

            //
            //Arithmetic Functions
            //

            //Adds pixel values of two images.
            public static unsafe IppStatus Add(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, PixelFormatType pixelFormat)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;
                int scaleFactor = 0;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiAdd_8u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiAdd_8u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiAdd_8u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiAdd_16u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiAdd_16u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiAdd_16u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiAdd_8u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiAdd_8u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiAdd_8u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiAdd_16u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiAdd_16u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiAdd_16u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                    }
                }

                return ippStat;
            }

            public static unsafe IppStatus Sub(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, PixelFormatType pixelFormat)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;
                int scaleFactor = 0;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiSub_8u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiSub_8u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiSub_8u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiSub_16u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiSub_16u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiSub_16u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiSub_8u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiSub_8u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiSub_8u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiSub_16u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiSub_16u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiSub_16u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                    }
                }

                return ippStat;
            }

            public static unsafe IppStatus Mul(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, PixelFormatType pixelFormat)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;
                int scaleFactor = 0;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiMul_8u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiMul_8u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiMul_8u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiMul_16u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiMul_16u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiMul_16u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiMul_8u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiMul_8u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiMul_8u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiMul_16u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiMul_16u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiMul_16u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                    }
                }

                return ippStat;
            }

            public static unsafe IppStatus MulC(float* pSrc, int srcStep, float value, float* pDst, int dstStep, IppiSize roiSize)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    ippStat = IppImaging64.ippiMulC_32f_C1R(pSrc, srcStep, value, pDst, dstStep, roiSize);
                }
                else
                {
                    ippStat = IppImaging32.ippiMulC_32f_C1R(pSrc, srcStep, value, pDst, dstStep, roiSize);
                }

                return ippStat;
            }

            public static unsafe IppStatus Div(byte* pSrc1, int src1Step, byte* pSrc2, int src2Step, byte* pDst, int dstStep, IppiSize roiSize, PixelFormatType pixelFormat)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;
                int scaleFactor = 0;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiDiv_8u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiDiv_8u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiDiv_8u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiDiv_16u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiDiv_16u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiDiv_16u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiDiv_8u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiDiv_8u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiDiv_8u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiDiv_16u_C1RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiDiv_16u_C3RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiDiv_16u_C4RSfs(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize, scaleFactor);
                            break;
                    }
                }

                return ippStat;
            }

            //This function divides pixel values of the source buffer pSrc2 by the corresponding pixel
            //values of the buffer pSrc1 and places the result in a destination buffer pDst.
            public static unsafe IppStatus Div_32f_C1R(float* pSrc1, int src1Step, float* pSrc2, int src2Step, float* pDst, int dstStep, IppiSize roiSize)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    ippStat = IppImaging64.ippiDiv_32f_C1R(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize);
                }
                else
                {
                    ippStat = IppImaging32.ippiDiv_32f_C1R(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize);
                }

                return ippStat;
            }

            public static unsafe IppStatus DivC_32f_C1R(float* pSrcData, int srcStep, float value, float* pDstData, int dstStep, IppiSize roiSize)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    ippStat = IppImaging64.ippiDivC_32f_C1R(pSrcData, srcStep, value, pDstData, dstStep, roiSize);
                }
                else
                {
                    ippStat = IppImaging32.ippiDivC_32f_C1R(pSrcData, srcStep, value, pDstData, dstStep, roiSize);
                }

                return ippStat;
            }

            public static unsafe IppStatus Add_32f_C1R(float* pSrc1, int src1Step, float* pSrc2, int src2Step, float* pDst, int dstStep, IppiSize roiSize)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    ippStat = IppImaging64.ippiAdd_32f_C1R(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize);
                }
                else
                {
                    ippStat = IppImaging32.ippiAdd_32f_C1R(pSrc1, src1Step, pSrc2, src2Step, pDst, dstStep, roiSize);
                }

                return ippStat;
            }


            public static unsafe IppStatus MedianFilter(byte* pSrc, int srcStep, IppiSize roiSize, PixelFormatType pixelFormat, byte* pDst, int dstStep)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                IppiSize dstRoiSize = new IppiSize(roiSize.width - 2, roiSize.height - 2);
                IppiSize maskSize = new IppiSize(3, 3); // kernel size
                IppiPoint anchor = new IppiPoint(1, 1);
                byte* pSrcData = null;
                byte* pDstData = null;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            pSrc = pSrc + srcStep + 1;
                            pDst = pDst + dstStep + 1;
                            ippStat = IppImaging64.ippiFilterMedian_8u_C1R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P8u_C3:
                            pSrcData = pSrc + srcStep + 1 * 3;
                            pDstData = pDst + dstStep + 1 * 3;
                            ippStat = IppImaging64.ippiFilterMedian_8u_C3R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P8u_C4:
                            pSrcData = pSrc + srcStep + 1 * 4;
                            pDstData = pDst + dstStep + 1 * 4;
                            ippStat = IppImaging64.ippiFilterMedian_8u_C4R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P16u_C1:
                            pSrcData = pSrc + srcStep + 1 * 2;
                            pDstData = pDst + dstStep + 1 * 2;
                            ippStat = IppImaging64.ippiFilterMedian_16u_C1R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P16u_C3:
                            pSrcData = pSrc + srcStep + 2 * 3;
                            pDstData = pDst + dstStep + 2 * 3;
                            ippStat = IppImaging64.ippiFilterMedian_16u_C3R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P16u_C4:
                            pSrcData = pSrc + srcStep + 2 * 4;
                            pDstData = pDst + dstStep + 2 * 4;
                            ippStat = IppImaging64.ippiFilterMedian_16u_C4R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            pSrc = pSrc + srcStep + 1;
                            pDst = pDst + dstStep + 1;
                            ippStat = IppImaging32.ippiFilterMedian_8u_C1R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P8u_C3:
                            pSrcData = pSrc + srcStep + 1 * 3;
                            pDstData = pDst + dstStep + 1 * 3;
                            ippStat = IppImaging32.ippiFilterMedian_8u_C3R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P8u_C4:
                            pSrcData = pSrc + srcStep + 1 * 4;
                            pDstData = pDst + dstStep + 1 * 4;
                            ippStat = IppImaging32.ippiFilterMedian_8u_C4R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P16u_C1:
                            pSrcData = pSrc + srcStep + 1 * 2;
                            pDstData = pDst + dstStep + 1 * 2;
                            ippStat = IppImaging32.ippiFilterMedian_16u_C1R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P16u_C3:
                            pSrcData = pSrc + srcStep + 2 * 3;
                            pDstData = pDst + dstStep + 2 * 3;
                            ippStat = IppImaging32.ippiFilterMedian_16u_C3R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                        case PixelFormatType.P16u_C4:
                            pSrcData = pSrc + srcStep + 2 * 4;
                            pDstData = pDst + dstStep + 2 * 4;
                            ippStat = IppImaging32.ippiFilterMedian_16u_C4R(pSrcData, srcStep, pDstData, dstStep, dstRoiSize, maskSize, anchor);
                            break;
                    }
                }

                return ippStat;
            }

            public static unsafe IppStatus CopyReplicateBorder(byte* pSrc, int srcStep, IppiSize srcRoiSize, PixelFormatType pixelFormat,
                                                               byte* pDst, int dstStep, IppiSize dstRoiSize, int topBorderHeight, int leftBorderWidth)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiCopyReplicateBorder_8u_C1R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiCopyReplicateBorder_8u_C3R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiCopyReplicateBorder_8u_C4R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiCopyReplicateBorder_16u_C1R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiCopyReplicateBorder_16u_C3R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiCopyReplicateBorder_16u_C4R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiCopyReplicateBorder_8u_C1R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiCopyReplicateBorder_8u_C3R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiCopyReplicateBorder_8u_C4R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiCopyReplicateBorder_16u_C1R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiCopyReplicateBorder_16u_C3R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiCopyReplicateBorder_16u_C4R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth);
                            break;
                    }
                }

                return ippStat;
            }

            public static unsafe IppStatus CopyConstBorder(byte* pSrc, int srcStep, IppiSize srcRoiSize, PixelFormatType pixelFormat,
                                                           byte* pDst, int dstStep, IppiSize dstRoiSize, int topBorderHeight, int leftBorderWidth, ushort value)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            byte valueInByte = (byte)value;
                            ippStat = IppImaging64.ippiCopyConstBorder_8u_C1R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, valueInByte);
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] byValues = new byte[] { (byte)value, (byte)value, (byte)value };
                            ippStat = IppImaging64.ippiCopyConstBorder_8u_C3R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, byValues);
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] byValues8uC4 = new byte[] { (byte)value, (byte)value, (byte)value, (byte)value };
                            ippStat = IppImaging64.ippiCopyConstBorder_8u_C4R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, byValues8uC4);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiCopyConstBorder_16u_C1R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, value);
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usValues = new ushort[] { value, value, value };
                            ippStat = IppImaging64.ippiCopyConstBorder_16u_C3R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, usValues);
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usValues16uC4 = new ushort[] { value, value, value, value };
                            ippStat = IppImaging64.ippiCopyConstBorder_16u_C4R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, usValues16uC4);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            byte valueInByte = (byte)value;
                            ippStat = IppImaging32.ippiCopyConstBorder_8u_C1R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, valueInByte);
                            break;
                        case PixelFormatType.P8u_C3:
                            byte[] byValues = new byte[] { (byte)value, (byte)value, (byte)value };
                            ippStat = IppImaging32.ippiCopyConstBorder_8u_C3R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, byValues);
                            break;
                        case PixelFormatType.P8u_C4:
                            byte[] byValues8uC4 = new byte[] { (byte)value, (byte)value, (byte)value, (byte)value };
                            ippStat = IppImaging32.ippiCopyConstBorder_8u_C4R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, byValues8uC4);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiCopyConstBorder_16u_C1R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, value);
                            break;
                        case PixelFormatType.P16u_C3:
                            ushort[] usValues = new ushort[] { value, value, value };
                            ippStat = IppImaging32.ippiCopyConstBorder_16u_C3R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, usValues);
                            break;
                        case PixelFormatType.P16u_C4:
                            ushort[] usValues16uC4 = new ushort[] { value, value, value, value };
                            ippStat = IppImaging32.ippiCopyConstBorder_16u_C4R(pSrc, srcStep, srcRoiSize, pDst, dstStep, dstRoiSize, topBorderHeight, leftBorderWidth, usValues16uC4);
                            break;
                    }
                }

                return ippStat;
            }

            //Computes the mean and standard deviation of image pixel values.
            //coi: Channel of interest (for color images only); can be 1, 2, or 3.
            public static unsafe IppStatus MeanStdDev(byte* pSrc, int srcStep, IppiSize roiSize, PixelFormatType pixelFormat, int coi, double* pMean, double* pStdDev)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiMean_StdDev_8u_C1R(pSrc, srcStep, roiSize, pMean, pStdDev);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiMean_StdDev_8u_C3CR(pSrc, srcStep, roiSize, coi, pMean, pStdDev);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiMean_StdDev_8u_C4CR(pSrc, srcStep, roiSize, coi, pMean, pStdDev);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiMean_StdDev_16u_C1R(pSrc, srcStep, roiSize, pMean, pStdDev);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiMean_StdDev_16u_C3CR(pSrc, srcStep, roiSize, coi, pMean, pStdDev);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiMean_StdDev_16u_C4CR(pSrc, srcStep, roiSize, coi, pMean, pStdDev);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiMean_StdDev_8u_C1R(pSrc, srcStep, roiSize, pMean, pStdDev);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiMean_StdDev_8u_C3CR(pSrc, srcStep, roiSize, coi, pMean, pStdDev);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiMean_StdDev_8u_C4CR(pSrc, srcStep, roiSize, coi, pMean, pStdDev);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiMean_StdDev_16u_C1R(pSrc, srcStep, roiSize, pMean, pStdDev);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiMean_StdDev_16u_C3CR(pSrc, srcStep, roiSize, coi, pMean, pStdDev);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiMean_StdDev_16u_C4CR(pSrc, srcStep, roiSize, coi, pMean, pStdDev);
                            break;
                    }
                }

                return ippStat;
            }

            public static unsafe IppStatus HistogramRange(byte* pSrc, int srcStep, PixelFormatType pixelFormat, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiHistogramRange_8u_C1R(pSrc, srcStep, roiSize, pHist[0], pLevels[0], nLevels[0]);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiHistogramRange_8u_C3R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiHistogramRange_8u_C4R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiHistogramRange_16u_C1R(pSrc, srcStep, roiSize, pHist[0], pLevels[0], nLevels[0]);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiHistogramRange_16u_C3R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiHistogramRange_16u_C4R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiHistogramRange_8u_C1R(pSrc, srcStep, roiSize, pHist[0], pLevels[0], nLevels[0]);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiHistogramRange_8u_C3R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiHistogramRange_8u_C4R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiHistogramRange_16u_C1R(pSrc, srcStep, roiSize, pHist[0], pLevels[0], nLevels[0]);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiHistogramRange_16u_C3R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiHistogramRange_16u_C4R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels);
                            break;
                    }
                }

                return ippStat;
            }


            // Computes the intensity histogram of an image using equal bins.
            // This function computes the intensity histogram of an image in the ranges
            // specified by the values lowerLevel (inclusive), upperLevel (exclusive), and nLevels.
            // The function operates on the assumption that all histogram bins have the same width
            // and equal boundary values of the bins (levels).
            public static unsafe IppStatus HistogramEven(byte* pSrc, int srcStep, PixelFormatType pixelFormat, IppiSize roiSize, int*[] pHist, int*[] pLevels, int[] nLevels, int[] lowerLevel, int[] upperLevel)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiHistogramEven_8u_C1R(pSrc, srcStep, roiSize, pHist[0], pLevels[0], nLevels[0], lowerLevel[0], upperLevel[0]);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiHistogramEven_8u_C3R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels, lowerLevel, upperLevel);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiHistogramEven_8u_C4R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels, lowerLevel, upperLevel);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging64.ippiHistogramEven_16u_C1R(pSrc, srcStep, roiSize, pHist[0], pLevels[0], nLevels[0], lowerLevel[0], upperLevel[0]);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging64.ippiHistogramEven_16u_C3R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels, lowerLevel, upperLevel);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging64.ippiHistogramEven_16u_C4R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels, lowerLevel, upperLevel);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiHistogramEven_8u_C1R(pSrc, srcStep, roiSize, pHist[0], pLevels[0], nLevels[0], lowerLevel[0], upperLevel[0]);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiHistogramEven_8u_C3R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels, lowerLevel, upperLevel);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiHistogramEven_8u_C4R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels, lowerLevel, upperLevel);
                            break;
                        case PixelFormatType.P16u_C1:
                            ippStat = IppImaging32.ippiHistogramEven_16u_C1R(pSrc, srcStep, roiSize, pHist[0], pLevels[0], nLevels[0], lowerLevel[0], upperLevel[0]);
                            break;
                        case PixelFormatType.P16u_C3:
                            ippStat = IppImaging32.ippiHistogramEven_16u_C3R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels, lowerLevel, upperLevel);
                            break;
                        case PixelFormatType.P16u_C4:
                            ippStat = IppImaging32.ippiHistogramEven_16u_C4R(pSrc, srcStep, roiSize, pHist, pLevels, nLevels, lowerLevel, upperLevel);
                            break;
                    }
                }

                return ippStat;
            }

            public static unsafe IppStatus Convert8bppTo16bpp(byte* pSrc, int srcStep, PixelFormatType pixelFormat, byte* pDst, int dstStep, IppiSize roiSize)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging64.ippiConvert_8u16u_C1R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging64.ippiConvert_8u16u_C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging64.ippiConvert_8u16u_C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                    }
                }
                else
                {
                    switch (pixelFormat)
                    {
                        case PixelFormatType.P8u_C1:
                            ippStat = IppImaging32.ippiConvert_8u16u_C1R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P8u_C3:
                            ippStat = IppImaging32.ippiConvert_8u16u_C3R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                        case PixelFormatType.P8u_C4:
                            ippStat = IppImaging32.ippiConvert_8u16u_C4R(pSrc, srcStep, pDst, dstStep, roiSize);
                            break;
                    }
                }

                return ippStat;
            }

            public static unsafe IppStatus Convert_16u32f_C1R(byte* pSrc, int srcStep, float* pDst, int dstStep, IppiSize roiSize)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;

                if (_Is64bit)
                {
                    ippStat = IppImaging64.ippiConvert_16u32f_C1R(pSrc, srcStep, pDst, dstStep, roiSize);
                }
                else
                {
                    ippStat = IppImaging32.ippiConvert_16u32f_C1R(pSrc, srcStep, pDst, dstStep, roiSize);
                }

                return ippStat;
            }

            public static unsafe IppStatus Convert_32f16u_C1R(float* pSrc, int srcStep, byte* pDst, int dstStep, int width, int height)
            {
                IppStatus ippStat = IppStatus.ippStsNoErr;
                IppRoundMode roundMode = IppRoundMode.ippRndNear;
                IppiSize roiSize = new IppiSize(width, height);

                if (_Is64bit)
                {
                    ippStat = IppImaging64.ippiConvert_32f16u_C1R(pSrc, srcStep, pDst, dstStep, roiSize, roundMode);
                }
                else
                {
                    ippStat = IppImaging32.ippiConvert_32f16u_C1R(pSrc, srcStep, pDst, dstStep, roiSize, roundMode);
                }

                return ippStat;
            }
        }


        public static class IppLoader
        {
            [DllImport("kernel32", SetLastError = true)]
            private static extern IntPtr LoadLibrary(string fileName);

            /// <summary>
            /// Preloads a DLL from either 32 or 64
            /// </summary>
            /// <param name="dllName">Name of the DLL without a path</param>
            /// <remarks></remarks>
            public static void PreloadDll(string dllName)
            {
                // Find the default install, we could do this with the resistery 
                //const string basePath = @"C:\Program Files (x86)\Intel\ComposerXE-2011\redist";
                string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string dllPath;
                string libPath1;
                string libPath2;

                // Find if we are running under 32 or 64 bit code
                if (IntPtr.Size == 8)
                {
                    dllPath = Path.Combine(basePath, @"Ipp\em64t", dllName);
                    libPath1 = Path.Combine(basePath, @"Ipp\em64t\libiomp5md.dll");
                    libPath2 = Path.Combine(basePath, @"Ipp\em64t\ippcoreem64t-6.0.dll");
                }
                else
                {
                    dllPath = Path.Combine(basePath, @"Ipp\ia32", dllName);
                    libPath1 = Path.Combine(basePath, @"Ipp\ia32\libiomp5md.dll");
                    libPath2 = Path.Combine(basePath, @"Ipp\ia32\ippcore-6.0.dll");
                }

                // We could optimize the loads here so the lib is not tried many times
                if (File.Exists(libPath1))
                {
                    LoadLibrary(libPath1);
                }

                // We could optimize the loads here so the lib is not tried many times
                if (File.Exists(libPath2))
                {
                    LoadLibrary(libPath2);
                }

                // We could optimize the loads here so the lib is not tried many times
                if (File.Exists(dllPath))
                {
                    LoadLibrary(dllPath);
                }
            }

        }

    }
}

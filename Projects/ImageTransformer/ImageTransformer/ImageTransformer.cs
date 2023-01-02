using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Sequlite.Image.Processing
{
    public struct ImageTransformParameters
    {
        public int WidthAdjust { get; set; } //< amount to scale the image on the x-axis
        public int HeightAdjust { get; set; } //< amount to scale the image on the y-axis
        public int XOffset { get; set; } //< amount to translate the image on the x-axis
        public int YOffset { get; set; } //< amount to translate the image on the y-axis
        public Int32Rect CropRectangle { get; set; } //< rectangle used to crop images after scaling and translation
    }

    ////////////////////////////////////////////////////////////////
    // Image Transformer
    //
    // This class contains methods that calculate and apply affine
    // transformations used for image alignment.
    //
    // This class is a singleton class
    //
    // Version: 0.2
    // Author: Erik Werner
    // Copyright: 2022 Sequlite Genomics, all rights reserved
    ////////////////////////////////////////////////////////////////
    public class ImageTransformer
    {
        private static ImageTransformer _Transformer = null; //< The instance of ImageTransformer
        private static object _instanceCreationLocker = new object(); //< used for locking during instantiation

        // Used to lookup final computed image scaling and crop values from rectangles. Key = channel name
        private Dictionary<string, ImageTransformParameters> _ImageTransforms;

        private int _frameWidth; // < The number of columns in the input image. Set during initialization
        private int _frameHeight; //< The number of rows in the input image. Set during initialization

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static ImageTransformer GetImageTransformer()
        {
            if (_Transformer == null)
            {
                lock (_instanceCreationLocker)
                {
                    if (_Transformer == null)
                    {
                        _Transformer = new ImageTransformer();
                    }
                }
            }
            return _Transformer;
        }

        /// <summary>
        /// The dictionary is initialized in the private constructor.
        /// </summary>
        private ImageTransformer()
        {
            _ImageTransforms = new Dictionary<string, ImageTransformParameters>();
        }

        /// <summary>
        /// Computes crop rectangles for each image channel from initial config parameters (scale and translate)
        /// Rectangle Width and Height are used for scale operations. 
        /// Rectangle X and Y coordinates are used to crop the scaled images.
        /// Final values are stored in the dictionary _ImageTransforms.
        /// The dictionary is reset each time initialize is called.
        /// </summary>
        /// <param name="frameWidth">The width of the images to be processed (units = [px])</param>
        /// <param name="frameHeight">The height of the images to be processed (units = [px])</param>
        /// <param name="parameters">A dictionary of structures containing transform parameters. 
        /// The key stays with each parameter set through initialization and is used to look up computed transform parameters</param>
        /// <returns>A rectangle representing the intersection of all transformed images. If no intersection is found, this method
        /// returns an empty rectangle.</returns>
        public Int32Rect Initialize(in int frameWidth, in int frameHeight, Dictionary<string, ImageTransformParameters> parameters)
        {
            Int32Rect sucessRect = new Int32Rect(); // the intersection rectangle
            try
            {
                _frameWidth = frameWidth;
                _frameHeight = frameHeight;
                // generate transformed rectangles for the image of each channel
                Dictionary<string, Int32Rect> transformedRects = new Dictionary<string, Int32Rect>();
                foreach (string channel in parameters.Keys)
                {
                    ImageTransformParameters p = parameters[channel];
                    Int32Rect transformedRect = ScaleAndTranslate(new Int32Rect(0, 0, frameWidth, frameHeight), p.WidthAdjust, p.HeightAdjust, p.XOffset, p.YOffset);
                    transformedRects.Add(channel, transformedRect);
                }

                // find the intersection of all transformed rectangles
                Int32Rect intersection = Intersection(transformedRects.Values.ToList());
                if (intersection.IsEmpty)
                {
                    // error, no intersection could be found
                    string msg = $"Error: Invalid Transform parameters. No intersecting rect was found.";
                    throw new ArgumentOutOfRangeException(msg);
                }

                // convert coordinates of intersection rect to crop original rects
                Dictionary<string, Int32Rect> cropRects = CalcCropRects(intersection, transformedRects);

                // initialize data structure with final transform values
                _ImageTransforms.Clear();
                foreach (string channel in cropRects.Keys)
                {
                    ImageTransformParameters p = parameters[channel];
                    p.CropRectangle = cropRects[channel];
                    _ImageTransforms.Add(channel, p);
                }
                return intersection;
            }
            catch (ArgumentOutOfRangeException)
            {
                // TODO: display a message for the user
            }
            return sucessRect;
        }

        /// <summary>
        /// Searches an image file for channel information (G1, G2, R3, R4)
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>A string with the channel information (G1, G2, R3, R4).
        /// If no channel information is found, returns an empty string.</returns>
        private static string FindImageChannel(string fileName)
        {
            // pattern to extract the channel name (G1, G2, R3, R4)
            const string _pattern = @"_(Inc(\d+)_(?<channel>(G1|G2|R3|R4))_(b|t)L(1|2|3|4)(\d{2})(A|B|C|D))_.*\.tif";
            Match m = Regex.Match(fileName, _pattern);
            string channel = "";
            if (m.Success && m.Groups["channel"].Success)
            {
                channel = m.Groups["channel"].Value;
            }
            return channel;
        }

        /// <summary>
        /// Used to get a set of image transform parameters from a dictionary.
        /// Parameter sets are keyed by image channel name.
        /// The channel name can be extracted from the file name using FindImageChannel(fileName)
        /// </summary>
        /// <param name="key">The channel name</param>
        /// <returns></returns>
        public ImageTransformParameters LookupParameters(in string key)
        {
            return _ImageTransforms[key];
        }

        /// <summary>
        /// First scales and then translates a rectangle.
        /// </summary>
        /// <param name="rect">The input rectangle to transform</param>
        /// <param name="xScale">Amount to scale the rectangle on the x-axis. Negative values shrink the rectangle. Units = [px]</param>
        /// <param name="yScale">Amount to scale the rectangle on the y-axis. Negative values shrink the rectangle. Units = [px]</param>
        /// <param name="xShift">Amount to translate the rectangle on the x-axis. Negative values move the rectangle "up". Units = [px]</param>
        /// <param name="yShift">Amount to translate the rectangle on the y-axis. Negative values move the rectangle "left". Units = [px]</param>
        /// <returns>The transformed rectangle in the coordinate system of the input rectangle. 
        /// (ex: if the input rect starts at (2,3) and the rect is shifted 4 px right and 5 px down, the output rectangle will start at (6,8)</returns>
        private static Int32Rect ScaleAndTranslate(in Int32Rect rect, in int xScale, in int yScale, in int xShift, in int yShift)
        {
            int left = rect.X - (xScale / 2) + xShift;
            int top = rect.Y - (yScale / 2) + yShift;
            int width = rect.Width + xScale;
            int height = rect.Height + yScale;

            return new Int32Rect(left, top, width, height);
        }

        /// <summary>
        /// Finds the intersection of a set of rectangles.
        /// </summary>
        /// <param name="rects">A set of rectangles to search for an intersection</param>
        /// <returns>A rectangle that intersects all rectangles of the input set.
        /// If no intersection is found, an empty rectangle is returned. </returns>
        private static Int32Rect Intersection(in List<Int32Rect> rects)
        {
            // note: could also apply Rectangle.Intersect() to each rect
            int left = rects.Max(a => a.X);
            int top = rects.Max(a => a.Y);
            int right = rects.Min(a => a.X + a.Width);
            int bottom = rects.Min(a => a.Y + a.Height);

            if (left < right && top < bottom)
            {
                return new Int32Rect(left, top, right - left, bottom - top);
            }
            else
            {
                return new Int32Rect(); // no intersection
            }
        }

        /// <summary>
        /// Calculates the crop rectangle for each transformed rectangle using the intersection of all transformed rectangles.
        /// </summary>
        /// <param name="intersection"> The rectangle that overlaps all input rectangles in the coordinate system of the source images</param>
        /// <param name="transformedRects">A set of transformed rectangles in the coordinate system of the source images</param>
        /// <returns></returns>
        private static Dictionary<string, Int32Rect> CalcCropRects(in Int32Rect intersection, in Dictionary<string, Int32Rect> transformedRects)
        {
            Dictionary<string, Int32Rect> results = new Dictionary<string, Int32Rect>();
            try
            {
                foreach (string channel in transformedRects.Keys)
                {
                    Int32Rect transformedRect = transformedRects[channel];
                    // change coordinate system of intersection rect to coordinate system of transformed rect
                    int left = intersection.X - transformedRect.X;
                    int top = intersection.Y - transformedRect.Y;
                    Int32Rect cropRect = new Int32Rect(left, top, intersection.Width, intersection.Height);

                    // create a transformed rect with origin of 0,0 for comparison
                    Int32Rect referenceRect = new Int32Rect(0, 0, transformedRect.Width, transformedRect.Height);

                    // check that the crop rect is inside the target image rect
                    //if (referenceRect.Contains(cropRect))
                    if (referenceRect.X <= cropRect.X &&
                        referenceRect.X + referenceRect.Width >= cropRect.X + cropRect.Width &&
                        referenceRect.Y <= cropRect.Y &&
                        referenceRect.Y + referenceRect.Height >= cropRect.Y + cropRect.Height)
                    {
                        results.Add(channel, cropRect);
                    }
                    else
                    {
                        // error, crop rect is out of bounds
                        // error, no intersection could be found
                        string msg = $"Error, crop rect OOB. Rect:{transformedRect} CropRect:{cropRect}";
                        throw new ArgumentOutOfRangeException(msg);
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // TODO: display an error message for the user
            }

            return results;
        }

        /// <summary>
        /// Transforms an input image by applying a resize operation followed by a crop operation.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="parameters"></param>
        public static void Transform(in WriteableBitmap input, out WriteableBitmap output, in ImageTransformParameters parameters)
        {
            int adjustedWidth = input.PixelWidth + parameters.WidthAdjust;
            int adjustedHeight = input.PixelHeight + parameters.HeightAdjust;
            WriteableBitmap temp;
            Resize(input, out temp, adjustedWidth, adjustedHeight);
            Crop(temp, out output, parameters.CropRectangle);
        }

        /// <summary>
        /// Transforms an input operation by resizing and cropping. The operations are combined for improved speed.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="parameters"></param>
        public static unsafe void FastTransform(in WriteableBitmap input, out WriteableBitmap output, in ImageTransformParameters parameters)
        {
            int widthInput = input.PixelWidth;
            int heightInput = input.PixelHeight;
            int scaledWidth = widthInput + parameters.WidthAdjust;
            int scaledHeight = heightInput + parameters.HeightAdjust;

            // compute parameters for nagivating image data array
            const int bytesPerPixel = 2;
            const int bitsPerPixel = bytesPerPixel * 8;

            float xScale = (float)widthInput / scaledWidth; // calculate x and y scale factors
            float yScale = (float)heightInput / scaledHeight;

            // variables for indexing
            int scaledX, scaledY;
            float srcX, srcY;
            int x0, y0;

            // get crop rect coordinates
            int cropX = parameters.CropRectangle.X;
            int cropY = parameters.CropRectangle.Y;
            int widthOutput = parameters.CropRectangle.Width;
            int heightOutput = parameters.CropRectangle.Height;

            // wpf aligns rows to 32-bit boundaries
            int strideIn = ((widthInput * bitsPerPixel + 31) & ~31) >> 3;
            int strideOut = ((widthOutput * bitsPerPixel + 31) & ~31) >> 3;

            output = new WriteableBitmap(widthOutput, heightOutput, input.DpiX, input.DpiY, input.Format, input.Palette);

            byte* outPtr = (byte*)output.BackBuffer.ToPointer();
            byte* inPtr = (byte*)input.BackBuffer.ToPointer();

            //scale and crop each line of the destination bitmap
            for (int row = 0; row < heightOutput; row++)
            {
                for (int col = 0; col < widthOutput; col++)
                {
                    // cropping calculation comes first from destinati`on POV
                    // map final x and y coordinates back to uncropped, scaled image
                    scaledX = col + cropX;
                    scaledY = row + cropY;

                    // next calculate the source position from the POV of the scaled image
                    // map x and y coordinates to source bitmap
                    srcX = scaledX * xScale;
                    srcY = scaledY * yScale;

                    // cast to an integer (nearest neighbor interpolation)
                    x0 = (int)srcX;
                    y0 = (int)srcY;

                    // use source x and y to index into byte array
                    int srcIdx = (y0 * strideIn) + (x0 * bytesPerPixel);

                    // copy both bytes of the 16-bit pixel
                    outPtr[row * strideOut + col * bytesPerPixel] = inPtr[srcIdx];
                    outPtr[row * strideOut + col * bytesPerPixel + 1] = inPtr[srcIdx + 1];
                }
            }
        }

        /// <summary>
        /// Transforms an input operation by resizing and cropping. The operations are combined for improved speed.
        /// This method uses the file name to look up transform parameters.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="fileName"></param>
        public unsafe void FastTransform(in byte[] input, out byte[] output, in string fileName)
        {
            string channel = FindImageChannel(fileName);
            ImageTransformParameters parameters = LookupParameters(channel);
            FastTransform(input, out output, _frameWidth, _frameHeight, parameters);
        }

        /// <summary>
        /// Transforms an input operation by resizing and cropping. The operations are combined for improved speed.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="widthInput"></param>
        /// <param name="heightInput"></param>
        /// <param name="parameters"></param>
        public static unsafe void FastTransform(in byte[] input, out byte[] output, in int widthInput, in int heightInput, in ImageTransformParameters parameters)
        {
            int scaledWidth = widthInput + parameters.WidthAdjust;
            int scaledHeight = heightInput + parameters.HeightAdjust;

            // compute parameters for nagivating image data array
            const int bytesPerPixel = 2;
            const int bitsPerPixel = bytesPerPixel * 8;

            float xScale = (float)widthInput / scaledWidth; // calculate x and y scale factors
            float yScale = (float)heightInput / scaledHeight;

            // variables for indexing
            int scaledX, scaledY;
            float srcX, srcY;
            int x0, y0;

            // get crop rect coordinates
            int cropX = parameters.CropRectangle.X;
            int cropY = parameters.CropRectangle.Y;
            int widthOutput = parameters.CropRectangle.Width;
            int heightOutput = parameters.CropRectangle.Height;

            // wpf aligns rows to 32-bit boundaries
            int strideIn = ((widthInput * bitsPerPixel + 31) & ~31) >> 3;
            int strideOut = ((widthOutput * bitsPerPixel + 31) & ~31) >> 3;


            // initialize storage for output
            output = new byte[heightOutput * strideOut];

            //scale and crop each line of the destination bitmap
            for (int row = 0; row < heightOutput; row++)
            {
                for (int col = 0; col < widthOutput; col++)
                {
                    // cropping calculation comes first from destinati`on POV
                    // map final x and y coordinates back to uncropped, scaled image
                    scaledX = col + cropX;
                    scaledY = row + cropY;

                    // next calculate the source position from the POV of the scaled image
                    // map x and y coordinates to source bitmap
                    srcX = scaledX * xScale;
                    srcY = scaledY * yScale;

                    // cast to an integer (nearest neighbor interpolation)
                    x0 = (int)srcX;
                    y0 = (int)srcY;

                    // use source x and y to index into byte array
                    int srcIdx = (y0 * strideIn) + (x0 * bytesPerPixel);

                    // copy both bytes of the 16-bit pixel
                    output[row * strideOut + col * bytesPerPixel] = input[srcIdx];
                    output[row * strideOut + col * bytesPerPixel + 1] = input[srcIdx + 1];
                }
            }
        }
        public static unsafe void Resize(in WriteableBitmap input, out WriteableBitmap output, in int width, in int height)
        {
            // gather parameters
            int widthIn = input.PixelWidth;
            int heightIn = input.PixelHeight;
            int bitsPerPixel = input.Format.BitsPerPixel;
            int bytesPerPixel = bitsPerPixel / 8;

            // wpf aligns rows to 32-bit boundaries
            int strideIn = ((widthIn * bitsPerPixel + 31) & ~31) >> 3;
            int strideOut = ((width * bitsPerPixel + 31) & ~31) >> 3;

            // calculate x and y scale factors
            var xs = (float)widthIn / width;
            var ys = (float)heightIn / height;

            // variables for scaling and indexing
            float sx, sy;
            int x0, y0;

            output = new WriteableBitmap(width, height, input.DpiX, input.DpiY, input.Format, input.Palette);

            // set pointers to the start of input and output image data
            byte* ptrIn = (byte*)input.BackBuffer.ToPointer();
            byte* ptrOut = (byte*)output.BackBuffer.ToPointer();

            // process each line of the destination bitmap
            for (int row = 0; row < height; row++)
            {
                // point to the beginning of a row in the output bitmap
                byte* lineOut = ptrOut + (row * strideOut);

                // process each pixel of each row of the destination bitmap
                for (int col = 0; col < width; col++)
                {
                    // map x and y to pixel coordinates to source bitmap
                    sx = col * xs; // index of the pixel. needs to point to the starting byte of a pixel
                    sy = row * ys; // index of the row. will always be the start of a pixel

                    // cast to an integer (nearest neighbor interpolation)
                    x0 = (int)sx;
                    y0 = (int)sy;

                    //int srcIdx = y0 * widthSource + x0;
                    // index into source using the source stride
                    int srcIdx = (y0 * strideIn) + (x0 * bytesPerPixel);

                    // set both the low and high bytes of the destination bitmap
                    lineOut[col * bytesPerPixel] = ptrIn[srcIdx];
                    lineOut[(col * bytesPerPixel) + 1] = ptrIn[srcIdx + 1];
                }
            }
        }

        public static unsafe void Crop(in WriteableBitmap input, out WriteableBitmap output, Int32Rect cropRect)
        {
            int widthIn = input.PixelWidth;
            int heightIn = input.PixelHeight;
            int x = cropRect.X;
            int y = cropRect.Y;
            int width = cropRect.Width;
            int height = cropRect.Height;

            // If the rectangle is completely out of the bitmap
            if (x > widthIn || y > heightIn)
            {
                output = new WriteableBitmap(0, 0, input.DpiX, input.DpiY, input.Format, null);
                Debug.WriteLine("Warning: Crop rect OOB");
            }
            output = new WriteableBitmap(width, height, input.DpiX, input.DpiY, input.Format, null);

            byte* srcPtr = (byte*)input.BackBuffer.ToPointer();
            byte* dstPtr = (byte*)output.BackBuffer.ToPointer();

            // Clamp to boundaries
            if (x < 0) x = 0;
            if (x + width > widthIn) width = widthIn - x;
            if (y < 0) y = 0;
            if (y + height > heightIn) height = heightIn - y;

            int bitsPerPixel = input.Format.BitsPerPixel;
            int bytesPerPixel = bitsPerPixel / 8;

            int widthBytes = width * bytesPerPixel;

            // wpf aligns rows to 32-bit boundaries
            int strideIn = ((widthIn * bitsPerPixel + 31) & ~31) >> 3;
            int strideOut = ((width * bitsPerPixel + 31) & ~31) >> 3;

            // align to 8-bit boundary (not used for wpf)
            //int strideInput = widthIn * ((bitsPerPixel + 7) / 8); // bytes per line
            //int strideOutput = width * ((bitsPerPixel + 7) / 8); // bytes per line

            // process each line of the destination bitmap
            for (int row = 0; row < height; row++)
            {
                // point to the beginning of a row in the output bitmap
                byte* currentLine = dstPtr + (row * strideOut);
                for (int dstCol = 0; dstCol < width; dstCol++)
                {
                    int srcOff = ((y + row) * strideIn) + ((x + dstCol) * bytesPerPixel);

                    currentLine[dstCol * bytesPerPixel] = srcPtr[srcOff];
                    currentLine[(dstCol * bytesPerPixel) + 1] = srcPtr[srcOff + 1];
                }
            }
        }
    }
}

using System.Drawing;
using System.Windows.Media.Imaging;


namespace Sequlite.Image.Processing
{
    public interface IImageStatistics
    {
        double GetTotalSum(WriteableBitmap image, Rectangle roi);

        double GetAverage(WriteableBitmap image);

        double GetStdDeviation(WriteableBitmap image, Rectangle roi);

        double GetMedian(WriteableBitmap image, Rectangle roi);

        int GetPixelMin(WriteableBitmap image, Rectangle rectROI);

        int GetPixelMax(WriteableBitmap image, Rectangle roi);
    }
}

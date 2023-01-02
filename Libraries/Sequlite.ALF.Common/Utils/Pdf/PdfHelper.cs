using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public sealed class PdfHelper
    {
        private PdfHelper()
        {
        }

        public static PdfHelper Instance { get; } = new PdfHelper();
        public PdfDocument StartSaving()
        {
            return new PdfDocument();
        }

        public void EndSaving(PdfDocument pdf, string fileName)
        {
            pdf.Save(fileName);
            pdf.Dispose();
        }
        public void AddImageToPdf(PdfDocument document, string imageFileName, 
            int x, int y, int width, int height, bool deleteImage )
        {
            PdfPage page = document.AddPage();
            using (XImage img = XImage.FromFile(imageFileName))
            {
                if (height <= 0)
                {
                    // Calculate new height to keep image ratio
                    height = (int)(((double)width / (double)img.PixelWidth) * img.PixelHeight);
                }
                XGraphics gfx = XGraphics.FromPdfPage(page);
                //x = 10; y =10; for example
                gfx.DrawImage(img, x, y, width, height);
                gfx.Dispose();
            }
            if (deleteImage)
            {
                File.Delete(imageFileName);
            }
        }
    }
}

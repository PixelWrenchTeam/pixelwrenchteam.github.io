using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PixelWrench
{
    public partial class MainWindow
    {
        private void DrawWatermark(DrawingContext dc, double worldPixelWidth, double worldPixelHeight, string watermarkPath)
        {
            try
            {
                using var watermarkImg = new Bitmap(watermarkPath);

                double scale = 0.5;
                double imgWidth = watermarkImg.PixelSize.Width * scale;
                double imgHeight = watermarkImg.PixelSize.Height * scale;

                double paddingX = TILE_SIZE * 1.0;
                double paddingY = TILE_SIZE * -2.25; 

                double xPos = worldPixelWidth - imgWidth - paddingX;
                double yPos = worldPixelHeight - imgHeight - paddingY;

                using (dc.PushOpacity(0.6))
                {
                    dc.DrawImage(watermarkImg, new Rect(xPos, yPos, imgWidth, imgHeight));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Watermark rendering failed: " + ex.Message);
            }
        }
    }
}
namespace SharpEquations.WinForms
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using SkiaSharp;

    /// <summary>
    /// The pixels a control draws into with Skia, in its size in pixels, copied to its window as they are: Skia draws
    /// everything, and Windows Forms only puts the bitmap on the screen.
    /// </summary>
    internal sealed class MathSurface : IDisposable
    {
        private SKBitmap? bitmap;
        private Bitmap? image;

        /// <summary>A canvas over the pixels, of a size in pixels, cleared with a colour and scaled from device-independent pixels.</summary>
        public SKCanvas Begin(Size size, SKColor background, float scale)
        {
            var width = Math.Max(1, size.Width);
            var height = Math.Max(1, size.Height);
            if (bitmap == null || bitmap.Width != width || bitmap.Height != height)
            {
                Release();
                bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                image = new Bitmap(width, height, bitmap.RowBytes, PixelFormat.Format32bppPArgb, bitmap.GetPixels());
            }
            var canvas = new SKCanvas(bitmap);
            canvas.Clear(background);
            canvas.Scale(scale);
            return canvas;
        }

        /// <summary>Puts the pixels drawn on the graphics of the window.</summary>
        public void Present(Graphics graphics)
        {
            if (image != null)
                graphics.DrawImageUnscaled(image, 0, 0);
        }

        /// <summary>The pixels drawn, as bytes in the order blue, green, red, alpha, premultiplied (for the tests).</summary>
        internal SKBitmap? Pixels => bitmap;

        public void Dispose() => Release();

        private void Release()
        {
            image?.Dispose();
            image = null;
            bitmap?.Dispose();
            bitmap = null;
        }
    }
}

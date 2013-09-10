using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Image2D = System.Drawing.Image;
using System.IO;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace XOGroup.Image.IO
{
    public enum Position
    {
        Left,
        LeftTop,
        Top,
        RightTop,
        Right,
        RightBottom,
        Bottom,
        LeftBottom,
        Center
    }

    class Margin
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    class ImageProcessor
    {
        /// <summary>
        /// Crop image with given size.
        /// </summary>
        /// <param name="imageFile">The image file that will be cropped</param>
        /// <param name="margin">The image edge will be cropped</param>
        /// <returns>Image stream.</returns>
        internal static Stream Crop(string imageFile, Margin margin)
        {
            Stream stream;

            using (Image2D image = Image2D.FromFile(imageFile))
            {
                stream = Crop(image, margin);
            }

            return stream;
        }

        /// <summary>
        /// Crop image with given size.
        /// </summary>
        /// <param name="image">Image object that will be cropped</param>
        /// <param name="margin">The image edge will be cropped</param>
        /// <returns>Image stream.</returns>
        internal static Stream Crop(Image2D image, Margin margin)
        {
            #region Crop Images

            Size canvasSize = new Size(image.Width - margin.Left - margin.Right, image.Height - margin.Top - margin.Bottom);
            Rectangle rectDest = new Rectangle(0, 0, canvasSize.Width, canvasSize.Height);
            Rectangle rectSrc = new Rectangle(margin.Left, margin.Top, canvasSize.Width, canvasSize.Height);

            Stream bitmap = CreateImageStream(image, canvasSize, rectDest, rectSrc);

            #endregion

            return bitmap;
        }

        /// <summary>
        /// Resize image with given size.
        /// </summary>
        /// <param name="imageFile">The image file that will be resized</param>
        /// <param name="sizeTo">The new image size</param>
        internal static Stream Resize(string imageFile, Size sizeTo)
        {
            Stream stream;

            using (Image2D image = Image2D.FromFile(imageFile))
            {
                stream = Resize(image, sizeTo);
            }

            return stream;
        }

        /// <summary>
        /// Resize image with given size.
        /// </summary>
        /// <param name="image">Image object that will be resize</param>
        /// <param name="sizeTo">The new image size</param>
        internal static Stream Resize(Image2D image, Size sizeTo)
        {
            bool isCrop = false;
            int width = sizeTo.Width;
            int height = sizeTo.Height;
            Stream bitmap;

            #region Resize Images

            // detect crop
            if (sizeTo.Height != int.MinValue)
            {
                isCrop = Math.Abs((double)image.Width / image.Height - (double)width / height) > 0.00001;
            }

            Size size = new Size();
            if (isCrop)
            {
                if ((double)image.Width / image.Height > (double)width / height)
                {
                    size.Height = height;
                    size.Width = (height * image.Width) / image.Height;
                }
                else
                {
                    size.Width = width;
                    size.Height = (width * image.Height) / image.Width;
                }

                // calculate crop margin
                Margin margin = new Margin()
                {
                    Left = (image.Width - size.Width) / 2,
                    Right = (image.Width - size.Width) / 2,
                    Top = (image.Height - size.Height) / 2,
                    Bottom = (image.Height - size.Height) / 2
                };

                Stream imageStream = Crop(image, margin);
                image = Image2D.FromStream(imageStream);
            }

            // resize image
            Rectangle rectDest = new Rectangle(0, 0, width, height);
            Rectangle rectSrc = new Rectangle(0, 0, image.Width, image.Height);
            bitmap = CreateImageStream(image, sizeTo, rectDest, rectSrc);

            #endregion

            return bitmap;
        }

        /// <summary>
        /// Set watermark on an image.
        /// </summary>
        /// <param name="imageStream">Stream of an image file</param>
        /// <param name="position">Watermark position</param>
        /// <returns>Image stream.</returns>
        internal static Stream SetWaterMark(Stream imageStream, Position position, Margin margin, Stream watermark)
        {
            MemoryStream stream = new MemoryStream();
            float transparent = 0.5f;
            
            #region Set Watermark

            using (Image2D image = Image2D.FromStream(imageStream))
            {
                using (Image2D wmLoad = Image2D.FromStream(watermark))
                {
                    // resize watermark image to 2/3 size (width) of image
                    Size size = new Size(image.Width / 3 * 2, (int)((double)image.Width / 3 * 2 / wmLoad.Width * wmLoad.Height));

                    using (Image2D wm = Image2D.FromStream(Resize(wmLoad, size)))
                    {
                        using (Graphics g = Graphics.FromImage(image))
                        {
                            // set transparency
                            ImageAttributes imgAttributes = new ImageAttributes();
                            ColorMap colorMap = new ColorMap();
                            colorMap.OldColor = Color.FromArgb(255, 0, 255, 0);
                            colorMap.NewColor = Color.FromArgb(0, 0, 0, 0);
                            ColorMap[] remapTable = { colorMap };
                            imgAttributes.SetRemapTable(remapTable, ColorAdjustType.Bitmap);

                            float[][] colorMatrixElements = { 
                            new float[] {1.0f,  0.0f,  0.0f,  0.0f, 0.0f},
                            new float[] {0.0f,  1.0f,  0.0f,  0.0f, 0.0f},
                            new float[] {0.0f,  0.0f,  1.0f,  0.0f, 0.0f},
                            new float[] {0.0f,  0.0f,  0.0f,  transparent, 0.0f},// set transparency to 0.5
                            new float[] {0.0f,  0.0f,  0.0f,  0.0f, 1.0f}
                        };

                            PointF point = GetWaterMarkPosition(position, wm.Size, image.Size, margin);

                            ColorMatrix wmColorMatrix = new ColorMatrix(colorMatrixElements);
                            imgAttributes.SetColorMatrix(wmColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(wm, new Rectangle((int)point.X, (int)point.Y, wm.Width, wm.Height), 0, 0, wm.Width, wm.Height, GraphicsUnit.Pixel, imgAttributes);
                        }
                    }
                }

                image.Save(stream, image.RawFormat);
            }

            #endregion

            return stream;
        }

        /// <summary>
        /// Convert text to watermark image (PNG format).
        /// </summary>
        /// <param name="font">Text font</param>
        /// <param name="color">Text color</param>
        /// <param name="text">Content of text</param>
        /// <returns>Image stream.</returns>
        internal static Stream ConvertTextToImage(Font font, Color color, string text)
        {
            MemoryStream stream = new MemoryStream();
            Size size;

            using (Graphics g = Graphics.FromImage(new Bitmap((int)font.Size * text.Length, (int)font.Size)))
            {
                size = g.MeasureString(text, font).ToSize();
            }

            using (Image2D image = new Bitmap(size.Width, size.Height))
            {
                using (Graphics g = Graphics.FromImage(image))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.Transparent);
                    g.DrawString(text, font, new SolidBrush(color), 0, 0);
                }

                image.Save(stream, ImageFormat.Png);
            }

            return stream;
        }

        private static Stream CreateImageStream(Image2D srcImage, Size canvasSize, Rectangle rectDest, Rectangle rectSrc)
        {
            return CreateImageStream(srcImage, canvasSize, rectDest, rectSrc, null);
        }

        private static Stream CreateImageStream(Image2D srcImage, Size canvasSize, Rectangle rectDest, Rectangle rectSrc, ImageAttributes imageAttr)
        {
            MemoryStream stream = new MemoryStream();

            using (Image2D bitmap = new Bitmap(canvasSize.Width, canvasSize.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.White);
                    if (imageAttr == null)
                    {
                        g.DrawImage(srcImage, rectDest, rectSrc, GraphicsUnit.Pixel);
                    }
                    else
                    {
                        g.DrawImage(srcImage, rectDest, rectSrc.X, rectSrc.Y, rectSrc.Width, rectSrc.Height, GraphicsUnit.Pixel, imageAttr);
                    }
                }

                bitmap.Save(stream, srcImage.RawFormat);
            }

            return stream;
        }

        private static PointF GetWaterMarkPosition(Position position, Size wmSize, Size imageSize, Margin margin)
        {
            PointF point = new PointF();

            switch (position)
            {
                case Position.Left:
                    point.X = margin.Left;
                    point.Y = ((float)imageSize.Height - (float)wmSize.Height) / 2;
                    break;
                case Position.LeftTop:
                    point.X = margin.Left;
                    point.Y = margin.Top;
                    break;
                case Position.Top:
                    point.X = ((float)imageSize.Width - (float)wmSize.Width) / 2;
                    point.Y = margin.Top;
                    break;
                case Position.RightTop:
                    point.X = imageSize.Width - wmSize.Width - margin.Right;
                    point.Y = margin.Top;
                    break;
                case Position.Right:
                    point.X = imageSize.Width - wmSize.Width - margin.Right;
                    point.Y = ((float)imageSize.Height - (float)wmSize.Height) / 2;
                    break;
                case Position.Bottom:
                    point.X = ((float)imageSize.Width - (float)wmSize.Width) / 2;
                    point.Y = imageSize.Height - wmSize.Height - margin.Bottom;
                    break;
                case Position.LeftBottom:
                    point.X = margin.Left;
                    point.Y = imageSize.Height - wmSize.Height - margin.Bottom;
                    break;
                case Position.Center:
                    point.X = ((float)imageSize.Width - (float)wmSize.Width) / 2;
                    point.Y = ((float)imageSize.Height - (float)wmSize.Height) / 2;
                    break;
                case Position.RightBottom:
                default:
                    point.X = imageSize.Width - wmSize.Width - margin.Right;
                    point.Y = imageSize.Height - wmSize.Height - margin.Bottom;
                    break;
            }

            return point;
        }
    }
}

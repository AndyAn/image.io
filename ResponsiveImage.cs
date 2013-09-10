using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using Image2D = System.Drawing.Image;
using System.Text.RegularExpressions;

namespace XOGroup.Image.IO
{
    /// <summary>
    /// Responsive Image Generator
    /// </summary>
    public class ResponsiveImage
    {
        private static Regex regWH = new Regex("^(?<width>\\d+)x(?<height>\\d+)$", RegexOptions.IgnoreCase);
        private static Regex regW = new Regex("^(?<width>\\d+)$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Stream of the generated responsive image.
        /// </summary>
        public Stream BaseStream { get; private set; }

        /// <summary>
        /// Generated responsive image.
        /// </summary>
        public Image2D Image { get; private set; }

        /// <summary>
        /// Private constructorof this class.
        /// </summary>
        /// <param name="imagePath">Path of the image on server</param>
        /// <param name="userAgent">User agent string passed from browser</param>
        private ResponsiveImage(string imagePath, string userAgent)
        {
            BaseStream = null;
            Image = null;

            ProcessImage(imagePath, userAgent);
        }

        #region Static methods to get instant of the class

        /// <summary>
        /// Get the instant of ResponsiveImage class.
        /// </summary>
        /// <param name="imagePath">Path of the image on server</param>
        /// <returns></returns>
        public static ResponsiveImage Create(string imagePath)
        {
            return Create(imagePath, "");
        }

        /// <summary>
        /// Get the instant of ResponsiveImage class.
        /// </summary>
        /// <param name="imagePath">Path of the image on server</param>
        /// <param name="userAgent">User agent string passed from browser</param>
        /// <returns></returns>
        public static ResponsiveImage Create(string imagePath, string userAgent)
        {
            return new ResponsiveImage(imagePath, userAgent);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Process image.
        /// </summary>
        /// <param name="imagePath">Path of the image on server</param>
        /// <param name="userAgent">User agent string passed from browser</param>
        private void ProcessImage(string imagePath, string userAgent)
        {
            if (File.Exists(imagePath))
            {
                Size size = GetRequestSize(imagePath, userAgent);
                BaseStream = ImageProcessor.Resize(imagePath, size);
                if (ConfigHelper.GlobalSettings["watermark"])
                {
                    SetWaterMark();
                }
                Image = Image2D.FromStream(BaseStream);
            }
        }

        private void SetWaterMark()
        {
            //HttpApplication
        }

        /// <summary>
        /// Get the image size that requested from browser.
        /// <para>Device-detect module is not completed yet</para>
        /// </summary>
        /// <param name="imagePath">Path of the image on server</param>
        /// <param name="userAgent">User agent string passed from browser</param>
        /// <returns></returns>
        private Size GetRequestSize(string imagePath, string userAgent)
        {
            bool isDeviceDetect = true;
            Size size = Size.Empty;
            string fileName = Path.GetFileNameWithoutExtension(imagePath);

            int seperatorIndex = fileName.LastIndexOf("-");
            if (seperatorIndex > -1)
            {
                string arg = fileName.Substring(seperatorIndex);
                if (ConfigHelper.NamedImageSize.ContainsKey(arg))
                {
                    // Named Image Size
                    // e.g. http://www.ijie.com/image/image1-l.jpg
                    size = ConfigHelper.NamedImageSize[arg];
                    isDeviceDetect = false;
                }
                else
                {
                    // Customized Image Size
                    // e.g. http://www.ijie.com/image/image1-200x400.jpg
                    Match m = regWH.Match(arg); // width x height
                    if (m.Success)
                    {
                        size = new Size(int.Parse(m.Groups["width"].Value), int.Parse(m.Groups["height"].Value));
                        isDeviceDetect = false;
                    }
                    else
                    {
                        // e.g. http://www.ijie.com/image/image1-200.jpg
                        m = regW.Match(arg); // width
                        if (m.Success)
                        {
                            size = new Size(int.Parse(m.Groups["width"].Value), int.MinValue);
                            isDeviceDetect = false;
                        }
                    }
                }
            }

            if (isDeviceDetect)
            {
                // Device-Detected Image Size

                // pending to do device detect ....

                size = new Size(int.MinValue, int.MinValue);
            }

            return size;
        }

        #endregion
    }
}

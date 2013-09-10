using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;
using System.Drawing;
using Image2D = System.Drawing.Image;
using System.Net;
using System.Text.RegularExpressions;

namespace XOGroup.Image.IO
{
    public class ConfigHelper
    {
        private static object locker = new object();
        private static IDictionary<string, IDictionary<string, string>> config = null;

        static ConfigHelper()
        {
            config = ConfigurationManager.GetSection("imageRouting") as IDictionary<string, IDictionary<string, string>>;
        }

        #region Global Settings

        private static IDictionary<string, bool> settings = null;
        public static IDictionary<string, bool> GlobalSettings
        {
            get
            {
                lock (locker)
                {
                    if (settings == null)
                    {
                        settings = new Dictionary<string, bool>();
                        foreach (KeyValuePair<string, string> kv in config["settings"])
                        {
                            settings.Add(kv.Key, kv.Value.ToLower() == "on");
                        }
                    }
                }

                return settings;
            }
        }

        #endregion

        #region Supported image formats

        private static List<string> filterList = null;
        public static List<string> SupportedImageFormat
        {
            get
            {
                lock (locker)
                {
                    if (filterList == null)
                    {
                        filterList = new List<string>();
                        filterList.AddRange(config["format"].Values);
                    }
                }

                return filterList;
            }
        }

        #endregion

        #region Max-Age

        private static IDictionary<string, TimeSpan> maxAge = null;
        public static IDictionary<string, TimeSpan> MaxAge
        {
            get
            {
                lock (locker)
                {
                    if (maxAge == null)
                    {
                        maxAge = new Dictionary<string, TimeSpan>();
                        IDictionary<string, string> expiredTime = config["cacheTime"];
                        TimeSpan time = TimeSpan.Parse("7.00:00:00");

                        if (expiredTime.ContainsKey("all"))
                        {
                            TimeSpan.TryParse(expiredTime["all"], out time);
                            foreach (string ext in SupportedImageFormat)
                            {
                                maxAge.Add(ext.ToLower(), time);
                            }
                        }

                        foreach (string key in expiredTime.Keys)
                        {
                            time = TimeSpan.Parse("7.00:00:00");
                            TimeSpan.TryParse(expiredTime[key], out time);
                            if (key != "all")
                            {
                                if (maxAge.ContainsKey(key.ToLower()))
                                {
                                    maxAge[key.ToLower()] = time;
                                }
                                else
                                {
                                    maxAge.Add(key.ToLower(), time);
                                }
                            }
                        }
                    }
                }

                return maxAge;
            }
        }

        #endregion

        #region Named Image Size

        private static IDictionary<string, Size> namingSize = null;
        public static IDictionary<string, Size> NamedImageSize
        {
            get
            {
                lock (locker)
                {
                    if (namingSize == null)
                    {
                        namingSize = new Dictionary<string, Size>();
                        IDictionary<string, string> sizeList = config["namingSize"];
                        string size = "";
                        int width, height;

                        foreach (string key in sizeList.Keys)
                        {
                            width = 640;
                            height = int.MinValue;
                            size = sizeList[key];

                            if (size.IndexOf("x") > -1)
                            {
                                int.TryParse(size.Split('x')[0], out width);
                                int.TryParse(size.Split('x')[1], out height);
                            }
                            else
                            {
                                int.TryParse(size, out width);
                            }

                            namingSize.Add(key, new Size(width, height));
                        }
                    }
                }

                return namingSize;
            }
        }

        #endregion

        #region WaterMark

        private static IDictionary<string, Stream> watermark = null;
        public static IDictionary<string, Stream> Watermark
        {
            get
            {
                lock (locker)
                {
                    if (watermark == null)
                    {
                        watermark = new Dictionary<string, Stream>();
                        Stream wm = null;
                        foreach (KeyValuePair<string, string> kv in config["watermark"])
                        {
                            if (kv.Value.ToLower().StartsWith("http://")) // uri format definition
                            {
                                WebRequest wr = WebRequest.Create(kv.Value);
                                try
                                {
                                    wm = wr.GetResponse().GetResponseStream();
                                }
                                catch { } // get exception, change the parse mode from image to text
                            }
                            else if (File.Exists(kv.Value)) // file format definition
                            {
                                using(Image2D img = Image2D.FromFile(kv.Value))
                                {
                                    img.Save(wm, img.RawFormat);
                                }
                            }
                            else
                            {
                                if (kv.Value.ToLower().StartsWith("font:")) // text format definition
                                {
                                    WatermarkText wmText = TextWatermarkSerializer(kv.Value);
                                    wm = ImageProcessor.ConvertTextToImage(wmText.Font, wmText.FontColor, wmText.Content);
                                }
                            }

                            // handle all unrecognized definition as text mode
                            if (wm == null)
                            {
                                WatermarkText wmText = TextWatermarkSerializer("font:text=" + kv.Value);
                                wm = ImageProcessor.ConvertTextToImage(wmText.Font, wmText.FontColor, wmText.Content);
                            }

                            watermark.Add(kv.Key, wm);
                        }
                    }
                }

                return watermark;
            }
        }

        private static WatermarkText TextWatermarkSerializer(string setting)
        {
            string[] fontProp = setting.ToLower().Substring(5).Split(';');
            string name = "微软雅黑", text = ""; // default value
            float size = 32;    // default value
            Color color = Color.FromArgb(255, 255, 255); // default value

            string n, v;
            int i;
            foreach (string prop in fontProp)
            {
                i = prop.IndexOf("=");
                if (i > -1)
                {
                    n = prop.Substring(0, i).Trim().ToLower();
                    v = prop.Substring(i + 1).Trim();

                    switch (n)
                    {
                        case "name":
                            name = v;
                            break;
                        case "text":
                            text = v;
                            break;
                        case "size":
                            float.TryParse(v, out size);
                            break;
                        case "color":
                            Regex reg = new Regex("^#(?<value>[0-9A-F]{3}|[0-9A-F]{6})$", RegexOptions.IgnoreCase);
                            Match m = reg.Match(v);
                            if (m.Success)
                            {
                                v = m.Groups["value"].Value;
                                if (v.Length == 3)
                                {
                                    v = new string(v[0], 2) + new string(v[1], 2) + new string(v[2], 2);
                                }
                                color = Color.FromArgb(int.Parse(v, System.Globalization.NumberStyles.HexNumber));
                            }
                            else
                            {
                                color = Color.FromName(v); // try to convert as a named color
                            }
                            break;
                    }
                }
            }

            return new WatermarkText() { Content = text, Font = new Font(name, size), FontColor = color };
        }

        protected class WatermarkText
        {
            protected internal Font Font { get; set; }
            protected internal Color FontColor { get; set; }
            protected internal string Content { get; set; }
        }

        #endregion
    }
}

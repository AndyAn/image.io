using System;
using System.Web;
using System.IO;

namespace XOGroup.Image.IO
{
    public class ImageSourceHandler : IHttpModule
    {
        /// <summary>
        /// You will need to configure this module in the web.config file of your
        /// web and register it with IIS before being able to use it. For more information
        /// see the following link: http://go.microsoft.com/?linkid=8101007
        /// </summary>
        #region IHttpModule Members

        public void Dispose()
        {
            //clean-up code here.
        }

        public void Init(HttpApplication context)
        {
            // Below is an example of how you can handle LogRequest event and provide 
            // custom logging implementation for it
            context.LogRequest += new EventHandler(OnLogRequest);
            context.BeginRequest += new EventHandler(OnBeginRequest);
        }

        #endregion

        private void OnLogRequest(Object source, EventArgs e)
        {
            //custom logging logic can go here
        }

        private void OnBeginRequest(object source, EventArgs e)
        {
            // on/off this HttpModule
            if (ConfigHelper.GlobalSettings["service"])
            {
                HttpContext context = (source as HttpApplication).Context;

                ImageProcess(context);
            }
        }

        private void ImageProcess(HttpContext context)
        {
            string userAgent = GetUserAgent(context);
            string uri = context.Request.Url.AbsolutePath;
            string resFile = context.Server.MapPath(uri);
            string ext = Path.GetExtension(uri).Replace(".", "").ToLower();

            ResponsiveImage ri = ResponsiveImage.Create(resFile);

            if (ConfigHelper.SupportedImageFormat.IndexOf(ext) > -1)
            {
                DateTime lastModifiedDate = File.GetLastWriteTimeUtc(resFile);
                string etag = System.Web.Security.FormsAuthentication.HashPasswordForStoringInConfigFile(lastModifiedDate.ToString(), "MD5").ToLower().Substring(8, 16);

                context.Response.Clear();
                context.Response.Cache.SetETag(string.Format("\"{0}\"", etag + ":0"));
                context.Response.Cache.SetCacheability(HttpCacheability.Public);
                //context.Response.Cache.SetProxyMaxAge(ConfigHelper.MaxAge[ext]);
                context.Response.Cache.SetMaxAge(ConfigHelper.MaxAge[ext]);
                context.Response.Cache.SetExpires(DateTime.Now.Add(ConfigHelper.MaxAge[ext]));
                context.Response.Cache.SetLastModified(lastModifiedDate);

                //context.Response.Headers.Remove("Server");
                //context.Response.AddHeader("Server", "MS-IIS");
                context.Response.AddHeader("Accept-Ranges", "bytes");

                context.Response.ContentType = string.Format("image/{0}", ext);
            }
        }

        private string GetUserAgent(HttpContext context)
        {
            return context.Request.UserAgent;
        }
    }
}

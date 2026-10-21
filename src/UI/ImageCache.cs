using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace SkyMatch
{
    public static class ThumbnailCache
    {
        private const int Width = 720;
        private const int Height = 405;

        public static string ThumbPath(string id)
        {
            return Path.Combine(AppPaths.ThumbnailDirectory, id + ".jpg");
        }

        public static void BuildAll(IList<WallpaperItem> wallpapers)
        {
            foreach (WallpaperItem item in wallpapers)
            {
                try
                {
                    Build(item);
                }
                catch (Exception ex)
                {
                    Log.Warn("生成缩略图失败：" + item.Id + " | " + ex.Message);
                }
            }
        }

        public static void Build(WallpaperItem item)
        {
            if (item == null || String.IsNullOrEmpty(item.PreviewPath) || !File.Exists(item.PreviewPath)) return;
            string thumb = ThumbPath(item.Id);
            if (File.Exists(thumb) && File.GetLastWriteTimeUtc(thumb) >= File.GetLastWriteTimeUtc(item.PreviewPath))
                return;
            AppPaths.Ensure();
            using (Image source = LoadImage(item.PreviewPath))
            {
                if (source == null) return;
                using (Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(Color.FromArgb(18, 23, 32));
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        double scale = Math.Max((double)Width / source.Width, (double)Height / source.Height);
                        int w = (int)Math.Ceiling(source.Width * scale);
                        int h = (int)Math.Ceiling(source.Height * scale);
                        g.DrawImage(source, (Width - w) / 2, (Height - h) / 2, w, h);
                    }
                    string tmp = thumb + ".tmp";
                    SaveJpeg(bitmap, tmp, 88);
                    if (File.Exists(thumb)) File.Delete(thumb);
                    File.Move(tmp, thumb);
                }
            }
        }

        private static void SaveJpeg(Image image, string path, long quality)
        {
            ImageCodecInfo codec = FindJpegCodec();
            if (codec == null)
            {
                image.Save(path, ImageFormat.Jpeg);
                return;
            }
            using (EncoderParameters parameters = new EncoderParameters(1))
            using (EncoderParameter parameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality))
            {
                parameters.Param[0] = parameter;
                image.Save(path, codec, parameters);
            }
        }

        private static ImageCodecInfo FindJpegCodec()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
                if (codec.FormatID == ImageFormat.Jpeg.Guid) return codec;
            return null;
        }

        public static Image LoadImage(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (Image source = Image.FromStream(stream, true, false))
                    {
                        return new Bitmap(source);
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }

    public static class AppImages
    {
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();
        private static readonly object Sync = new object();

        public static Bitmap GetWallpaper(WallpaperItem item, int width, int height)
        {
            return GetWallpaper(item, width, height, false);
        }

        public static Bitmap GetWallpaper(WallpaperItem item, int width, int height, bool fit)
        {
            if (item == null) return null;
            string key = item.Id + "|" + width + "|" + height + (fit ? "|fit" : "|crop");
            lock (Sync)
            {
                Bitmap cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
            }
            string sourcePath = item.PreviewPath;
            if (String.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                sourcePath = ThumbnailCache.ThumbPath(item.Id);
            if (!File.Exists(sourcePath)) sourcePath = item.PreviewPath;
            Bitmap result = fit ? LoadFit(sourcePath, width, height) : LoadCropped(sourcePath, width, height);
            lock (Sync)
            {
                if (result != null) Cache[key] = result;
            }
            return result;
        }

        public static Bitmap LoadFit(string path, int width, int height)
        {
            if (String.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                using (Image source = ThumbnailCache.LoadImage(path))
                {
                    if (source == null) return null;
                    Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                    using (Graphics g = Graphics.FromImage(result))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        double scale = Math.Min((double)width / source.Width, (double)height / source.Height);
                        int w = (int)Math.Floor(source.Width * scale);
                        int h = (int)Math.Floor(source.Height * scale);
                        g.DrawImage(source, (width - w) / 2, (height - h) / 2, w, h);
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("加载壁纸预览失败：" + path + " | " + ex.Message);
                return null;
            }
        }

        public static Bitmap LoadCropped(string path, int width, int height)
        {
            if (String.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                using (Image source = ThumbnailCache.LoadImage(path))
                {
                    if (source == null) return null;
                    Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                    using (Graphics g = Graphics.FromImage(result))
                    {
                        g.Clear(Color.FromArgb(20, 26, 35));
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        double scale = Math.Max((double)width / source.Width, (double)height / source.Height);
                        int w = (int)Math.Ceiling(source.Width * scale);
                        int h = (int)Math.Ceiling(source.Height * scale);
                        g.DrawImage(source, (width - w) / 2, (height - h) / 2, w, h);
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("加载壁纸预览失败：" + path + " | " + ex.Message);
                return null;
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                foreach (Bitmap bmp in Cache.Values) bmp.Dispose();
                Cache.Clear();
            }
        }
    }
}

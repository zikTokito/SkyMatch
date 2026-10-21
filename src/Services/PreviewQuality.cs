using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SkyMatch
{
    /// <summary>
    /// Detects broken workshop previews that decode to a nearly uniform black
    /// frame. These are not usable wallpaper previews and are hidden.
    /// </summary>
    public static class PreviewQuality
    {
        public static bool IsSolidColor(string path)
        {
            if (String.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image image = Image.FromStream(stream, true, false))
                using (Bitmap small = new Bitmap(64, 64, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(small))
                    {
                        g.Clear(Color.Black);
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(image, new Rectangle(0, 0, 64, 64));
                    }
                    BitmapData bits = small.LockBits(new Rectangle(0, 0, 64, 64),
                        ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        byte[] row = new byte[bits.Stride];
                        Dictionary<int, int> buckets = new Dictionary<int, int>();
                        int count = 0;
                        for (int y = 0; y < 64; y++)
                        {
                            Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), row, 0, row.Length);
                            for (int x = 0; x < 64; x++)
                            {
                                int p = x * 3;
                                int b = row[p] >> 3;
                                int g = row[p + 1] >> 3;
                                int r = row[p + 2] >> 3;
                                int key = (r << 10) | (g << 5) | b;
                                int n;
                                buckets.TryGetValue(key, out n);
                                buckets[key] = n + 1;
                                count++;
                            }
                        }
                        int dominant = 0;
                        foreach (int n in buckets.Values) if (n > dominant) dominant = n;
                        // A single colour covering half the preview is treated
                        // as a solid block (black/grey/white or any colour).
                        return count > 0 && dominant >= (int)Math.Ceiling(count * 0.50);
                    }
                    finally
                    {
                        small.UnlockBits(bits);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("预览纯色检测失败：" + ex.Message);
                return false;
            }
        }

        public static bool IsBlankBlack(string path)
        {
            return IsSolidColor(path);
        }
    }
}

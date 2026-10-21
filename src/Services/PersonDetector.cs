using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SkyMatch
{
    /// <summary>
    /// Small COCO person detector used by the scenic-only mode. It catches
    /// people in scenery (including small background figures) that the
    /// character/landscape CLIP prompts intentionally ignore.
    /// </summary>
    public static class PersonDetector
    {
        private static readonly object Sync = new object();
        private static InferenceSession _session;
        private static bool _tried;
        private static bool _available;

        public static bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                return _available;
            }
        }

        public static bool HasPerson(string previewPath)
        {
            EnsureInitialized();
            if (!_available || String.IsNullOrEmpty(previewPath) || !File.Exists(previewPath)) return false;
            try
            {
                const int size = 640;
                float[] data = new float[3 * size * size];
                using (Image source = LoadImage(previewPath))
                {
                    if (source == null) return false;
                    double scale = Math.Min((double)size / source.Width, (double)size / source.Height);
                    int w = Math.Max(1, (int)Math.Round(source.Width * scale));
                    int h = Math.Max(1, (int)Math.Round(source.Height * scale));
                    int left = (size - w) / 2;
                    int top = (size - h) / 2;
                    using (Bitmap canvas = new Bitmap(size, size, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(canvas))
                        {
                            g.Clear(Color.FromArgb(114, 114, 114));
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(source, new Rectangle(left, top, w, h));
                        }
                        BitmapData bits = canvas.LockBits(new Rectangle(0, 0, size, size),
                            ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                        try
                        {
                            byte[] row = new byte[bits.Stride];
                            for (int y = 0; y < size; y++)
                            {
                                Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), row, 0, row.Length);
                                for (int x = 0; x < size; x++)
                                {
                                    int p = x * 3;
                                    data[0 * size * size + y * size + x] = row[p + 2] / 255f;
                                    data[1 * size * size + y * size + x] = row[p + 1] / 255f;
                                    data[2 * size * size + y * size + x] = row[p] / 255f;
                                }
                            }
                        }
                        finally
                        {
                            canvas.UnlockBits(bits);
                        }
                    }
                }

                DenseTensor<float> tensor = new DenseTensor<float>(data, new[] { 1, 3, size, size });
                NamedOnnxValue input = NamedOnnxValue.CreateFromTensor("images", tensor);
                lock (Sync)
                {
                    using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
                        _session.Run(new[] { input }))
                    {
                        DenseTensor<float> output = outputs[0].AsTensor<float>() as DenseTensor<float>;
                        if (output == null || output.Length < 84 * 8400) return false;
                        // YOLOv8: [1, 4+80, 8400], class 0 = person.
                        int count = 8400;
                        int personOffset = 4 * count;
                        for (int i = 0; i < count; i++)
                        {
                            if (output.GetValue(personOffset + i) >= 0.20f) return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Person detection failed:" + ex.Message);
            }
            return false;
        }

        private static void EnsureInitialized()
        {
            if (_tried) return;
            lock (Sync)
            {
                if (_tried) return;
                _tried = true;
                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "models", "yolo", "yolov8n.onnx");
                    if (!File.Exists(path))
                    {
                        Log.Warn("The person detection model does not exist:" + path);
                        return;
                    }
                    SessionOptions options = new SessionOptions();
                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    options.IntraOpNumThreads = Math.Max(1, Math.Min(2, Environment.ProcessorCount - 1));
                    _session = new InferenceSession(path, options);
                    _available = true;
                    Log.Info("Person detection model has been loaded");
                }
                catch (Exception ex)
                {
                    Log.Warn("Person detection model failed to load:" + ex.Message);
                    _available = false;
                }
            }
        }

        private static Image LoadImage(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image image = Image.FromStream(stream, true, false))
                    return new Bitmap(image);
            }
            catch (Exception ex)
            {
                Log.Warn("Person detection failed to read preview:" + ex.Message);
                return null;
            }
        }
    }
}

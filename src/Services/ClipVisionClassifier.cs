using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SkyMatch
{
    public class ClipVisionResult
    {
        public bool Available { get; set; }
        public WeatherKind Weather { get; set; }
        public double WeatherConfidence { get; set; }
        public Dictionary<WeatherKind, double> WeatherScores { get; set; }
        public TimeSlot Time { get; set; }
        public double TimeConfidence { get; set; }
        public Dictionary<TimeSlot, double> TimeScores { get; set; }
        public bool CharacterFocus { get; set; }
        public double CharacterProbability { get; set; }
        public string Reason { get; set; }
        public string Error { get; set; }
    }

    public static class ClipVisionClassifier
    {
        private static readonly object Sync = new object();
        private static InferenceSession _session;
        private static bool _available;
        private static bool _largeModel;
        private static string _loadedAlgorithm;
        private static string _modelDirectory;
        private static Dictionary<string, Dictionary<string, List<ClipPrompt>>> _prompts;

        public class ClipPrompt
        {
            public string Prompt { get; set; }
            public List<double> Embedding { get; set; }
        }

        public static bool IsAvailable
        {
            get { return IsAvailableFor("clip"); }
        }

        public static bool IsAvailableFor(string algorithm)
        {
            string dir = DirectoryFor(algorithm);
            return File.Exists(Path.Combine(dir, "vision_model.onnx"))
                && File.Exists(Path.Combine(dir, "prompt_embeddings.json"))
                && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onnxruntime.dll"));
        }

        public static string ModelDirectory
        {
            get { return DirectoryFor(_loadedAlgorithm); }
        }

        public static void Unload()
        {
            lock (Sync)
            {
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }
                _prompts = null;
                _available = false;
                _loadedAlgorithm = null;
            }
        }

        private static string DirectoryFor(string algorithm)
        {
            bool large = algorithm == "clip_large" || algorithm == "clip_large_hybrid";
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models",
                large ? "clip-large" : "clip");
        }

        public static ClipVisionResult Classify(string previewPath)
        {
            return Classify(previewPath, "clip");
        }

        public static ClipVisionResult Classify(string previewPath, string algorithm)
        {
            ClipVisionResult result = new ClipVisionResult();
            result.WeatherScores = new Dictionary<WeatherKind, double>();
            result.TimeScores = new Dictionary<TimeSlot, double>();
            EnsureInitialized(algorithm);
            if (!_available)
            {
                result.Error = "CLIP 模型不可用";
                return result;
            }
            if (String.IsNullOrEmpty(previewPath) || !File.Exists(previewPath))
            {
                result.Error = "预览图不存在";
                return result;
            }
            try
            {
                float[] embedding = GetImageEmbedding(previewPath);
                if (embedding == null) return result;

                Dictionary<string, double> w = ScoreGroup("weather", embedding, 100.0);
                Dictionary<TimeSlot, double> t = ScoreGroupTime("time", embedding);
                Dictionary<string, double> s = ScoreGroup("subject", embedding, 20.0);

                WeatherKind bestWeather;
                result.WeatherConfidence = BestWeather(w, out bestWeather);
                result.Weather = bestWeather;
                foreach (KeyValuePair<string, double> pair in w)
                {
                    WeatherKind kind;
                    if (TryWeather(pair.Key, out kind)) result.WeatherScores[kind] = pair.Value * 10.0;
                }

                TimeSlot bestTime;
                result.TimeConfidence = BestTime(t, out bestTime);
                result.Time = bestTime;
                foreach (KeyValuePair<TimeSlot, double> pair in t) result.TimeScores[pair.Key] = pair.Value * 10.0;

                double charProb = s.ContainsKey("Character") ? s["Character"] : 0;
                result.CharacterProbability = charProb;
                result.CharacterFocus = charProb >= (_largeModel ? 0.63 : 0.55);
                result.Available = true;
                result.Reason = "CLIP 零样本：天气「" + WeatherKindInfo.Name(result.Weather)
                    + "」，时段「" + TimeSlotInfo.Name(result.Time) + "」，"
                    + (result.CharacterFocus ? "主人物" : "主风景");
                return result;
            }
            catch (Exception ex)
            {
                Log.Warn("CLIP 识图失败：" + ex.Message);
                result.Error = ex.Message;
                return result;
            }
        }

        private static void EnsureInitialized(string algorithm)
        {
            bool large = algorithm == "clip_large" || algorithm == "clip_large_hybrid";
            lock (Sync)
            {
                if (_loadedAlgorithm == algorithm) return;
                try
                {
                    if (_session != null)
                    {
                        _session.Dispose();
                        _session = null;
                    }
                    _available = false;
                    _largeModel = large;
                    _loadedAlgorithm = algorithm;
                    _modelDirectory = DirectoryFor(algorithm);
                    string dir = _modelDirectory;
                    string vision = Path.Combine(dir, "vision_model.onnx");
                    string promptPath = Path.Combine(dir, "prompt_embeddings.json");
                    if (!File.Exists(vision) || !File.Exists(promptPath))
                    {
                        Log.Warn("CLIP 模型文件不完整：" + dir);
                        return;
                    }
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = Int32.MaxValue;
                    _prompts = serializer.Deserialize<Dictionary<string, Dictionary<string, List<ClipPrompt>>>>(
                        File.ReadAllText(promptPath, System.Text.Encoding.UTF8));
                    SessionOptions options = new SessionOptions();
                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    options.IntraOpNumThreads = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));
                    _session = new InferenceSession(vision, options);
                    _available = true;
                    Log.Info("CLIP 识图模型已加载：" + (large ? "ViT-L/14" : "ViT-B/32"));
                }
                catch (Exception ex)
                {
                    Log.Warn("CLIP 模型加载失败：" + ex.Message);
                    _available = false;
                }
            }
        }

        private static Dictionary<string, double> ScoreGroup(string group, float[] image, double temperature)
        {
            Dictionary<string, double> raw = new Dictionary<string, double>();
            if (_prompts == null || !_prompts.ContainsKey(group)) return raw;
            foreach (KeyValuePair<string, List<ClipPrompt>> category in _prompts[group])
            {
                double best = Double.MinValue;
                foreach (ClipPrompt prompt in category.Value)
                {
                    float[] emb = ToFloat(prompt.Embedding);
                    double sim = Dot(image, emb);
                    if (sim > best) best = sim;
                }
                raw[category.Key] = best;
            }
            return Softmax(raw, temperature);
        }

        private static Dictionary<TimeSlot, double> ScoreGroupTime(string group, float[] image)
        {
            Dictionary<string, double> raw = ScoreGroup(group, image, 100.0);
            Dictionary<TimeSlot, double> result = new Dictionary<TimeSlot, double>();
            foreach (KeyValuePair<string, double> pair in raw)
            {
                TimeSlot slot;
                if (TryTime(pair.Key, out slot)) result[slot] = pair.Value;
            }
            return result;
        }

        private static Dictionary<string, double> Softmax(Dictionary<string, double> raw, double temperature)
        {
            Dictionary<string, double> result = new Dictionary<string, double>();
            if (raw.Count == 0) return result;
            double max = Double.MinValue;
            foreach (double value in raw.Values) if (value > max) max = value;
            double sum = 0;
            foreach (double value in raw.Values) sum += Math.Exp((value - max) * temperature);
            foreach (KeyValuePair<string, double> pair in raw)
                result[pair.Key] = Math.Exp((pair.Value - max) * temperature) / sum;
            return result;
        }

        private static double BestWeather(Dictionary<string, double> scores, out WeatherKind best)
        {
            best = WeatherKind.Cloudy;
            double top = 0;
            foreach (KeyValuePair<string, double> pair in scores)
            {
                WeatherKind kind;
                if (!TryWeather(pair.Key, out kind)) continue;
                if (pair.Value >= top) { top = pair.Value; best = kind; }
            }
            return top * 100.0;
        }

        private static double BestTime(Dictionary<TimeSlot, double> scores, out TimeSlot best)
        {
            best = TimeSlot.Day;
            double top = 0;
            foreach (KeyValuePair<TimeSlot, double> pair in scores)
                if (pair.Value >= top) { top = pair.Value; best = pair.Key; }
            return top * 100.0;
        }

        private static bool TryWeather(string name, out WeatherKind kind)
        {
            kind = WeatherKind.Cloudy;
            if (String.IsNullOrEmpty(name)) return false;
            foreach (WeatherKind candidate in WeatherKindInfo.All)
            {
                if (String.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    kind = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool TryTime(string name, out TimeSlot slot)
        {
            slot = TimeSlot.Day;
            if (String.IsNullOrEmpty(name)) return false;
            foreach (TimeSlot candidate in TimeSlotInfo.All)
            {
                if (String.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    slot = candidate;
                    return true;
                }
            }
            return false;
        }

        private static float[] ToFloat(List<double> values)
        {
            if (values == null) return new float[0];
            float[] result = new float[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = (float)values[i];
            return result;
        }

        private static double Dot(float[] a, float[] b)
        {
            double sum = 0;
            int count = Math.Min(a.Length, b.Length);
            for (int i = 0; i < count; i++) sum += a[i] * b[i];
            return sum;
        }

        private static float[] GetImageEmbedding(string path)
        {
            const int size = 224;
            float[] data = new float[3 * size * size];
            using (Image source = LoadImage(path))
            {
                if (source == null) return null;
                double scale = 224.0 / Math.Min(source.Width, source.Height);
                int w = Math.Max(size, (int)Math.Round(source.Width * scale));
                int h = Math.Max(size, (int)Math.Round(source.Height * scale));
                using (Bitmap resized = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(source, 0, 0, w, h);
                    }
                    int left = (w - size) / 2;
                    int top = (h - size) / 2;
                    using (Bitmap crop = new Bitmap(size, size, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(crop))
                            g.DrawImage(resized, new Rectangle(0, 0, size, size),
                                new Rectangle(left, top, size, size), GraphicsUnit.Pixel);
                        float[] means = { 0.48145466f, 0.4578275f, 0.40821073f };
                        float[] stds = { 0.26862954f, 0.26130258f, 0.27577711f };
                        BitmapData bits = crop.LockBits(new Rectangle(0, 0, size, size),
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
                                    data[0 * size * size + y * size + x] = ((row[p + 2] / 255f) - means[0]) / stds[0];
                                    data[1 * size * size + y * size + x] = ((row[p + 1] / 255f) - means[1]) / stds[1];
                                    data[2 * size * size + y * size + x] = ((row[p] / 255f) - means[2]) / stds[2];
                                }
                            }
                        }
                        finally
                        {
                            crop.UnlockBits(bits);
                        }
                    }
                }
            }

            DenseTensor<float> tensor = new DenseTensor<float>(data, new[] { 1, 3, size, size });
            NamedOnnxValue input = NamedOnnxValue.CreateFromTensor("pixel_values", tensor);
            lock (Sync)
            {
                using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
                    _session.Run(new[] { input }))
                {
                    DenseTensor<float> output = outputs[0].AsTensor<float>() as DenseTensor<float>;
                    if (output == null) return null;
                    int length = (int)output.Length;
                    float[] embedding = new float[length];
                    for (int i = 0; i < length; i++) embedding[i] = output.GetValue(i);
                    double norm = 0;
                    for (int i = 0; i < embedding.Length; i++) norm += embedding[i] * embedding[i];
                    norm = Math.Sqrt(norm);
                    if (norm > 0)
                        for (int i = 0; i < embedding.Length; i++) embedding[i] = (float)(embedding[i] / norm);
                    return embedding;
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
                Log.Warn("CLIP 读取预览失败：" + ex.Message);
                return null;
            }
        }
    }
}

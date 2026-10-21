using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace SkyMatch
{
    public class VisionCacheEntry
    {
        public string Id { get; set; }
        public long PreviewLength { get; set; }
        public long PreviewTicks { get; set; }
        public int Weather { get; set; }
        public int Time { get; set; }
        public double WeatherConfidence { get; set; }
        public double TimeConfidence { get; set; }
        public bool CharacterFocus { get; set; }
        public bool HasPeople { get; set; }
        public bool BlankPreview { get; set; }
        public bool PeopleScanned { get; set; }
        public string Reason { get; set; }
        public Dictionary<string, double> Scores { get; set; }
        public Dictionary<string, double> TimeScores { get; set; }
    }

    public class VisionCacheFile
    {
        public int Version { get; set; }
        public Dictionary<string, VisionCacheEntry> Entries { get; set; }

        public VisionCacheFile()
        {
            Entries = new Dictionary<string, VisionCacheEntry>(StringComparer.Ordinal);
        }
    }

    public static class VisionCacheStore
    {
        private const int CurrentVersion = 2;
        private static readonly object Sync = new object();

        private static string FilePath
        {
            get { return Path.Combine(AppPaths.DataDirectory, "vision-cache.json"); }
        }

        public static VisionCacheFile Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new VisionCacheFile();
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = Int32.MaxValue;
                VisionCacheFile file = serializer.Deserialize<VisionCacheFile>(
                    File.ReadAllText(FilePath, System.Text.Encoding.UTF8));
                if (file == null || file.Version != CurrentVersion) return new VisionCacheFile();
                if (file.Entries == null) file.Entries = new Dictionary<string, VisionCacheEntry>(StringComparer.Ordinal);
                return file;
            }
            catch (Exception ex)
            {
                Log.Warn("读取识图缓存失败：" + ex.Message);
                return new VisionCacheFile();
            }
        }

        public static void Save(VisionCacheFile file)
        {
            if (file == null) return;
            lock (Sync)
            {
                try
                {
                    file.Version = CurrentVersion;
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = Int32.MaxValue;
                    string temp = FilePath + ".tmp";
                    File.WriteAllText(temp, serializer.Serialize(file), System.Text.Encoding.UTF8);
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    File.Move(temp, FilePath);
                }
                catch (Exception ex)
                {
                    Log.Warn("保存识图缓存失败：" + ex.Message);
                }
            }
        }

        public static bool IsValid(VisionCacheEntry entry, WallpaperItem item)
        {
            if (entry == null || item == null || String.IsNullOrEmpty(item.PreviewPath)) return false;
            if (!File.Exists(item.PreviewPath)) return false;
            try
            {
                FileInfo info = new FileInfo(item.PreviewPath);
                return entry.PreviewLength == info.Length && entry.PreviewTicks == info.LastWriteTimeUtc.Ticks;
            }
            catch
            {
                return false;
            }
        }

        public static VisionCacheEntry Create(WallpaperItem item)
        {
            VisionCacheEntry entry = new VisionCacheEntry();
            entry.Id = item.Id;
            try
            {
                FileInfo info = new FileInfo(item.PreviewPath);
                entry.PreviewLength = info.Length;
                entry.PreviewTicks = info.LastWriteTimeUtc.Ticks;
            }
            catch
            {
            }
            entry.Weather = (int)item.AutoPrimaryWeather;
            entry.Time = (int)item.AutoPrimaryTime;
            entry.WeatherConfidence = item.AutoConfidence;
            entry.TimeConfidence = item.AutoTimeConfidence;
            entry.CharacterFocus = item.CharacterFocus;
            entry.HasPeople = item.HasPeople;
            entry.BlankPreview = item.BlankPreview;
            entry.PeopleScanned = item.PeopleScanned;
            entry.Reason = item.AutoReason;
            entry.Scores = new Dictionary<string, double>();
            entry.TimeScores = new Dictionary<string, double>();
            if (item.AutoScores != null)
                foreach (KeyValuePair<WeatherKind, double> pair in item.AutoScores)
                    entry.Scores[pair.Key.ToString()] = pair.Value;
            if (item.AutoTimeScores != null)
                foreach (KeyValuePair<TimeSlot, double> pair in item.AutoTimeScores)
                    entry.TimeScores[pair.Key.ToString()] = pair.Value;
            return entry;
        }

        public static void Apply(VisionCacheEntry entry, WallpaperItem item, AppSettings settings)
        {
            if (entry == null || item == null) return;
            item.AutoPrimaryWeather = (WeatherKind)entry.Weather;
            item.AutoPrimaryTime = (TimeSlot)entry.Time;
            item.AutoConfidence = entry.WeatherConfidence;
            item.AutoTimeConfidence = entry.TimeConfidence;
            item.AutoReason = entry.Reason ?? "";
            item.AutoTimeReason = entry.Reason ?? "";
            item.CharacterFocus = entry.CharacterFocus;
            item.HasPeople = entry.HasPeople;
            item.BlankPreview = entry.BlankPreview;
            item.PeopleScanned = entry.PeopleScanned;
            item.AutoScores = new Dictionary<WeatherKind, double>();
            item.AutoTimeScores = new Dictionary<TimeSlot, double>();
            if (entry.Scores != null)
            {
                foreach (KeyValuePair<string, double> pair in entry.Scores)
                {
                    WeatherKind kind;
                    if (Enum.TryParse<WeatherKind>(pair.Key, out kind)) item.AutoScores[kind] = pair.Value;
                }
            }
            if (entry.TimeScores != null)
            {
                foreach (KeyValuePair<string, double> pair in entry.TimeScores)
                {
                    TimeSlot slot;
                    if (Enum.TryParse<TimeSlot>(pair.Key, out slot)) item.AutoTimeScores[slot] = pair.Value;
                }
            }
            item.Scores = new Dictionary<WeatherKind, double>(item.AutoScores);
            item.TimeScores = new Dictionary<TimeSlot, double>(item.AutoTimeScores);
            item.PrimaryWeather = item.AutoPrimaryWeather;
            item.Confidence = item.AutoConfidence;
            item.PrimaryTime = item.AutoPrimaryTime;
            item.TimeConfidence = item.AutoTimeConfidence;
            item.Reason = item.AutoReason;
            item.TimeReason = item.AutoTimeReason;
            WallpaperClassifier.ApplyManualOverride(item, settings);
        }
    }
}

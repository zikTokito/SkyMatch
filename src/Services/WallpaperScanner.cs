using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SkyMatch
{
    public class WallpaperScanResult
    {
        public string WallpaperEnginePath { get; set; }
        public string SteamPath { get; set; }
        public string WorkshopPath { get; set; }
        public string ActivePath { get; set; }
        public string ActiveId { get; set; }
        public List<WallpaperItem> Wallpapers { get; set; }
        public string Error { get; set; }
        public int SkippedProjects { get; set; }

        public WallpaperScanResult()
        {
            Wallpapers = new List<WallpaperItem>();
            Error = "";
            ActivePath = "";
            ActiveId = "";
        }
    }

    public class WallpaperScanner
    {
        private static readonly string[] AllowedExtensions = { ".pkg", ".mp4", ".webm", ".html", ".htm", ".json" };
        private const long MaxVideoBytes = 10L * 1024 * 1024;
        private static readonly Regex WorkshopIdRegex = new Regex(@"431960[\\/](\d+)", RegexOptions.IgnoreCase);

        public WallpaperScanResult Scan(AppSettings settings)
        {
            WallpaperScanResult result = new WallpaperScanResult();
            string steam = FindSteamPath();
            result.SteamPath = steam;
            string engine = FindWallpaperEnginePath(steam);
            result.WallpaperEnginePath = engine;
            if (String.IsNullOrEmpty(engine))
            {
                result.Error = "没有找到 Wallpaper Engine，请确认 Steam 中已安装并运行过它。";
                return result;
            }
            if (String.IsNullOrEmpty(steam)) steam = GuessSteamFromEngine(engine);
            if (String.IsNullOrEmpty(steam))
            {
                result.Error = "没有找到 Steam 安装目录。";
                return result;
            }
            result.SteamPath = steam;

            string active = ReadActiveWallpaperPath(engine);
            result.ActivePath = active;
            result.ActiveId = ExtractWorkshopId(active);

            List<string> libraries = FindSteamLibraries(steam);
            List<string> seenWorkshops = new List<string>();
            int skipped = 0;
            foreach (string library in libraries)
            {
                string workshop = Path.Combine(library, "steamapps", "workshop", "content", "431960");
                if (!Directory.Exists(workshop)) continue;
                string normalized = Path.GetFullPath(workshop).TrimEnd('\\').ToLowerInvariant();
                if (seenWorkshops.Contains(normalized)) continue;
                seenWorkshops.Add(normalized);
                if (String.IsNullOrEmpty(result.WorkshopPath)) result.WorkshopPath = workshop;
                ScanWorkshopDirectory(workshop, result, settings, ref skipped);
            }

            result.SkippedProjects = skipped;
            result.Wallpapers.Sort(delegate(WallpaperItem a, WallpaperItem b)
            {
                return String.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
            });
            if (seenWorkshops.Count == 0)
                result.Error = "没有找到创意工坊目录（431960）。";
            Log.Info("壁纸扫描完成：找到 " + result.Wallpapers.Count + " 张可用壁纸，跳过 "
                + skipped + " 个项目；工坊目录 " + result.WorkshopPath);
            return result;
        }

        private void ScanWorkshopDirectory(string workshop, WallpaperScanResult result, AppSettings settings, ref int skipped)
        {
            DirectoryInfo root = new DirectoryInfo(workshop);
            DirectoryInfo[] dirs;
            try { dirs = root.GetDirectories(); }
            catch (Exception ex)
            {
                Log.Error("读取创意工坊目录失败：" + workshop, ex);
                return;
            }
            foreach (DirectoryInfo dir in dirs)
            {
                string projectFile = Path.Combine(dir.FullName, "project.json");
                if (!File.Exists(projectFile))
                {
                    skipped++;
                    continue;
                }
                WallpaperItem item = ReadProject(dir, projectFile, settings);
                if (item == null)
                {
                    skipped++;
                    continue;
                }
                if (PathEquals(item.FilePath, result.ActivePath) || PathEquals(item.ProjectFile, result.ActivePath))
                    item.IsActive = true;
                result.Wallpapers.Add(item);
            }
        }

        private WallpaperItem ReadProject(DirectoryInfo dir, string projectFile, AppSettings settings)
        {
            try
            {
                string raw = File.ReadAllText(projectFile, new UTF8Encoding(true));
                Dictionary<string, object> json = Json.Object(Json.Parse(raw));
                if (json == null) return null;

                string category = Json.GetString(json, "category");
                string type = Json.GetString(json, "type");
                string file = Json.GetString(json, "file");
                if (String.IsNullOrEmpty(file)) return null;
                if (IsAsset(category, type, file)) return null;

                string fullFile = Path.Combine(dir.FullName, file.Replace('/', '\\'));
                string ext = Path.GetExtension(fullFile).ToLowerInvariant();
                if (!File.Exists(fullFile) && (ext == ".json" || ext == ".pkg"))
                {
                    string packed = Path.Combine(dir.FullName, "scene.pkg");
                    if (File.Exists(packed))
                    {
                        fullFile = packed;
                        ext = ".pkg";
                    }
                }
                if (!File.Exists(fullFile)) return null;
                if (Array.IndexOf(AllowedExtensions, ext) < 0) return null;
                if (ext == ".json" && (Path.GetFileName(fullFile).Equals("assets.json", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(fullFile).Equals("effect.json", StringComparison.OrdinalIgnoreCase)))
                    return null;

                WallpaperItem item = new WallpaperItem();
                item.Id = dir.Name;
                item.Title = Json.GetString(json, "title");
                if (String.IsNullOrEmpty(item.Title)) item.Title = dir.Name;
                item.Type = String.IsNullOrEmpty(type) ? InferType(ext) : type;
                item.Category = category;
                item.ProjectFile = projectFile;
                item.FilePath = fullFile;
                item.IsVideo = IsVideoExt(ext);
                item.IsWeb = ext == ".html" || ext == ".htm";
                if (item.IsVideo || item.IsWeb) item.LaunchPath = fullFile;
                else item.LaunchPath = projectFile;
                try
                {
                    long mediaSize = new FileInfo(fullFile).Length;
                    item.DirectorySize = mediaSize;
                    if (item.IsVideo && mediaSize > MaxVideoBytes)
                    {
                        Log.Info("跳过大于 10 MB 的视频壁纸：" + fullFile);
                        return null;
                    }
                }
                catch { }
                item.Description = CleanDescription(Json.GetString(json, "description"));
                item.PreviewPath = ResolvePreview(dir, Json.GetString(json, "preview"));
                item.WorkshopUrl = Json.GetString(json, "workshopurl");
                if (String.IsNullOrEmpty(item.WorkshopUrl))
                    item.WorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + item.Id;
                item.Tags = ReadTags(json);
                item.AgeRating = AgeRatingInfo.FromText(Json.GetString(json, "contentrating"));
                if (item.AgeRating == AgeRating.Everyone)
                    item.AgeRating = AgeRatingInfo.FromTags(item.Tags);
                item.ManualKind = ReadManualKind(settings, item.Id);
                item.Hidden = settings.HiddenIds != null && settings.HiddenIds.Contains(item.Id);
                if (File.Exists(projectFile))
                {
                    try { item.DirectorySize = new FileInfo(fullFile).Length; }
                    catch { }
                }
                return item;
            }
            catch (Exception ex)
            {
                Log.Error("读取壁纸项目失败：" + projectFile, ex);
                return null;
            }
        }

        private static bool IsAsset(string category, string type, string file)
        {
            if (String.Equals(category, "Asset", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(category, "Effect", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(category, "Application", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(type, "assets", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(type, "application", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(type, "effect", StringComparison.OrdinalIgnoreCase)) return true;
            string name = Path.GetFileName(file);
            if (String.Equals(name, "assets.json", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(name, "effect.json", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string InferType(string ext)
        {
            if (IsVideoExt(ext)) return "video";
            if (ext == ".html" || ext == ".htm") return "web";
            return "scene";
        }

        private static bool IsVideoExt(string ext)
        {
            return ext == ".mp4" || ext == ".webm";
        }

        private static List<string> ReadTags(Dictionary<string, object> json)
        {
            List<string> tags = new List<string>();
            List<object> raw = Json.GetArray(json, "tags");
            foreach (object tag in raw)
            {
                string value = Convert.ToString(tag);
                if (!String.IsNullOrEmpty(value)) tags.Add(value);
            }
            return tags;
        }

        private static string ResolvePreview(DirectoryInfo dir, string preview)
        {
            if (!String.IsNullOrEmpty(preview))
            {
                string path = Path.Combine(dir.FullName, preview.Replace('/', '\\'));
                if (File.Exists(path)) return path;
            }
            string[] names = { "preview.gif", "preview.jpg", "preview.png", "preview.jpeg", "preview.webp" };
            foreach (string name in names)
            {
                string path = Path.Combine(dir.FullName, name);
                if (File.Exists(path)) return path;
            }
            return "";
        }

        private static string CleanDescription(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            string text = Regex.Replace(value, "<[^>]+>", " ");
            text = text.Replace("\r", " ").Replace("\n", " ");
            text = Regex.Replace(text, "\\s+", " ");
            if (text.Length > 220) text = text.Substring(0, 220) + "…";
            return text.Trim();
        }

        private static Nullable<int> ReadManualKind(AppSettings settings, string id)
        {
            if (settings == null || settings.ManualKinds == null) return null;
            int value;
            if (settings.ManualKinds.TryGetValue(id, out value))
            {
                if (value >= 0 && value <= 7) return value;
            }
            return null;
        }

        public static string FindSteamPath()
        {
            string path = ReadRegistry(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath");
            if (!String.IsNullOrEmpty(path) && Directory.Exists(path)) return NormalizeSteamPath(path);
            path = ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            if (!String.IsNullOrEmpty(path) && Directory.Exists(path)) return NormalizeSteamPath(path);
            path = ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath");
            if (!String.IsNullOrEmpty(path) && Directory.Exists(path)) return NormalizeSteamPath(path);
            string steamExe = ReadRegistry(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe");
            if (!String.IsNullOrEmpty(steamExe))
            {
                string dir = Path.GetDirectoryName(steamExe);
                if (!String.IsNullOrEmpty(dir) && Directory.Exists(dir)) return NormalizeSteamPath(dir);
            }
            string[] defaults =
            {
                @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam",
                @"D:\Steam", @"D:\SteamLibrary", @"E:\Steam", @"E:\SteamLibrary"
            };
            foreach (string candidate in defaults)
                if (Directory.Exists(candidate)) return candidate;
            return "";
        }

        private static string ReadRegistry(string key, string value)
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key.Replace("HKEY_CURRENT_USER\\", ""), false))
                {
                    if (registryKey != null)
                    {
                        object data = registryKey.GetValue(value);
                        if (data != null) return Convert.ToString(data);
                    }
                }
                if (key.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                {
                    using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(
                        key.Replace("HKEY_LOCAL_MACHINE\\", ""), false))
                    {
                        if (registryKey != null)
                        {
                            object data = registryKey.GetValue(value);
                            if (data != null) return Convert.ToString(data);
                        }
                    }
                }
            }
            catch
            {
            }
            return "";
        }

        private static string NormalizeSteamPath(string path)
        {
            return path.TrimEnd('\\', '/').Replace('/', '\\');
        }

        public static string FindWallpaperEnginePath(string steam)
        {
            List<string> candidates = new List<string>();
            if (!String.IsNullOrEmpty(steam))
                candidates.Add(Path.Combine(steam, "steamapps", "common", "wallpaper_engine"));
            Process[] processes = Process.GetProcessesByName("wallpaper64");
            foreach (Process process in processes)
            {
                try
                {
                    if (!String.IsNullOrEmpty(process.MainModule.FileName))
                        candidates.Add(Path.GetDirectoryName(process.MainModule.FileName));
                }
                catch { }
            }
            processes = Process.GetProcessesByName("wallpaper32");
            foreach (Process process in processes)
            {
                try
                {
                    if (!String.IsNullOrEmpty(process.MainModule.FileName))
                        candidates.Add(Path.GetDirectoryName(process.MainModule.FileName));
                }
                catch { }
            }
            foreach (string candidate in candidates)
            {
                if (!String.IsNullOrEmpty(candidate) && File.Exists(Path.Combine(candidate, "wallpaper64.exe")))
                    return candidate;
                if (!String.IsNullOrEmpty(candidate) && File.Exists(Path.Combine(candidate, "wallpaper32.exe")))
                    return candidate;
            }
            return "";
        }

        private static string GuessSteamFromEngine(string engine)
        {
            try
            {
                DirectoryInfo common = Directory.GetParent(engine);
                if (common == null) return "";
                DirectoryInfo steamapps = common.Parent;
                if (steamapps == null) return "";
                DirectoryInfo steam = steamapps.Parent;
                return steam == null ? "" : steam.FullName;
            }
            catch
            {
                return "";
            }
        }

        public static List<string> FindSteamLibraries(string steam)
        {
            List<string> libraries = new List<string>();
            AddUnique(libraries, steam);
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                try
                {
                    string text = File.ReadAllText(vdf, Encoding.UTF8);
                    MatchCollection matches = Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        string path = match.Groups[1].Value.Replace("\\\\", "\\");
                        if (Directory.Exists(path)) AddUnique(libraries, path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("读取 Steam 库配置失败：" + ex.Message);
                }
            }
            return libraries;
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (String.IsNullOrEmpty(value)) return;
            string normalized = Path.GetFullPath(value).TrimEnd('\\');
            foreach (string existing in list)
                if (String.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(normalized);
        }

        public static string ReadActiveWallpaperPath(string enginePath)
        {
            try
            {
                string config = Path.Combine(enginePath, "config.json");
                if (!File.Exists(config)) return "";
                string raw = File.ReadAllText(config, Encoding.UTF8);
                Dictionary<string, object> root = Json.Object(Json.Parse(raw));
                if (root == null) return "";
                foreach (KeyValuePair<string, object> top in root)
                {
                    Dictionary<string, object> topObject = Json.Object(top.Value);
                    Dictionary<string, object> general = Json.GetObject(topObject, "general");
                    if (general == null) continue;
                    Dictionary<string, object> wallConfig = Json.GetObject(general, "wallpaperconfig");
                    Dictionary<string, object> selected = Json.GetObject(wallConfig, "selectedwallpapers");
                    if (selected == null) continue;
                    foreach (KeyValuePair<string, object> monitor in selected)
                    {
                        Dictionary<string, object> monitorObject = Json.Object(monitor.Value);
                        string file = Json.GetString(monitorObject, "file");
                        if (!String.IsNullOrEmpty(file)) return file.Replace('/', '\\');
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("读取当前壁纸失败：" + ex.Message);
            }
            return "";
        }

        public static string ExtractWorkshopId(string path)
        {
            if (String.IsNullOrEmpty(path)) return "";
            Match match = WorkshopIdRegex.Match(path.Replace('/', '\\'));
            return match.Success ? match.Groups[1].Value : "";
        }

        public static bool PathEquals(string a, string b)
        {
            if (String.IsNullOrEmpty(a) || String.IsNullOrEmpty(b)) return false;
            try
            {
                return String.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}

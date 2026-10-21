using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace SkyMatch
{
    public static class AppPaths
    {
        public static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyMatch");
        public static readonly string SettingsFile = Path.Combine(DataDirectory, "settings.json");
        public static readonly string WeatherCacheFile = Path.Combine(DataDirectory, "weather-cache.json");
        public static readonly string WorkshopCacheFile = Path.Combine(DataDirectory, "workshop-cache.json");
        public static readonly string WorkshopPreviewDirectory = Path.Combine(DataDirectory, "workshop-previews");
        public static readonly string LogFile = Path.Combine(DataDirectory, "logs", "app.log");
        public static readonly string ThumbnailDirectory = Path.Combine(DataDirectory, "thumbnails");

        public static void Ensure()
        {
            if (!Directory.Exists(DataDirectory)) Directory.CreateDirectory(DataDirectory);
            string logDir = Path.GetDirectoryName(LogFile);
            if (!String.IsNullOrEmpty(logDir) && !Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            if (!Directory.Exists(ThumbnailDirectory)) Directory.CreateDirectory(ThumbnailDirectory);
            if (!Directory.Exists(WorkshopPreviewDirectory)) Directory.CreateDirectory(WorkshopPreviewDirectory);
        }
    }

    public static class AppSettingsStore
    {
        private static readonly object Sync = new object();

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(AppPaths.SettingsFile)) return new AppSettings();
                string json = File.ReadAllText(AppPaths.SettingsFile, new UTF8Encoding(true));
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = Int32.MaxValue;
                AppSettings settings = serializer.Deserialize<AppSettings>(json);
                return Validate(settings);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read settings, default settings will be used", ex);
                return new AppSettings();
            }
        }

        public static AppSettings Validate(AppSettings settings)
        {
            if (settings == null) settings = new AppSettings();
            if (settings.ManualKinds == null) settings.ManualKinds = new Dictionary<string, int>();
            if (settings.HiddenIds == null) settings.HiddenIds = new List<string>();
            settings.WorkshopTopEnabled = true;
            if (settings.BlockedIds == null) settings.BlockedIds = new List<string>();
            if (settings.WhitelistIds == null) settings.WhitelistIds = new List<string>();
            if (settings.WorkshopRefreshDays < 1) settings.WorkshopRefreshDays = 7;
            if (settings.WorkshopRefreshDays > 365) settings.WorkshopRefreshDays = 365;

            if (settings.RefreshMinutes != 60) settings.RefreshMinutes = 60;
            if (settings.RecommendationRotateMinutes < 0) settings.RecommendationRotateMinutes = 0;
            if (settings.RecommendationRotateMinutes > 1440) settings.RecommendationRotateMinutes = 1440;
            if (settings.WallpaperVolume < 0) settings.WallpaperVolume = 0;
            if (settings.WallpaperVolume > 100) settings.WallpaperVolume = 100;
            if (String.IsNullOrEmpty(settings.Timezone)) settings.Timezone = "Asia/Shanghai";
            if (settings.Latitude < -90 || settings.Latitude > 90) settings.HasLocation = false;
            if (settings.Longitude < -180 || settings.Longitude > 180) settings.HasLocation = false;
            if (settings.HasLocation && String.IsNullOrEmpty(settings.City)) settings.City = "Current location";
            if (settings.LocationHistory == null) settings.LocationHistory = new List<LocationInfo>();
            settings.LocationHistory.RemoveAll(delegate(LocationInfo item)
            {
                return item == null || String.IsNullOrEmpty(item.Name)
                    || Double.IsNaN(item.Latitude) || Double.IsNaN(item.Longitude);
            });
            if (settings.LocationHistory.Count > 8)
                settings.LocationHistory.RemoveRange(8, settings.LocationHistory.Count - 8);
            if (settings.LocationLocked && !settings.HasLocation) settings.LocationLocked = false;
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                lock (Sync)
                {
                    AppPaths.Ensure();
                    string json = Json.Stringify(settings);
                    string tmp = AppPaths.SettingsFile + ".tmp";
                    File.WriteAllText(tmp, json, new UTF8Encoding(false));
                    if (File.Exists(AppPaths.SettingsFile))
                    {
                        try
                        {
                            File.Replace(tmp, AppPaths.SettingsFile, null);
                        }
                        catch
                        {
                            File.Copy(tmp, AppPaths.SettingsFile, true);
                            File.Delete(tmp);
                        }
                    }
                    else
                    {
                        File.Move(tmp, AppPaths.SettingsFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save settings", ex);
            }
        }

        public static AppSettings Clone(AppSettings source)
        {
            if (source == null) return new AppSettings();
            AppSettings copy = new AppSettings();
            copy.Latitude = source.Latitude;
            copy.Longitude = source.Longitude;
            copy.HasLocation = source.HasLocation;
            copy.City = source.City;
            copy.Admin1 = source.Admin1;
            copy.District = source.District;
            copy.Country = source.Country;
            copy.Timezone = source.Timezone;
            copy.LocationApproximate = source.LocationApproximate;
            copy.LocationLocked = source.LocationLocked;
            copy.LocationHistory = new List<LocationInfo>();
            if (source.LocationHistory != null)
            {
                foreach (LocationInfo history in source.LocationHistory)
                {
                    if (history == null) continue;
                    LocationInfo copyHistory = new LocationInfo();
                    copyHistory.Name = history.Name;
                    copyHistory.Admin1 = history.Admin1;
                    copyHistory.District = history.District;
                    copyHistory.Country = history.Country;
                    copyHistory.Latitude = history.Latitude;
                    copyHistory.Longitude = history.Longitude;
                    copyHistory.Timezone = history.Timezone;
                    copyHistory.IsApproximate = false;
                    copy.LocationHistory.Add(copyHistory);
                }
            }
            copy.AutoFollow = source.AutoFollow;
            copy.RefreshMinutes = source.RefreshMinutes;
            copy.RecommendationRotateMinutes = source.RecommendationRotateMinutes;
            copy.RecommendationRotateEnabled = source.RecommendationRotateEnabled;
            copy.UseTimeOfDay = source.UseTimeOfDay;
            copy.WorkshopTopEnabled = source.WorkshopTopEnabled;
            copy.WorkshopGridView = source.WorkshopGridView;
            copy.WorkshopSortBySubscriptions = source.WorkshopSortBySubscriptions;
            copy.WorkshopRefreshDays = source.WorkshopRefreshDays;
            copy.WorkshopAutoRefresh = source.WorkshopAutoRefresh;
            copy.WorkshopRefreshOnStartup = source.WorkshopRefreshOnStartup;

            copy.PreferScenery = source.PreferScenery;
            copy.AgeRatingMask = source.AgeRatingMask;
            copy.WallpaperVolume = source.WallpaperVolume;
            copy.WallpaperMuted = source.WallpaperMuted;
            copy.MinimizeToTray = source.MinimizeToTray;
            copy.StartWithWindows = source.StartWithWindows;
            copy.ShowNotifications = source.ShowNotifications;
            copy.HasAppliedOnce = source.HasAppliedOnce;
            copy.LastWeatherKind = source.LastWeatherKind;
            copy.LastTimeSlot = source.LastTimeSlot;
            copy.LastAppliedId = source.LastAppliedId;
            copy.LastAppliedKind = source.LastAppliedKind;
            copy.LastWeatherFetch = source.LastWeatherFetch;
            copy.OriginalWallpaperId = source.OriginalWallpaperId;
            copy.OriginalWallpaperPath = source.OriginalWallpaperPath;
            copy.WallpaperEnginePath = source.WallpaperEnginePath;
            copy.ManualKinds = new Dictionary<string, int>();
            if (source.ManualKinds != null)
            {
                foreach (KeyValuePair<string, int> pair in source.ManualKinds)
                    copy.ManualKinds[pair.Key] = pair.Value;
            }
            copy.HiddenIds = new List<string>();
            if (source.HiddenIds != null) copy.HiddenIds.AddRange(source.HiddenIds);
            copy.BlockedIds = new List<string>();
            if (source.BlockedIds != null) copy.BlockedIds.AddRange(source.BlockedIds);
            copy.WhitelistIds = new List<string>();
            if (source.WhitelistIds != null) copy.WhitelistIds.AddRange(source.WhitelistIds);
            return copy;
        }
    }

    public static class WeatherCacheStore
    {
        public static void Save(WeatherData weather)
        {
            if (weather == null) return;
            try
            {
                AppPaths.Ensure();
                WeatherData copy = weather;
                copy.FromCache = false;
                File.WriteAllText(AppPaths.WeatherCacheFile, Json.Stringify(copy), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Error("Caching weather failed", ex);
            }
        }

        public static WeatherData Load()
        {
            try
            {
                if (!File.Exists(AppPaths.WeatherCacheFile)) return null;
                string json = File.ReadAllText(AppPaths.WeatherCacheFile, new UTF8Encoding(true));
                System.Web.Script.Serialization.JavaScriptSerializer serializer =
                    new System.Web.Script.Serialization.JavaScriptSerializer();
                serializer.MaxJsonLength = Int32.MaxValue;
                WeatherData weather = serializer.Deserialize<WeatherData>(json);
                if (weather == null || weather.Current == null || weather.Location == null) return null;
                if (weather.FetchedAt == default(DateTime)) weather.FetchedAt = File.GetLastWriteTime(AppPaths.WeatherCacheFile);
                if ((DateTime.Now - weather.FetchedAt).TotalHours > 72) return null;
                weather.FromCache = true;
                return weather;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read weather cache", ex);
                return null;
            }
        }
    }

    public static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SkyMatch";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enabled)
                    {
                        string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(ValueName, "\"" + exe + "\" --minimized");
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Setting startup failed", ex);
            }
        }
    }
}

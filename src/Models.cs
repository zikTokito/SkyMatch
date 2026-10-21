using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace SkyMatch
{
    public enum WeatherKind
    {
        ClearDay = 0,
        ClearNight = 1,
        Cloudy = 2,
        Overcast = 3,
        Rain = 4,
        Storm = 5,
        Snow = 6,
        Fog = 7
    }

    public enum TimeSlot
    {
        Dawn = 0,
        Day = 1,
        Dusk = 2,
        Night = 3
    }

    public enum AgeRating
    {
        Everyone = 0,
        Questionable = 1,
        Mature = 2
    }

    public static class AgeRatingInfo
    {
        private static readonly string[] Names = { "Everyone", "Teen", "Mature" };

        public static AgeRating[] All
        {
            get { return new[] { AgeRating.Everyone, AgeRating.Questionable, AgeRating.Mature }; }
        }

        public static string Name(AgeRating rating)
        {
            int i = (int)rating;
            return i >= 0 && i < Names.Length ? Names[i] : "Everyone";
        }

        public static int MaskOf(AgeRating rating)
        {
            return 1 << (int)rating;
        }

        public static string DescribeMask(int mask)
        {
            List<string> selected = new List<string>();
            foreach (AgeRating rating in All)
                if ((mask & MaskOf(rating)) != 0) selected.Add(Name(rating));
            return selected.Count == 0 ? "Not selected" : String.Join(" + ", selected.ToArray());
        }

        public static AgeRating FromText(string value)
        {
            if (String.IsNullOrEmpty(value)) return AgeRating.Everyone;
            string text = value.Trim().ToLowerInvariant();
            if (text.Contains("mature") || text.Contains("adult") || text.Contains("r18")
                || text.Contains("18+") || text.Contains("nsfw")) return AgeRating.Mature;
            if (text.Contains("questionable") || text.Contains("teen") || text.Contains("13+"))
                return AgeRating.Questionable;
            return AgeRating.Everyone;
        }

        public static AgeRating FromTags(List<string> tags)
        {
            AgeRating rating = AgeRating.Everyone;
            if (tags == null) return rating;
            foreach (string tag in tags)
            {
                AgeRating current = FromText(tag);
                if (current > rating) rating = current;
            }
            return rating;
        }
    }

    public static class TimeSlotInfo
    {
        private static readonly string[] Names = { "Dawn", "Day", "Dusk", "Night" };
        private static readonly string[] English = { "DAWN", "DAY", "DUSK", "NIGHT" };

        public static string Name(TimeSlot slot)
        {
            int i = (int)slot;
            return i >= 0 && i < Names.Length ? Names[i] : "Unknown";
        }

        public static string EnglishName(TimeSlot slot)
        {
            int i = (int)slot;
            return i >= 0 && i < English.Length ? English[i] : "UNKNOWN";
        }

        public static TimeSlot[] All
        {
            get { return new[] { TimeSlot.Dawn, TimeSlot.Day, TimeSlot.Dusk, TimeSlot.Night }; }
        }

        public static TimeSlot Parse(string value, TimeSlot fallback)
        {
            if (!String.IsNullOrEmpty(value))
            {
                TimeSlot parsed;
                if (Enum.TryParse<TimeSlot>(value, true, out parsed)) return parsed;
                for (int i = 0; i < Names.Length; i++)
                {
                    if (value == Names[i]) return (TimeSlot)i;
                }
            }
            return fallback;
        }

        public static TimeSlot Current()
        {
            return FromTime(DateTime.Now);
        }

        public static TimeSlot FromTime(DateTime time)
        {
            int hour = time.Hour;
            if (hour >= 5 && hour < 9) return TimeSlot.Dawn;
            if (hour >= 9 && hour < 17) return TimeSlot.Day;
            if (hour >= 17 && hour < 20) return TimeSlot.Dusk;
            return TimeSlot.Night;
        }

        public static string Range(TimeSlot slot)
        {
            switch (slot)
            {
                case TimeSlot.Dawn: return "05:00 – 09:00";
                case TimeSlot.Day: return "09:00 – 17:00";
                case TimeSlot.Dusk: return "17:00 – 20:00";
                default: return "20:00 – 05:00";
            }
        }

        public static string Suggestion(TimeSlot slot)
        {
            switch (slot)
            {
                case TimeSlot.Dawn: return "Warm morning light, soft and low contrast";
                case TimeSlot.Day: return "Bright, open daylight with full color";
                case TimeSlot.Dusk: return "Warm orange, magenta, or low-angle evening light";
                default: return "Dark sky, moonlight, city lights, and neon";
            }
        }
    }

    public static class WeatherKindInfo
    {
        private static readonly string[] Names =
        {
            "Clear", "Clear Night", "Cloudy", "Overcast", "Rain", "Storm", "Snow", "Fog"
        };

        private static readonly string[] English =
        {
            "CLEAR DAY", "CLEAR NIGHT", "CLOUDY", "OVERCAST", "RAIN", "THUNDER", "SNOW", "FOG"
        };

        private static readonly string[] Short =
        {
            "Sun", "Night", "Cloud", "Grey", "Rain", "Storm", "Snow", "Fog"
        };

        public static string Name(WeatherKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < Names.Length ? Names[i] : "Unknown";
        }

        public static string EnglishName(WeatherKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < English.Length ? English[i] : "UNKNOWN";
        }

        public static string ShortName(WeatherKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < Short.Length ? Short[i] : "?";
        }

        public static WeatherKind Parse(string value, WeatherKind fallback)
        {
            if (!String.IsNullOrEmpty(value))
            {
                WeatherKind parsed;
                if (Enum.TryParse<WeatherKind>(value, true, out parsed)) return parsed;
                for (int i = 0; i < Names.Length; i++)
                {
                    if (value == Names[i] || value == Short[i]) return (WeatherKind)i;
                }
            }
            return fallback;
        }

        public static WeatherKind[] All
        {
            get
            {
                return new[]
                {
                    WeatherKind.ClearDay, WeatherKind.ClearNight, WeatherKind.Cloudy,
                    WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Storm,
                    WeatherKind.Snow, WeatherKind.Fog
                };
            }
        }

        public static string Suggestion(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.ClearDay: return "Bright, warm, clear sky";
                case WeatherKind.ClearNight: return "Dark sky, moonlight, and neon highlights";
                case WeatherKind.Cloudy: return "Soft clouds and muted colors";
                case WeatherKind.Overcast: return "Quiet grey-blue atmosphere";
                case WeatherKind.Rain: return "Cool blue-grey tones and wet surfaces";
                case WeatherKind.Storm: return "High-contrast, dramatic darkness";
                case WeatherKind.Snow: return "Cold white, crisp snow tones";
                case WeatherKind.Fog: return "Low-contrast, hazy atmosphere";
                default: return "A scene that fits the sky outside";
            }
        }
    }

    public class LocationInfo
    {
        public string Name { get; set; }
        public string Admin1 { get; set; }
        public string District { get; set; }
        public string Country { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Timezone { get; set; }
        public bool IsApproximate { get; set; }
        public string Source { get; set; }

        public LocationInfo()
        {
            Name = "";
            Admin1 = "";
            District = "";
            Country = "";
            Timezone = "Asia/Shanghai";
            Source = "";
        }

        [ScriptIgnore]
        public string DisplayName
        {
            get
            {
                if (!String.IsNullOrEmpty(District) && District != Name) return Name + " · " + District;
                if (String.IsNullOrEmpty(Admin1) || Admin1 == Name) return Name;
                return Name + " · " + Admin1;
            }
        }

        [ScriptIgnore]
        public string CityDistrict
        {
            get
            {
                if (!String.IsNullOrEmpty(District) && District != Name) return Name + " · " + District;
                return Name;
            }
        }

        [ScriptIgnore]
        public string ShortDisplay
        {
            get
            {
                if (String.IsNullOrEmpty(Name)) return "Unknown location";
                return Name;
            }
        }

        public string CoordinateText
        {
            get
            {
                return Latitude.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
                    + ", " + Longitude.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    public class HourlyForecast
    {
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
        public int WeatherCode { get; set; }
        public WeatherKind Kind { get; set; }
        public double PrecipitationProbability { get; set; }
        public bool IsDay { get; set; }
    }

    public class DailyForecast
    {
        public DateTime Date { get; set; }
        public int WeatherCode { get; set; }
        public WeatherKind Kind { get; set; }
        public string Description { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double PrecipitationProbability { get; set; }
        public DateTime? Sunrise { get; set; }
        public DateTime? Sunset { get; set; }

        public DailyForecast()
        {
            Description = "";
        }

        [ScriptIgnore]
        public string Weekday
        {
            get
            {
                string[] names = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
                return names[(int)Date.DayOfWeek];
            }
        }
    }

    public class CurrentWeather
    {
        public DateTime ObservedAt { get; set; }
        public double Temperature { get; set; }
        public double ApparentTemperature { get; set; }
        public double Humidity { get; set; }
        public double WindSpeed { get; set; }
        public double Precipitation { get; set; }
        public double PrecipitationProbability { get; set; }
        public double CloudCover { get; set; }
        public int WeatherCode { get; set; }
        public WeatherKind Kind { get; set; }
        public string Description { get; set; }
        public bool IsDay { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public DateTime? Sunrise { get; set; }
        public DateTime? Sunset { get; set; }

        public CurrentWeather()
        {
            Description = "";
            Kind = WeatherKind.ClearDay;
            IsDay = true;
        }
    }

    public class WeatherData
    {
        public LocationInfo Location { get; set; }
        public CurrentWeather Current { get; set; }
        public List<HourlyForecast> Hourly { get; set; }
        public List<DailyForecast> Daily { get; set; }
        public DateTime FetchedAt { get; set; }
        public string Provider { get; set; }
        public bool FromCache { get; set; }

        public WeatherData()
        {
            Hourly = new List<HourlyForecast>();
            Daily = new List<DailyForecast>();
            Provider = "Open-Meteo";
        }
    }

    public class WallpaperItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public string ProjectFile { get; set; }
        public string FilePath { get; set; }
        public string LaunchPath { get; set; }
        public string PreviewPath { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        public bool IsVideo { get; set; }
        public bool IsWeb { get; set; }
        public bool IsActive { get; set; }
        public bool IsRemote { get; set; }
        public bool IsInstalled { get; set; }
        public int WorkshopPage { get; set; }
        public int WorkshopRank { get; set; }
        public int WorkshopVotes { get; set; }
        public int WorkshopSubscriptions { get; set; }
        public string Author { get; set; }
        public string RemotePreviewUrl { get; set; }
        public WeatherKind PrimaryWeather { get; set; }
        public double Confidence { get; set; }
        public Dictionary<WeatherKind, double> Scores { get; set; }
        public Dictionary<WeatherKind, double> AutoScores { get; set; }
        public WeatherKind AutoPrimaryWeather { get; set; }
        public double AutoConfidence { get; set; }
        public string AutoReason { get; set; }
        public TimeSlot PrimaryTime { get; set; }
        public double TimeConfidence { get; set; }
        public Dictionary<TimeSlot, double> TimeScores { get; set; }
        public Dictionary<TimeSlot, double> AutoTimeScores { get; set; }
        public TimeSlot AutoPrimaryTime { get; set; }
        public double AutoTimeConfidence { get; set; }
        public string TimeReason { get; set; }
        public string AutoTimeReason { get; set; }
        public Nullable<int> ManualKind { get; set; }
        public bool Hidden { get; set; }
        public bool IsSuitable { get; set; }
        public bool CharacterFocus { get; set; }
        public bool HasPeople { get; set; }
        public bool PeopleScanned { get; set; }
        public bool BlankPreview { get; set; }
        public bool PreviewQualityChecked { get; set; }
        public AgeRating AgeRating { get; set; }
        public string ExcludeReason { get; set; }
        public string Reason { get; set; }
        public double Brightness { get; set; }
        public double Saturation { get; set; }
        public double Warmth { get; set; }
        public double Coolness { get; set; }
        public double Contrast { get; set; }
        public long DirectorySize { get; set; }
        public string WorkshopUrl { get; set; }

        public WallpaperItem()
        {
            Title = "";
            Type = "";
            Category = "";
            Description = "";
            Tags = new List<string>();
            Scores = new Dictionary<WeatherKind, double>();
            AutoScores = new Dictionary<WeatherKind, double>();
            TimeScores = new Dictionary<TimeSlot, double>();
            AutoTimeScores = new Dictionary<TimeSlot, double>();
            Reason = "";
            AutoReason = "";
            ExcludeReason = "";
            IsSuitable = true;
            AgeRating = AgeRating.Everyone;
            TimeReason = "";
            AutoTimeReason = "";
            PrimaryWeather = WeatherKind.ClearDay;
            PrimaryTime = TimeSlot.Day;
            AutoPrimaryWeather = WeatherKind.ClearDay;
            AutoPrimaryTime = TimeSlot.Day;
            IsInstalled = true;
            Author = "";
            RemotePreviewUrl = "";
        }

        [ScriptIgnore]
        public bool IsManual
        {
            get { return ManualKind.HasValue; }
        }

        [ScriptIgnore]
        public WeatherKind MatchWeather
        {
            get
            {
                if (ManualKind.HasValue) return (WeatherKind)ManualKind.Value;
                return PrimaryWeather;
            }
        }

        [ScriptIgnore]
        public string TypeLabel
        {
            get
            {
                if (IsVideo) return "Video";
                if (IsWeb) return "Web";
                if (String.Equals(Type, "scene", StringComparison.OrdinalIgnoreCase)) return "Scene";
                return String.IsNullOrEmpty(Type) ? "Wallpaper" : Type;
            }
        }

        public double MatchScore(WeatherKind kind)
        {
            if (ManualKind.HasValue)
            {
                if ((int)kind == ManualKind.Value) return 1000.0;
                return -1000.0;
            }
            double score;
            if (Scores != null && Scores.TryGetValue(kind, out score)) return score;
            return 0.0;
        }

        public double VisualScore(WeatherKind kind)
        {
            double score;
            if (AutoScores != null && AutoScores.TryGetValue(kind, out score)) return score;
            return 0.0;
        }

        public double TimeScore(TimeSlot slot)
        {
            double score;
            if (TimeScores != null && TimeScores.TryGetValue(slot, out score)) return score;
            return 0.0;
        }

        public double VisualTimeScore(TimeSlot slot)
        {
            double score;
            if (AutoTimeScores != null && AutoTimeScores.TryGetValue(slot, out score)) return score;
            return 0.0;
        }

        [ScriptIgnore]
        public string TimeName
        {
            get { return TimeSlotInfo.Name(PrimaryTime); }
        }

        [ScriptIgnore]
        public string ActiveLabel
        {
            get { return IsActive ? "Playing on desktop" : ""; }
        }
    }

    public class AppSettings
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HasLocation { get; set; }
        public string City { get; set; }
        public string Admin1 { get; set; }
        public string District { get; set; }
        public string Country { get; set; }
        public string Timezone { get; set; }
        public bool LocationApproximate { get; set; }
        public bool LocationLocked { get; set; }
        public List<LocationInfo> LocationHistory { get; set; }

        public bool AutoFollow { get; set; }
        public int RefreshMinutes { get; set; }
        public int RecommendationRotateMinutes { get; set; }
        public bool RecommendationRotateEnabled { get; set; }
        public bool UseTimeOfDay { get; set; }
        public bool WorkshopTopEnabled { get; set; }
        public bool WorkshopGridView { get; set; }
        public bool WorkshopSortBySubscriptions { get; set; }
        public int WorkshopRefreshDays { get; set; }
        public bool WorkshopAutoRefresh { get; set; }
        public bool WorkshopRefreshOnStartup { get; set; }
        public bool PreferScenery { get; set; }
        public int AgeRatingMask { get; set; }
        public int WallpaperVolume { get; set; }
        public bool WallpaperMuted { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool StartWithWindows { get; set; }
        public bool ShowNotifications { get; set; }

        public bool HasAppliedOnce { get; set; }
        public string LastWeatherKind { get; set; }
        public string LastTimeSlot { get; set; }
        public string LastAppliedId { get; set; }
        public string LastAppliedKind { get; set; }
        public DateTime? LastWeatherFetch { get; set; }
        public string OriginalWallpaperId { get; set; }
        public string OriginalWallpaperPath { get; set; }
        public string WallpaperEnginePath { get; set; }

        public Dictionary<string, int> ManualKinds { get; set; }
        public List<string> HiddenIds { get; set; }
        public List<string> BlockedIds { get; set; }
        public List<string> WhitelistIds { get; set; }

        public AppSettings()
        {
            City = "";
            Admin1 = "";
            District = "";
            Country = "";
            Timezone = "Asia/Shanghai";
            LocationLocked = false;
            LocationHistory = new List<LocationInfo>();
            AutoFollow = true;
            RefreshMinutes = 60;
            RecommendationRotateMinutes = 0;
            RecommendationRotateEnabled = false;
            UseTimeOfDay = true;
            WorkshopTopEnabled = true;
            WorkshopGridView = true;
            WorkshopSortBySubscriptions = true;
            WorkshopRefreshDays = 7;
            WorkshopAutoRefresh = true;
            WorkshopRefreshOnStartup = false;
            PreferScenery = true;
            AgeRatingMask = 1;
            WallpaperVolume = 100;
            WallpaperMuted = false;
            MinimizeToTray = true;
            StartWithWindows = false;
            ShowNotifications = true;
            LastWeatherKind = "";
            LastTimeSlot = "";
            LastAppliedId = "";
            LastAppliedKind = "";
            OriginalWallpaperId = "";
            OriginalWallpaperPath = "";
            WallpaperEnginePath = "";
            ManualKinds = new Dictionary<string, int>();
            HiddenIds = new List<string>();
            BlockedIds = new List<string>();
            WhitelistIds = new List<string>();
        }

        public AppSettings Clone()
        {
            return AppSettingsStore.Clone(this);
        }
    }

    public class AppStatusEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public bool IsError { get; private set; }

        public AppStatusEventArgs(string message, bool isError)
        {
            Message = message;
            IsError = isError;
        }
    }

    public class WallpaperAppliedEventArgs : EventArgs
    {
        public WallpaperItem Wallpaper { get; private set; }
        public bool Automatic { get; private set; }
        public string Reason { get; private set; }

        public WallpaperAppliedEventArgs(WallpaperItem wallpaper, bool automatic, string reason)
        {
            Wallpaper = wallpaper;
            Automatic = automatic;
            Reason = reason;
        }
    }
}

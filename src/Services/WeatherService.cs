using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SkyMatch
{
    public static class Http
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.DefaultConnectionLimit = 8;
            }
            catch
            {
            }
            HttpClientHandler handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip;
            handler.UseCookies = true;
            handler.AllowAutoRedirect = true;
            HttpClient client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                + "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 SkyMatch/1.2");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,application/json;q=0.8,*/*;q=0.7");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language",
                "en-US,en;q=0.9,zh-CN;q=0.7,zh;q=0.6");
            return client;
        }

        public static async Task<string> GetStringAsync(string url, int timeoutSeconds)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                using (HttpResponseMessage response = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException("HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);
                    }
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task<Dictionary<string, object>> GetJsonObjectAsync(string url, int timeoutSeconds)
        {
            string text = await GetStringAsync(url, timeoutSeconds).ConfigureAwait(false);
            Dictionary<string, object> result = Json.Object(Json.Parse(text));
            if (result == null) throw new InvalidOperationException("The interface returns not a valid JSON object");
            return result;
        }

        public static async Task<byte[]> GetBytesAsync(string url, int timeoutSeconds)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                using (HttpResponseMessage response = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException("HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);
                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
        }

        public static string Escape(string value)
        {
            if (value == null) return "";
            return Uri.EscapeDataString(value);
        }

        public static string Invariant(double value, int decimals)
        {
            return value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }

    public class LocationResolutionException : Exception
    {
        public LocationResolutionException(string message) : base(message)
        {
        }

        public LocationResolutionException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public static class WeatherCodes
    {
        public static string Describe(int code)
        {
            if (code == 0) return "Clear";
            if (code == 1) return "Mostly clear";
            if (code == 2) return "Partly cloudy";
            if (code == 3) return "Overcast";
            if (code == 45 || code == 48) return "Fog";
            if (code == 51 || code == 53 || code == 55) return "Drizzle";
            if (code == 56 || code == 57) return "Freezing drizzle";
            if (code == 61) return "Light rain";
            if (code == 63) return "Rain";
            if (code == 65) return "Heavy rain";
            if (code == 66 || code == 67) return "Freezing rain";
            if (code == 71) return "Light snow";
            if (code == 73) return "Snow";
            if (code == 75) return "Heavy snow";
            if (code == 77) return "Snow grains";
            if (code == 80) return "Rain showers";
            if (code == 81) return "Heavy showers";
            if (code == 82) return "Violent showers";
            if (code == 85) return "Snow showers";
            if (code == 86) return "Heavy snow showers";
            if (code == 95) return "Thunderstorm";
            if (code == 96 || code == 99) return "Thunderstorm with hail";
            return "Changing weather";
        }

        public static WeatherKind Kind(int code, bool isDay)
        {
            if (code == 45 || code == 48) return WeatherKind.Fog;
            if (code >= 95) return WeatherKind.Storm;
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) return WeatherKind.Snow;
            if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return WeatherKind.Rain;
            if (code == 3) return WeatherKind.Overcast;
            if (code == 2) return WeatherKind.Cloudy;
            if (code == 0 || code == 1) return isDay ? WeatherKind.ClearDay : WeatherKind.ClearNight;
            return isDay ? WeatherKind.Cloudy : WeatherKind.ClearNight;
        }
    }

    public class WeatherService
    {
        public async Task<LocationInfo> LocateByIpAsync()
        {
            List<string> errors = new List<string>();

            try
            {
                LocationInfo loc = await TryFreeIpApiAsync().ConfigureAwait(false);
                if (loc != null) return loc;
            }
            catch (Exception ex) { errors.Add("freeipapi: " + ex.Message); }

            try
            {
                LocationInfo loc = await TryIpApiAsync().ConfigureAwait(false);
                if (loc != null) return loc;
            }
            catch (Exception ex) { errors.Add("ip-api: " + ex.Message); }

            try
            {
                LocationInfo loc = await TryIpWhoAsync().ConfigureAwait(false);
                if (loc != null) return loc;
            }
            catch (Exception ex) { errors.Add("ipwho: " + ex.Message); }

            throw new LocationResolutionException("Unable to get location by IP, please search for city manually." +
                (errors.Count > 0 ? "（" + String.Join("；", errors.ToArray()) + "）" : ""));
        }

        private async Task<LocationInfo> TryFreeIpApiAsync()
        {
            Dictionary<string, object> json = await Http.GetJsonObjectAsync("https://freeipapi.com/api/json", 10).ConfigureAwait(false);
            double lat = Json.GetDouble(json, "latitude", 0);
            double lon = Json.GetDouble(json, "longitude", 0);
            string city = Json.GetString(json, "cityName");
            if (lat == 0 && lon == 0) return null;
            if (String.IsNullOrEmpty(city)) city = Json.GetString(json, "regionName");
            if (String.IsNullOrEmpty(city)) return null;
            LocationInfo location = new LocationInfo();
            location.Name = city;
            location.Admin1 = Json.GetString(json, "regionName");
            location.Country = Json.GetString(json, "countryName");
            location.Latitude = lat;
            location.Longitude = lon;
            location.Timezone = GetStringOr(json, "timeZone", "Asia/Shanghai");
            if (String.IsNullOrEmpty(location.Timezone)) location.Timezone = "Asia/Shanghai";
            location.IsApproximate = true;
            location.Source = "IP";
            return await EnrichLocationAsync(location).ConfigureAwait(false);
        }

        private async Task<LocationInfo> TryIpApiAsync()
        {
            Dictionary<string, object> json = await Http.GetJsonObjectAsync(
                "http://ip-api.com/json/?lang=en&fields=status,message,country,regionName,city,lat,lon,timezone,isp",
                10).ConfigureAwait(false);
            if (Json.GetString(json, "status") != "success") return null;
            double lat = Json.GetDouble(json, "lat", 0);
            double lon = Json.GetDouble(json, "lon", 0);
            string city = Json.GetString(json, "city");
            if (lat == 0 && lon == 0 || String.IsNullOrEmpty(city)) return null;
            LocationInfo location = new LocationInfo();
            location.Name = city;
            location.Admin1 = Json.GetString(json, "regionName");
            location.Country = Json.GetString(json, "country");
            location.Latitude = lat;
            location.Longitude = lon;
            location.Timezone = GetStringOr(json, "timezone", "Asia/Shanghai");
            location.IsApproximate = true;
            location.Source = "IP";
            return await EnrichLocationAsync(location).ConfigureAwait(false);
        }

        private async Task<LocationInfo> TryIpWhoAsync()
        {
            Dictionary<string, object> json = await Http.GetJsonObjectAsync("https://ipwho.is/", 10).ConfigureAwait(false);
            if (!Json.GetBool(json, "success", false)) return null;
            double lat = Json.GetDouble(json, "latitude", 0);
            double lon = Json.GetDouble(json, "longitude", 0);
            string city = Json.GetString(json, "city");
            if (lat == 0 && lon == 0 || String.IsNullOrEmpty(city)) return null;
            LocationInfo location = new LocationInfo();
            location.Name = city;
            location.Admin1 = Json.GetString(json, "region");
            location.Country = Json.GetString(json, "country");
            location.Latitude = lat;
            location.Longitude = lon;
            location.Timezone = Json.GetNestedString(json, "timezone", "id");
            if (String.IsNullOrEmpty(location.Timezone)) location.Timezone = "Asia/Shanghai";
            location.IsApproximate = true;
            location.Source = "IP";
            return await EnrichLocationAsync(location).ConfigureAwait(false);
        }

        public async Task<LocationInfo> EnrichLocationAsync(LocationInfo location)
        {
            try
            {
                string url = "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude="
                    + Http.Invariant(location.Latitude, 6) + "&longitude=" + Http.Invariant(location.Longitude, 6)
                    + "&localityLanguage=en";
                Dictionary<string, object> json = await Http.GetJsonObjectAsync(url, 8).ConfigureAwait(false);
                string city = Json.GetString(json, "city");
                string locality = Json.GetString(json, "locality");
                string province = Json.GetString(json, "principalSubdivision");
                string country = Json.GetString(json, "countryName");
                if (!String.IsNullOrEmpty(city)) location.Name = city;
                else if (!String.IsNullOrEmpty(locality)) location.Name = locality;
                if (!String.IsNullOrEmpty(province)) location.Admin1 = province;
                if (!String.IsNullOrEmpty(country)) location.Country = country;

                string district = locality;
                if (district == location.Name) district = "";
                if (String.IsNullOrEmpty(district))
                {
                    List<Dictionary<string, object>> admin = Json.ObjectList(
                        Json.GetObject(json, "localityInfo"), "administrative");
                    foreach (Dictionary<string, object> entry in admin)
                    {
                        string name = Json.GetString(entry, "name");
                        int level = Json.GetInt(entry, "adminLevel", 0);
                        if (String.IsNullOrEmpty(name) || name == location.Name || name == province) continue;
                        if (level >= 6 || name.EndsWith("district", StringComparison.Ordinal)
                            || name.EndsWith("county", StringComparison.Ordinal)
                            || name.EndsWith("flag", StringComparison.Ordinal))
                        {
                            district = name;
                            break;
                        }
                    }
                }
                if (!String.IsNullOrEmpty(district)) location.District = district;
            }
            catch (Exception ex)
            {
                Log.Warn("Reverse geocoding fails, retaining original place names:" + ex.Message);
            }
            return location;
        }

        public async Task<List<LocationInfo>> SearchCitiesAsync(string query)
        {
            List<LocationInfo> result = new List<LocationInfo>();
            if (String.IsNullOrWhiteSpace(query)) return result;
            string url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Http.Escape(query.Trim())
                + "&count=8&language=en&format=json";
            Dictionary<string, object> json = await Http.GetJsonObjectAsync(url, 12).ConfigureAwait(false);
            List<Dictionary<string, object>> items = Json.ObjectList(json, "results");
            foreach (Dictionary<string, object> item in items)
            {
                string name = Json.GetString(item, "name");
                if (String.IsNullOrEmpty(name)) continue;
                LocationInfo loc = new LocationInfo();
                loc.Name = name;
                loc.Admin1 = Json.GetString(item, "admin1");
                loc.Country = Json.GetString(item, "country");
                loc.Latitude = Json.GetDouble(item, "latitude", 0);
                loc.Longitude = Json.GetDouble(item, "longitude", 0);
                loc.Timezone = GetStringOr(item, "timezone", "Asia/Shanghai");
                loc.IsApproximate = false;
                loc.Source = "City search";
                bool duplicate = false;
                foreach (LocationInfo existing in result)
                {
                    if (Math.Abs(existing.Latitude - loc.Latitude) < 0.01 &&
                        Math.Abs(existing.Longitude - loc.Longitude) < 0.01)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) result.Add(loc);
            }
            return result;
        }

        public async Task<WeatherData> FetchWeatherAsync(LocationInfo location)
        {
            if (location == null) throw new ArgumentNullException("location");
            string url = "https://api.open-meteo.com/v1/forecast?latitude="
                + Http.Invariant(location.Latitude, 6) + "&longitude=" + Http.Invariant(location.Longitude, 6)
                + "&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,precipitation,weather_code,cloud_cover,wind_speed_10m"
                + "&hourly=temperature_2m,weather_code,precipitation_probability,is_day"
                + "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,precipitation_probability_max"
                + "&timezone=auto&forecast_days=8";
            Dictionary<string, object> json = await Http.GetJsonObjectAsync(url, 22).ConfigureAwait(false);
            Dictionary<string, object> current = Json.GetObject(json, "current");
            Dictionary<string, object> daily = Json.GetObject(json, "daily");
            Dictionary<string, object> hourly = Json.GetObject(json, "hourly");
            if (current == null || daily == null) throw new InvalidOperationException("The weather interface is missing current/daily data");

            WeatherData data = new WeatherData();
            data.Location = location;
            data.FetchedAt = DateTime.Now;
            data.Provider = "Open-Meteo";
            data.FromCache = false;

            CurrentWeather cw = new CurrentWeather();
            cw.ObservedAt = ParseDateTime(Json.GetString(current, "time"), DateTime.Now);
            cw.Temperature = Json.GetDouble(current, "temperature_2m", 0);
            cw.ApparentTemperature = Json.GetDouble(current, "apparent_temperature", cw.Temperature);
            cw.Humidity = Json.GetDouble(current, "relative_humidity_2m", 0);
            cw.WindSpeed = Json.GetDouble(current, "wind_speed_10m", 0);
            cw.Precipitation = Json.GetDouble(current, "precipitation", 0);
            cw.CloudCover = Json.GetDouble(current, "cloud_cover", 0);
            cw.WeatherCode = Json.GetInt(current, "weather_code", 0);
            cw.IsDay = Json.GetInt(current, "is_day", 1) == 1;
            cw.Kind = WeatherCodes.Kind(cw.WeatherCode, cw.IsDay);
            cw.Description = WeatherCodes.Describe(cw.WeatherCode);
            data.Current = cw;

            List<object> dailyTimes = Json.GetArray(daily, "time");
            List<object> dailyCodes = Json.GetArray(daily, "weather_code");
            List<object> dailyMax = Json.GetArray(daily, "temperature_2m_max");
            List<object> dailyMin = Json.GetArray(daily, "temperature_2m_min");
            List<object> dailyPrecip = Json.GetArray(daily, "precipitation_probability_max");
            List<object> dailySunrise = Json.GetArray(daily, "sunrise");
            List<object> dailySunset = Json.GetArray(daily, "sunset");
            for (int i = 0; i < dailyTimes.Count; i++)
            {
                DailyForecast day = new DailyForecast();
                day.Date = ParseDate(Convert.ToString(dailyTimes[i]), DateTime.Today.AddDays(i));
                day.WeatherCode = GetIntAt(dailyCodes, i, 0);
                day.Kind = WeatherCodes.Kind(day.WeatherCode, true);
                day.Description = WeatherCodes.Describe(day.WeatherCode);
                day.Max = GetDoubleAt(dailyMax, i, 0);
                day.Min = GetDoubleAt(dailyMin, i, 0);
                day.PrecipitationProbability = GetDoubleAt(dailyPrecip, i, 0);
                day.Sunrise = ParseNullableDateTime(GetStringAt(dailySunrise, i));
                day.Sunset = ParseNullableDateTime(GetStringAt(dailySunset, i));
                data.Daily.Add(day);
            }
            if (data.Daily.Count > 0)
            {
                cw.High = data.Daily[0].Max;
                cw.Low = data.Daily[0].Min;
                cw.Sunrise = data.Daily[0].Sunrise;
                cw.Sunset = data.Daily[0].Sunset;
                cw.PrecipitationProbability = data.Daily[0].PrecipitationProbability;
            }

            if (hourly != null)
            {
                List<object> hTimes = Json.GetArray(hourly, "time");
                List<object> hTemps = Json.GetArray(hourly, "temperature_2m");
                List<object> hCodes = Json.GetArray(hourly, "weather_code");
                List<object> hPrecip = Json.GetArray(hourly, "precipitation_probability");
                List<object> hDay = Json.GetArray(hourly, "is_day");
                DateTime start = cw.ObservedAt.AddMinutes(-35);
                int added = 0;
                double nearestPrecip = 0;
                double nearestDelta = Double.MaxValue;
                for (int i = 0; i < hTimes.Count && added < 24; i++)
                {
                    DateTime t = ParseDateTime(GetStringAt(hTimes, i), DateTime.MinValue);
                    if (t == DateTime.MinValue || t < start) continue;
                    HourlyForecast h = new HourlyForecast();
                    h.Time = t;
                    h.Temperature = GetDoubleAt(hTemps, i, cw.Temperature);
                    h.WeatherCode = GetIntAt(hCodes, i, 0);
                    h.PrecipitationProbability = GetDoubleAt(hPrecip, i, 0);
                    h.IsDay = GetIntAt(hDay, i, 1) == 1;
                    h.Kind = WeatherCodes.Kind(h.WeatherCode, h.IsDay);
                    data.Hourly.Add(h);
                    added++;
                    double delta = Math.Abs((t - cw.ObservedAt).TotalMinutes);
                    if (delta < nearestDelta)
                    {
                        nearestDelta = delta;
                        nearestPrecip = h.PrecipitationProbability;
                    }
                }
                if (nearestDelta < Double.MaxValue)
                    cw.PrecipitationProbability = Math.Max(cw.PrecipitationProbability, nearestPrecip);
            }

            return data;
        }

        private static string GetStringOr(Dictionary<string, object> source, string key, string fallback)
        {
            string value = Json.GetString(source, key);
            return String.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string GetStringAt(List<object> list, int index)
        {
            if (list == null || index < 0 || index >= list.Count || list[index] == null) return "";
            return Convert.ToString(list[index], CultureInfo.InvariantCulture);
        }

        private static double GetDoubleAt(List<object> list, int index, double fallback)
        {
            if (list == null || index < 0 || index >= list.Count || list[index] == null) return fallback;
            try { return Convert.ToDouble(list[index], CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static int GetIntAt(List<object> list, int index, int fallback)
        {
            return (int)Math.Round(GetDoubleAt(list, index, fallback));
        }

        public static DateTime ParseDateTime(string value, DateTime fallback)
        {
            if (String.IsNullOrEmpty(value)) return fallback;
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return parsed;
            return fallback;
        }

        public static DateTime ParseDate(string value, DateTime fallback)
        {
            if (String.IsNullOrEmpty(value)) return fallback;
            DateTime parsed;
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsed)) return parsed;
            return ParseDateTime(value, fallback);
        }

        private static DateTime? ParseNullableDateTime(string value)
        {
            if (String.IsNullOrEmpty(value)) return null;
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed;
            return null;
        }
    }
}

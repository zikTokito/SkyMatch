using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SkyMatch
{
    public static class Program
    {
        public static readonly int ShowWindowMessage = (int)RegisterWindowMessage("SkyMatch.ShowExistingWindow");

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string message);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [STAThread]
        public static void Main(string[] args)
        {
            bool diagnostic = false;
            string diagnosticPath = "";
            bool minimized = false;
            string detailId = "";
            int initialPage = 0;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (String.Equals(arg, "--diagnose", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostic = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        diagnosticPath = args[++i];
                    }
                }
                else if (String.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    minimized = true;
                }
                else if (String.Equals(arg, "--detail", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) detailId = args[++i];
                }
                else if (String.Equals(arg, "--page", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) Int32.TryParse(args[++i], out initialPage);
                }
            }
            if (initialPage < 0 || initialPage > 4) initialPage = 0;

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }
            catch
            {
            }

            if (diagnostic)
            {
                int code = Diagnostics.Run(diagnosticPath);
                Environment.ExitCode = code;
                return;
            }

            bool created = false;
            Mutex mutex = new Mutex(true, "Local\\SkyMatch.SingleInstance", out created);
            if (!created)
            {
                // A second launch from the taskbar should bring the existing window to the front
                // instead of showing an error dialog.
                try
                {
                    PostMessage((IntPtr)0xFFFF, ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
                }
                catch
                {
                }
                return;
            }

            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                Log.Error("UI 线程异常", e.Exception);
                MessageBox.Show("SkyMatch 遇到错误：\r\n" + e.Exception.Message,
                    "SkyMatch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Error("未处理异常", e.ExceptionObject as Exception);
            };

            try
            {
                Application.Run(new MainForm(minimized, detailId, initialPage));
            }
            catch (Exception ex)
            {
                Log.Error("程序启动失败", ex);
                MessageBox.Show("SkyMatch 启动失败：\r\n" + ex.Message, "SkyMatch",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { mutex.ReleaseMutex(); }
                catch { }
                mutex.Dispose();
            }
        }
    }

    public static class Diagnostics
    {
        public static int Run(string outputPath)
        {
            try
            {
                if (String.IsNullOrEmpty(outputPath))
                    outputPath = Path.Combine(AppPaths.DataDirectory, "diagnostic.json");
                AppPaths.Ensure();
                AppSettings settings = AppSettingsStore.Load();
                WallpaperScanner scanner = new WallpaperScanner();
                WallpaperScanResult scan = scanner.Scan(settings);
                WallpaperClassifier.ClassifyAll(scan.Wallpapers, settings);

                Dictionary<string, object> report = new Dictionary<string, object>();
                report["generatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                report["enginePath"] = scan.WallpaperEnginePath;
                report["workshopPath"] = scan.WorkshopPath;
                report["activePath"] = scan.ActivePath;
                report["activeId"] = scan.ActiveId;
                report["count"] = scan.Wallpapers.Count;
                report["skipped"] = scan.SkippedProjects;
                report["error"] = scan.Error;

                List<object> wallpapers = new List<object>();
                foreach (WallpaperItem item in scan.Wallpapers)
                {
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    row["id"] = item.Id;
                    row["title"] = item.Title;
                    row["type"] = item.TypeLabel;
                    row["primary"] = WeatherKindInfo.Name(item.PrimaryWeather);
                    row["confidence"] = Math.Round(item.Confidence);
                    row["reason"] = item.Reason;
                    row["time"] = TimeSlotInfo.Name(item.PrimaryTime);
                    row["timeConfidence"] = Math.Round(item.TimeConfidence);
                    row["timeReason"] = item.TimeReason;
                    row["manual"] = item.IsManual;
                    row["active"] = item.IsActive;
                    row["brightness"] = Math.Round(item.Brightness, 2);
                    row["saturation"] = Math.Round(item.Saturation, 2);
                    row["warmth"] = Math.Round(item.Warmth, 2);
                    row["coolness"] = Math.Round(item.Coolness, 2);
                    Dictionary<string, object> scores = new Dictionary<string, object>();
                    foreach (WeatherKind kind in WeatherKindInfo.All)
                        scores[WeatherKindInfo.Name(kind)] = Math.Round(item.VisualScore(kind), 2);
                    row["scores"] = scores;
                    Dictionary<string, object> timeScores = new Dictionary<string, object>();
                    foreach (TimeSlot slot in TimeSlotInfo.All)
                        timeScores[TimeSlotInfo.Name(slot)] = Math.Round(item.VisualTimeScore(slot), 2);
                    row["timeScores"] = timeScores;
                    wallpapers.Add(row);
                }
                report["wallpapers"] = wallpapers;

                try
                {
                    WeatherService service = new WeatherService();
                    LocationInfo location;
                    if (settings.HasLocation)
                    {
                        location = new LocationInfo();
                        location.Name = settings.City;
                        location.Admin1 = settings.Admin1;
                        location.Country = settings.Country;
                        location.Latitude = settings.Latitude;
                        location.Longitude = settings.Longitude;
                        location.Timezone = settings.Timezone;
                    }
                    else
                    {
                        location = service.LocateByIpAsync().GetAwaiter().GetResult();
                    }
                    WeatherData weather = service.FetchWeatherAsync(location).GetAwaiter().GetResult();
                    Dictionary<string, object> current = new Dictionary<string, object>();
                    current["location"] = weather.Location.DisplayName;
                    current["temperature"] = weather.Current.Temperature;
                    current["description"] = weather.Current.Description;
                    current["kind"] = WeatherKindInfo.Name(weather.Current.Kind);
                    current["humidity"] = weather.Current.Humidity;
                    current["wind"] = weather.Current.WindSpeed;
                    current["observedAt"] = weather.Current.ObservedAt.ToString("yyyy-MM-dd HH:mm");
                    report["weather"] = current;
                }
                catch (Exception ex)
                {
                    report["weatherError"] = ex.Message;
                }

                string json = Json.Stringify(report);
                File.WriteAllText(outputPath, json, new UTF8Encoding(false));
                Log.Info("诊断报告已写入 " + outputPath);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("诊断失败", ex);
                try
                {
                    if (String.IsNullOrEmpty(outputPath)) outputPath = Path.Combine(AppPaths.DataDirectory, "diagnostic.json");
                    File.WriteAllText(outputPath, "{\"error\":" + Json.Stringify(ex.Message) + "}",
                        new UTF8Encoding(false));
                }
                catch { }
                return 1;
            }
        }
    }
}

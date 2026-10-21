using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SkyMatch
{
    public class AppController
    {
        private readonly WeatherService _weatherService;
        private readonly WallpaperScanner _scanner;
        private readonly SteamWorkshopService _workshopService;
        private readonly WallpaperAudioController _audio;
        private readonly object _sync = new object();
        private readonly Dictionary<string, bool> _characterCache = new Dictionary<string, bool>();
        private DateTime _lastLiveVolume = DateTime.MinValue;
        private string _workshopDirectory;
        private string _workshopStamp;
        private bool _scanning;
        private bool _previewBusy;
        private int _workshopBatchSerial;

        private AppSettings _settings;
        private WeatherData _weather;
        private List<WallpaperItem> _wallpapers;
        private List<WallpaperItem> _workshopItems;
        private WallpaperItem _recommended;
        private WallpaperEngineController _engine;
        private string _enginePath;
        private string _activePath;
        private string _activeId;
        private bool _busy;
        private bool _refreshingWeather;
        private bool _applying;
        private bool _autoSwitchDeferred;
        private DateTime _lastWallpaperChangeAt;
        private bool _checkingActive;
        private bool _workshopBusy;
        private bool _visionBusy;
        private bool _peopleScanBusy;
        private bool _previewQualityBusy;
        private VisionCacheFile _visionCache = new VisionCacheFile();
        private bool _cacheIncludesAllTags;
        private DateTime _lastAutoWorkshopRefresh = DateTime.MinValue;
        private DateTime _lastWorkshopCountdownDay = DateTime.MinValue;
        private string _status;
        private string _workshopStatus;
        private string _workshopError;
        private DateTime _workshopFetchedAt;
        private int _workshopPages;
        private DateTime _nextRefreshAt;
        private int _sessionSwitches;

        public event EventHandler WeatherUpdated;
        public event EventHandler WallpapersUpdated;
        public event EventHandler WorkshopUpdated;
        public event EventHandler StateChanged;
        public event EventHandler<AppStatusEventArgs> StatusChanged;
        public event EventHandler<WallpaperAppliedEventArgs> WallpaperApplied;
        public event EventHandler ActiveWallpaperChanged;

        public AppController()
        {
            _weatherService = new WeatherService();
            _scanner = new WallpaperScanner();
            _workshopService = new SteamWorkshopService();
            _audio = new WallpaperAudioController();
            _settings = AppSettingsStore.Load();
            _wallpapers = new List<WallpaperItem>();
            _workshopItems = new List<WallpaperItem>();
            _status = "正在准备…";
            _workshopStatus = "";
            _workshopError = "";
            _nextRefreshAt = DateTime.MinValue;
            _lastWallpaperChangeAt = DateTime.Now;
        }

        public AppSettings Settings
        {
            get { return _settings; }
        }

        public WeatherData Weather
        {
            get { return _weather; }
        }

        public TimeSlot CurrentTimeSlot
        {
            get { return TimeSlotInfo.Current(); }
        }

        public IList<WallpaperItem> Wallpapers
        {
            get { return _wallpapers.AsReadOnly(); }
        }

        public WallpaperItem Recommended
        {
            get { return _recommended; }
        }

        public IList<WallpaperItem> WorkshopItems
        {
            get { return _workshopItems.AsReadOnly(); }
        }

        public bool WorkshopBusy
        {
            get { return _workshopBusy; }
        }

        public string WorkshopStatus
        {
            get { return _workshopStatus; }
        }

        public string WorkshopError
        {
            get { return _workshopError; }
        }

        public DateTime WorkshopFetchedAt
        {
            get { return _workshopFetchedAt; }
        }

        public int WorkshopPages
        {
            get { return _workshopPages; }
        }

        public bool WorkshopTopEnabled
        {
            get { return _settings.WorkshopTopEnabled; }
        }

        public int WorkshopDaysUntilRefresh
        {
            get
            {
                if (_workshopItems.Count == 0 || _workshopFetchedAt == default(DateTime)) return 0;
                int days = Math.Max(1, _settings.WorkshopRefreshDays);
                DateTime next = _workshopFetchedAt.Date.AddDays(days);
                return Math.Max(0, (next - DateTime.Today).Days);
            }
        }

        public bool IsWorkshopRefreshDue
        {
            get { return WorkshopDaysUntilRefresh <= 0; }
        }

        public bool WorkshopAutoRefresh
        {
            get { return _settings.WorkshopAutoRefresh; }
        }

        public bool IsClipAvailable
        {
            get { return ClipVisionClassifier.IsAvailableFor("clip_large"); }
        }

        public bool IsVisionBusy
        {
            get { return _visionBusy; }
        }

        public async Task ScanBlankPreviewsAsync()
        {
            if (_previewQualityBusy) return;
            _previewQualityBusy = true;
            try
            {
                List<WallpaperItem> targets = new List<WallpaperItem>();
                foreach (WallpaperItem item in _workshopItems)
                {
                    if (item.PreviewQualityChecked) continue;
                    if (String.IsNullOrEmpty(item.PreviewPath) || !File.Exists(item.PreviewPath)) continue;
                    targets.Add(item);
                }
                if (targets.Count == 0) return;
                await Task.Run(delegate
                {
                    foreach (WallpaperItem item in targets)
                    {
                        item.BlankPreview = PreviewQuality.IsSolidColor(item.PreviewPath);
                        item.PreviewQualityChecked = true;
                        if (item.BlankPreview)
                        {
                            item.IsSuitable = false;
                            item.ExcludeReason = "预览为单色块，已过滤";
                        }
                    }
                }).ConfigureAwait(true);
                SaveWorkshopCache();
                RaiseWorkshopUpdated();
                RaiseStateChanged();
            }
            catch (Exception ex)
            {
                Log.Warn("工坊黑屏预览扫描失败：" + ex.Message);
            }
            finally
            {
                _previewQualityBusy = false;
            }
        }

        public async Task ScanWorkshopPeopleAsync()
        {
            if (!_settings.PreferScenery || _peopleScanBusy || !PersonDetector.IsAvailable) return;
            _peopleScanBusy = true;
            try
            {
                List<WallpaperItem> targets = new List<WallpaperItem>();
                foreach (WallpaperItem item in _workshopItems)
                {
                    if (item.PeopleScanned) continue;
                    if (String.IsNullOrEmpty(item.PreviewPath) || !File.Exists(item.PreviewPath)) continue;
                    targets.Add(item);
                }
                if (targets.Count == 0) return;
                await Task.Run(delegate
                {
                    foreach (WallpaperItem item in targets)
                    {
                        item.HasPeople = PersonDetector.HasPerson(item.PreviewPath);
                        item.PeopleScanned = true;
                        StoreVisionEntry(item);
                    }
                }).ConfigureAwait(true);
                SaveWorkshopCache();
                SaveVisionCache();
                RaiseWorkshopUpdated();
                RaiseStateChanged();
            }
            catch (Exception ex)
            {
                Log.Warn("工坊人物扫描失败：" + ex.Message);
            }
            finally
            {
                _peopleScanBusy = false;
            }
        }

        public async Task ReclassifyVisionAsync()
        {
            if (_visionBusy) return;
            _visionBusy = true;
            try
            {
                SetStatus("正在用 CLIP L/14 全量重算匹配度…", false);
                await EnsureWorkshopPreviewsAsync(Math.Max(1, _workshopItems.Count)).ConfigureAwait(true);
                await ScanBlankPreviewsAsync().ConfigureAwait(true);

                List<WallpaperItem> local = new List<WallpaperItem>(_wallpapers);
                await Task.Run(delegate
                {
                    WallpaperClassifier.ClassifyAll(local, _settings);
                    foreach (WallpaperItem item in local) StoreVisionEntry(item);
                }).ConfigureAwait(true);

                List<WallpaperItem> targets = new List<WallpaperItem>();
                foreach (WallpaperItem item in _workshopItems)
                {
                    if (String.IsNullOrEmpty(item.PreviewPath) || !File.Exists(item.PreviewPath)) continue;
                    VisionCacheEntry cached;
                    if (_visionCache.Entries.TryGetValue(item.Id, out cached)
                        && VisionCacheStore.IsValid(cached, item)) continue;
                    targets.Add(item);
                }
                SetStatus("高精度识图：待处理 " + targets.Count + " 张壁纸", false);
                await Task.Run(delegate
                {
                    int done = 0;
                    foreach (WallpaperItem item in targets)
                    {
                        ClassifyWorkshopItem(item);
                        done++;
                        if ((done % 8) == 0)
                        {
                            lock (_visionCache)
                            {
                                VisionCacheStore.Save(_visionCache);
                            }
                        }
                    }
                }).ConfigureAwait(true);

                EnsureMinimumWeatherCounts(10);
                LinkWorkshopToLocal();
                SaveWorkshopCache();
                SaveVisionCache();
                UpdateRecommendation();
                RaiseWallpapersUpdated();
                RaiseWorkshopUpdated();
                RaiseStateChanged();
                SetStatus("全部壁纸已按最高精度算法重算", false);
            }
            catch (Exception ex)
            {
                Log.Error("重新识图失败", ex);
                SetStatus("识图失败：" + ex.Message, true);
            }
            finally
            {
                _visionBusy = false;
            }
        }

        private void EnsureMinimumWeatherCounts(int minimum)
        {
            Dictionary<WeatherKind, int> counts = new Dictionary<WeatherKind, int>();
            foreach (WeatherKind kind in WeatherKindInfo.All) counts[kind] = 0;
            foreach (WallpaperItem item in _workshopItems)
                counts[item.MatchWeather] = counts[item.MatchWeather] + 1;

            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                int missing = minimum - counts[kind];
                if (missing <= 0) continue;
                List<WallpaperItem> candidates = new List<WallpaperItem>();
                foreach (WallpaperItem item in _workshopItems)
                {
                    if (item.MatchWeather == kind) continue;
                    if (item.BlankPreview) continue;
                    if (counts[item.MatchWeather] <= minimum) continue;
                    double score;
                    if (item.Scores == null || !item.Scores.TryGetValue(kind, out score)) continue;
                    candidates.Add(item);
                }
                candidates.Sort(delegate(WallpaperItem a, WallpaperItem b)
                {
                    double sa = a.Scores.ContainsKey(kind) ? a.Scores[kind] : 0;
                    double sb = b.Scores.ContainsKey(kind) ? b.Scores[kind] : 0;
                    return sb.CompareTo(sa);
                });
                foreach (WallpaperItem item in candidates)
                {
                    if (missing <= 0) break;
                    counts[item.MatchWeather] = counts[item.MatchWeather] - 1;
                    item.AutoPrimaryWeather = kind;
                    item.PrimaryWeather = kind;
                    item.AutoReason = (item.AutoReason ?? "") + " · 按天气匹配分补足到 " + minimum + " 张";
                    item.Reason = item.AutoReason;
                    counts[kind] = counts[kind] + 1;
                    missing--;
                }
            }
        }

        public void SetWorkshopAutoRefresh(bool enabled)
        {
            _settings.WorkshopAutoRefresh = enabled;
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
            SetStatus(enabled ? "工坊自动抓取已开启" : "工坊自动抓取已关闭", false);
            if (enabled && IsWorkshopRefreshDue)
            {
                Task ignored = CheckWorkshopRefreshDueAsync();
            }
        }

        public void SetWorkshopRefreshDays(int days)
        {
            days = Math.Max(1, Math.Min(365, days));
            _settings.WorkshopRefreshDays = days;
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
            SetStatus("工坊抓取间隔已设为 " + days + " 天", false);
            if (_settings.WorkshopAutoRefresh && IsWorkshopRefreshDue)
            {
                Task ignored = CheckWorkshopRefreshDueAsync();
            }
        }

        public async Task CheckWorkshopRefreshDueAsync()
        {
            if (!_settings.WorkshopTopEnabled || !_settings.WorkshopAutoRefresh) return;
            if (_lastWorkshopCountdownDay.Date != DateTime.Today)
            {
                _lastWorkshopCountdownDay = DateTime.Today;
                RaiseStateChanged();
            }
            if (_workshopBusy || !IsWorkshopRefreshDue) return;
            if (_lastAutoWorkshopRefresh.Date == DateTime.Today) return;
            _lastAutoWorkshopRefresh = DateTime.Now;
            await RefreshWorkshopAsync(true).ConfigureAwait(true);
        }

        public int WallpaperVolume
        {
            get { return _settings.WallpaperVolume; }
        }

        public bool WallpaperMuted
        {
            get { return _settings.WallpaperMuted; }
        }

        public string EnginePath
        {
            get { return _enginePath; }
        }

        public string ActiveId
        {
            get { return _activeId; }
        }

        public string ActivePath
        {
            get { return _activePath; }
        }

        public bool IsBusy
        {
            get { return _busy; }
        }

        public bool IsRefreshingWeather
        {
            get { return _refreshingWeather; }
        }

        public bool IsEngineRunning
        {
            get { return _engine != null && _engine.IsRunning; }
        }

        public bool IsEngineAvailable
        {
            get { return _engine != null && _engine.IsAvailable; }
        }

        public string Status
        {
            get { return _status; }
        }

        public DateTime NextRefreshAt
        {
            get { return _nextRefreshAt; }
        }

        public int SessionSwitches
        {
            get { return _sessionSwitches; }
        }

        public WallpaperItem ActiveItem
        {
            get
            {
                if (_wallpapers == null) return null;
                foreach (WallpaperItem item in _wallpapers)
                    if (item.IsActive) return item;
                if (!String.IsNullOrEmpty(_activeId))
                {
                    foreach (WallpaperItem item in _wallpapers)
                        if (item.Id == _activeId) return item;
                }
                return null;
            }
        }

        public async Task InitializeAsync()
        {
            AppPaths.Ensure();
            Log.Info("SkyMatch 启动，数据目录：" + AppPaths.DataDirectory);
            _visionCache = VisionCacheStore.Load();
            SetStatus("正在读取本地天气缓存…", false);
            if (_settings.WorkshopTopEnabled)
            {
                bool cached = LoadWorkshopCache();
                if (cached)
                {
                    RaiseWorkshopUpdated();
                    RaiseStateChanged();
                }
                // Fetch only when there is no cache or the configured interval
                // has reached zero. Otherwise keep the cached catalogue and
                // show the countdown in the workshop/settings UI.
                if (_settings.WorkshopRefreshOnStartup)
                {
                    _lastAutoWorkshopRefresh = DateTime.Now;
                    Task ignored = RefreshWorkshopAsync(true);
                }
                else if (_settings.WorkshopAutoRefresh && (!cached || IsWorkshopRefreshDue))
                {
                    _lastAutoWorkshopRefresh = DateTime.Now;
                    Task ignored = RefreshWorkshopAsync(true);
                }
            }
            if (_settings.HasLocation)
            {
                WeatherData cached = WeatherCacheStore.Load();
                if (cached != null && SameLocation(cached.Location, _settings))
                {
                    cached.FromCache = true;
                    _weather = cached;
                    DateTime fetchedAt = cached.FetchedAt;
                    if (fetchedAt.Kind == DateTimeKind.Utc) fetchedAt = fetchedAt.ToLocalTime();
                    _nextRefreshAt = fetchedAt.AddHours(1);
                    if (cached.Daily == null || cached.Daily.Count < 8)
                        _nextRefreshAt = DateTime.MinValue;
                    UpdateRecommendation();
                    RaiseWeatherUpdated();
                    SetStatus("已载入上次的天气，每小时更新一次", false);
                }
            }

            await ScanWallpapersAsync().ConfigureAwait(true);

            if (!_settings.HasLocation)
            {
                await LocateByIpAsync().ConfigureAwait(true);
            }
            else
            {
                if (String.IsNullOrEmpty(_settings.District))
                {
                    try
                    {
                        LocationInfo current = BuildLocation();
                        await _weatherService.EnrichLocationAsync(current).ConfigureAwait(true);
                        if (!String.IsNullOrEmpty(current.District))
                        {
                            _settings.District = current.District;
                            AppSettingsStore.Save(_settings);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("旧位置补齐区县失败：" + ex.Message);
                    }
                }
                if (_weather == null)
                {
                    await RefreshWeatherAsync(false).ConfigureAwait(true);
                }
                else if (DateTime.Now >= _nextRefreshAt)
                {
                    Task weatherTask = RefreshWeatherAsync(false);
                }
                else
                {
                    SetStatus("天气已载入 · 下次更新 " + _nextRefreshAt.ToString("HH:mm"), false);
                }
            }

            if (_settings.AutoFollow) await ApplyStartupMatchAsync().ConfigureAwait(true);
        }

        public async Task ScanWallpapersAsync()
        {
            if (_scanning) return;
            _scanning = true;
            SetBusy(true);
            SetStatus("正在扫描 Wallpaper Engine 创意工坊…", false);
            try
            {
                WallpaperScanResult scan = await Task.Run(delegate
                {
                    return _scanner.Scan(_settings);
                }).ConfigureAwait(true);

                _enginePath = scan.WallpaperEnginePath;
                _workshopDirectory = scan.WorkshopPath;
                _engine = new WallpaperEngineController(_enginePath);
                if (!String.IsNullOrEmpty(_enginePath))
                {
                    _settings.WallpaperEnginePath = _enginePath;
                }
                _activePath = scan.ActivePath;
                _activeId = scan.ActiveId;
                _wallpapers = scan.Wallpapers;
                EnsureWhitelistFromLocal();
                if (_workshopItems.Count > 0)
                {
                    LinkWorkshopToLocal();
                    RaiseWorkshopUpdated();
                }
                if (!String.IsNullOrEmpty(scan.Error))
                {
                    SetStatus(scan.Error, true);
                }
                else
                {
                    SetStatus("发现 " + _wallpapers.Count + " 张可用壁纸，正在分析画面…", false);
                }

                if (String.IsNullOrEmpty(_settings.OriginalWallpaperPath) && !String.IsNullOrEmpty(_activePath))
                {
                    _settings.OriginalWallpaperPath = _activePath;
                    _settings.OriginalWallpaperId = _activeId;
                }

                int total = _wallpapers.Count;
                int index = 0;
                List<WallpaperItem> pendingClassification = new List<WallpaperItem>();
                IProgress<string> progress = new Progress<string>(delegate(string message)
                {
                    SetStatus(message, false);
                });
                await Task.Run(delegate
                {
                    foreach (WallpaperItem item in _wallpapers)
                    {
                        if (!TryApplyLocalVisionCache(item))
                        {
                            WallpaperClassification quick = WallpaperClassifier.ClassifyQuick(item);
                            WallpaperClassifier.SetAutoResult(item, quick);
                            WallpaperClassifier.ApplyManualOverride(item, _settings);
                            pendingClassification.Add(item);
                        }
                        index++;
                        if (index % 4 == 0 || index == total)
                            progress.Report("正在读取壁纸 " + index + " / " + total + "…");
                    }
                }).ConfigureAwait(true);

                MarkActiveItem();
                LinkWorkshopToLocal();
                _settings.WallpaperEnginePath = _enginePath;
                _workshopStamp = BuildWorkshopStamp();
                AppSettingsStore.Save(_settings);
                Task audioTask = ReapplyAudioAfterSwitchAsync();
                UpdateRecommendation();
                RaiseWallpapersUpdated();
                RaiseStateChanged();

                if (_wallpapers.Count > 0)
                {
                    List<WallpaperItem> snapshot = new List<WallpaperItem>(_wallpapers);
                    Task thumbnailTask = Task.Run(delegate { ThumbnailCache.BuildAll(snapshot); });
                }

                if (String.IsNullOrEmpty(scan.Error))
                {
                    SetStatus("已识别 " + _wallpapers.Count + " 张壁纸 · 排除 " + AutoExcludedCount
                        + " 张不适合项目", false);
                }
                if (pendingClassification.Count > 0)
                {
                    Task classifyTask = RefineLocalClassificationsAsync(pendingClassification);
                }
            }
            catch (Exception ex)
            {
                Log.Error("扫描壁纸失败", ex);
                SetStatus("扫描壁纸失败：" + ex.Message, true);
            }
            finally
            {
                _scanning = false;
                SetBusy(false);
            }
        }

        private async Task RefineLocalClassificationsAsync(List<WallpaperItem> items)
        {
            if (items == null || items.Count == 0) return;
            try
            {
                // Let the first page settle before loading CLIP/YOLO and doing
                // full inference for local wallpapers in the background.
                await Task.Delay(5000).ConfigureAwait(true);
                await Task.Run(delegate
                {
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    try
                    {
                        foreach (WallpaperItem item in items)
                        {
                            WallpaperClassification c = WallpaperClassifier.ClassifyOne(item, _settings);
                            WallpaperClassifier.SetAutoResult(item, c);
                            WallpaperClassifier.ApplyManualOverride(item, _settings);
                            StoreVisionEntry(item);
                        }
                    }
                    finally
                    {
                        Thread.CurrentThread.Priority = ThreadPriority.Normal;
                    }
                }).ConfigureAwait(true);
                SaveVisionCache();
                _settings.WallpaperEnginePath = _enginePath;
                AppSettingsStore.Save(_settings);
                UpdateRecommendation();
                RaiseWallpapersUpdated();
                RaiseStateChanged();
            }
            catch (Exception ex)
            {
                Log.Warn("补充识别本地壁纸失败：" + ex.Message);
            }
        }

        public void CheckActiveWallpaper()
        {
            if (_checkingActive || _applying || _engine == null || !_engine.IsAvailable) return;
            if ((DateTime.Now - _lastWallpaperChangeAt).TotalSeconds < 5) return;
            _checkingActive = true;
            try
            {
                string path = WallpaperScanner.ReadActiveWallpaperPath(_enginePath);
                if (String.IsNullOrEmpty(path) || WallpaperScanner.PathEquals(path, _activePath)) return;
                string newId = WallpaperScanner.ExtractWorkshopId(path);
                if (!String.IsNullOrEmpty(newId) && newId == _activeId)
                {
                    _activePath = path;
                    return;
                }
                _activePath = path;
                _activeId = newId;
                _lastWallpaperChangeAt = DateTime.Now;
                MarkActiveItem();
                UpdateWorkshopActiveState();
                SetStatus("已同步 Wallpaper Engine 当前壁纸", false);
                EventHandler handler = ActiveWallpaperChanged;
                if (handler != null) handler(this, EventArgs.Empty);
                RaiseStateChanged();
            }
            catch (Exception ex)
            {
                Log.Warn("同步 Wallpaper Engine 当前壁纸失败：" + ex.Message);
            }
            finally { _checkingActive = false; }
        }

        public async Task CheckWorkshopDownloadsAsync()
        {
            if (_scanning || String.IsNullOrEmpty(_workshopDirectory)) return;
            string stamp = BuildWorkshopStamp();
            if (String.IsNullOrEmpty(stamp)) return;
            if (String.IsNullOrEmpty(_workshopStamp))
            {
                _workshopStamp = stamp;
                return;
            }
            if (stamp == _workshopStamp) return;
            SetStatus("检测到工坊壁纸变化，正在刷新壁纸库…", false);
            await ScanWallpapersAsync().ConfigureAwait(true);
            if (!_scanning) SetStatus("已检测到新订阅的壁纸，壁纸库已更新", false);
        }

        private string BuildWorkshopStamp()
        {
            if (String.IsNullOrEmpty(_workshopDirectory)) return "";
            try
            {
                List<string> entries = new List<string>();
                foreach (string directory in Directory.GetDirectories(_workshopDirectory))
                {
                    string project = Path.Combine(directory, "project.json");
                    string stamp = Path.GetFileName(directory);
                    if (File.Exists(project))
                        stamp += ":" + File.GetLastWriteTimeUtc(project).Ticks;
                    entries.Add(stamp);
                }
                entries.Sort(StringComparer.OrdinalIgnoreCase);
                return String.Join("|", entries.ToArray());
            }
            catch
            {
                return "";
            }
        }

        public void SetWallpaperVolumeLive(int volume)
        {
            if (volume < 0) volume = 0;
            if (volume > 100) volume = 100;
            _settings.WallpaperVolume = volume;
            if (_settings.WallpaperMuted)
            {
                _settings.WallpaperMuted = false;
                _audio.SetMute(false);
            }
            DateTime now = DateTime.Now;
            if ((now - _lastLiveVolume).TotalMilliseconds < 45) return;
            _lastLiveVolume = now;
            _audio.SetVolume(volume);
        }

        public void SetWallpaperVolume(int volume)
        {
            if (volume < 0) volume = 0;
            if (volume > 100) volume = 100;
            bool unmute = _settings.WallpaperMuted;
            _settings.WallpaperVolume = volume;
            if (unmute)
            {
                _settings.WallpaperMuted = false;
                _audio.SetMute(false);
            }
            bool applied = _audio.SetVolume(volume);
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
            SetStatus(applied ? "壁纸音量已调整为 " + volume + "%" : "已保存音量（Wallpaper Engine 未运行）", false);
        }

        public void SetWallpaperMuted(bool muted)
        {
            _settings.WallpaperMuted = muted;
            bool applied = _audio.SetMute(muted);
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
            SetStatus(applied ? (muted ? "壁纸已静音" : "壁纸声音已开启") : "已保存静音状态（Wallpaper Engine 未运行）", false);
        }

        public void SyncWallpaperAudio()
        {
            int volume;
            bool muted;
            if (_audio.TryGetState(out volume, out muted))
            {
                _settings.WallpaperVolume = volume;
                _settings.WallpaperMuted = muted;
                AppSettingsStore.Save(_settings);
                RaiseStateChanged();
            }
        }

        public void EnsureWallpaperAudio()
        {
            int volume;
            bool muted;
            if (!_audio.TryGetState(out volume, out muted)) return;
            if (volume == _settings.WallpaperVolume && muted == _settings.WallpaperMuted) return;
            _audio.SetVolume(_settings.WallpaperVolume);
            _audio.SetMute(_settings.WallpaperMuted);
        }

        private async Task ReapplyAudioAfterSwitchAsync()
        {
            int[] delays = { 400, 1200, 3000 };
            foreach (int delay in delays)
            {
                try
                {
                    await Task.Delay(delay).ConfigureAwait(true);
                    _audio.SetVolume(_settings.WallpaperVolume);
                    _audio.SetMute(_settings.WallpaperMuted);
                }
                catch
                {
                }
            }
        }

        public async Task SetWorkshopTopEnabledAsync(bool enabled)
        {            _settings.WorkshopTopEnabled = enabled;
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
            if (!enabled)
            {
                _workshopStatus = "订阅榜补充已关闭";
                SetStatus("已关闭创意工坊订阅榜补充", false);
                RaiseWorkshopUpdated();
                return;
            }

            bool loaded = LoadWorkshopCache();
            if (loaded && IsWorkshopCacheFresh())
            {
                _workshopStatus = "使用 " + _workshopFetchedAt.ToString("HH:mm") + " 缓存的 "
                    + _workshopItems.Count + " 个工坊候选";
                RaiseWorkshopUpdated();
                RaiseStateChanged();
                SetStatus("已启用创意工坊订阅榜补充", false);
                return;
            }
            if (loaded)
            {
                RaiseWorkshopUpdated();
                SetStatus("已载入旧缓存，正在更新订阅榜…", false);
            }
            await RefreshWorkshopAsync(false).ConfigureAwait(true);
        }

        public async Task RefreshWorkshopAsync(bool force)
        {
            if (!_settings.WorkshopTopEnabled)
            {
                SetStatus("请先开启创意工坊订阅榜补充", true);
                return;
            }
            if (_workshopBusy)
            {
                SetStatus("正在读取创意工坊，请稍候…", false);
                return;
            }
            _workshopBusy = true;
            _workshopError = "";
            int refreshSerial = ++_workshopBatchSerial;
            _workshopStatus = "准备读取创意工坊订阅榜前 100 张…";
            RaiseWorkshopUpdated();
            RaiseStateChanged();
            try
            {
                if (!force && _workshopItems.Count == 0 && LoadWorkshopCache() && IsWorkshopCacheFresh())
                {
                    _workshopStatus = "使用缓存 · 共 " + _workshopItems.Count + " 个候选";
                    RaiseWorkshopUpdated();
                    return;
                }

                IProgress<string> progress = new Progress<string>(delegate(string message)
                {
                    _workshopStatus = message;
                    RaiseStateChanged();
                });

                _workshopItems = new List<WallpaperItem>();
                _workshopPages = 0;
                _workshopStatus = "正在抓取订阅榜候选并去重排序…";
                RaiseWorkshopUpdated();
                RaiseStateChanged();

                Func<WorkshopItemDto, bool> accept = delegate(WorkshopItemDto dto)
                {
                    if (IsOversizedVideo(dto)) return false;
                    AgeRating rating = AgeRatingInfo.FromTags(dto.Tags);
                    if ((_settings.AgeRatingMask & AgeRatingInfo.MaskOf(rating)) == 0) return false;
                    if (_settings.PreferScenery && WorkshopTagFilter.IsExcluded(dto.Tags)) return false;
                    return true;
                };

                HashSet<string> combinedIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> liveIds = new HashSet<string>(StringComparer.Ordinal);
                IProgress<WorkshopFetchResult> liveProgress =
                    new Progress<WorkshopFetchResult>(delegate(WorkshopFetchResult partial)
                    {
                        if (refreshSerial != _workshopBatchSerial) return;
                        ApplyProgressiveWorkshop(partial, liveIds);
                    });
                List<WorkshopItemDto> combined = new List<WorkshopItemDto>();
                int pagesFetched = 0;
                string firstError = "";
                List<string> tags = _settings.PreferScenery
                    ? new List<string>(WorkshopContentTags.Allowed)
                    : new List<string>(WorkshopContentTags.All);
                int perTag = _settings.PreferScenery ? 100 : 50;
                WorkshopFetchResult[] tagResults = new WorkshopFetchResult[tags.Count];
                string[] tagErrors = new string[tags.Count];
                int nextTag = -1;
                int workers = Math.Min(3, tags.Count);
                _workshopStatus = "正在并行抓取 " + tags.Count + " 个 Steam 标签…";
                RaiseStateChanged();
                Task[] tasks = new Task[workers];
                for (int worker = 0; worker < workers; worker++)
                {
                    tasks[worker] = Task.Run(async delegate
                    {
                        while (true)
                        {
                            int index = Interlocked.Increment(ref nextTag);
                            if (index >= tags.Count) break;
                            string tag = tags[index];
                            try
                            {
                                tagResults[index] = await _workshopService.FetchTopAsync(
                                    SteamWorkshopService.DefaultPages, null, null, CancellationToken.None,
                                    accept, tag, perTag).ConfigureAwait(false);
                                liveProgress.Report(tagResults[index]);
                            }
                            catch (Exception tagError)
                            {
                                tagErrors[index] = tag + "：" + tagError.Message;
                                Log.Warn("读取工坊标签「" + tag + "」失败：" + tagError.Message);
                            }
                        }
                    });
                }
                await Task.WhenAll(tasks).ConfigureAwait(true);
                for (int index = 0; index < tags.Count; index++)
                {
                    WorkshopFetchResult tagResult = tagResults[index];
                    if (!String.IsNullOrEmpty(tagErrors[index]) && String.IsNullOrEmpty(firstError))
                        firstError = tagErrors[index];
                    if (tagResult == null) continue;
                    pagesFetched += tagResult.PagesFetched;
                    foreach (WorkshopItemDto dto in tagResult.Items)
                    {
                        if (combinedIds.Add(dto.Id)) combined.Add(dto);
                    }
                    if (!String.IsNullOrEmpty(tagResult.Error) && String.IsNullOrEmpty(firstError))
                        firstError = tagResult.Error;
                }
                _workshopStatus = "已抓取 " + tags.Count + " 个标签 · 去重后 " + combined.Count + " 张";
                RaiseStateChanged();
                _workshopBatchSerial++;
                if (combined.Count == 0)
                    throw new InvalidOperationException("没有从创意工坊标签页解析到可用壁纸。");

                _workshopFetchedAt = DateTime.Now;
                _workshopPages = pagesFetched;
                _cacheIncludesAllTags = !_settings.PreferScenery;
                BuildWorkshopItems(combined);
                foreach (WallpaperItem fetchedItem in _workshopItems) ClassifyWorkshopItemQuick(fetchedItem);
                ApplyVisionCache();
                _workshopItems.Sort(delegate(WallpaperItem a, WallpaperItem b)
                {
                    int subscriptions = b.WorkshopSubscriptions.CompareTo(a.WorkshopSubscriptions);
                    if (subscriptions != 0) return subscriptions;
                    return b.WorkshopVotes.CompareTo(a.WorkshopVotes);
                });
                for (int i = 0; i < _workshopItems.Count; i++) _workshopItems[i].WorkshopRank = i + 1;
                _workshopStatus = "正在挑选适合当前天气与时段的候选…";
                RaiseStateChanged();
                LinkWorkshopToLocal();
                SaveWorkshopCache();

                if (!String.IsNullOrEmpty(firstError))
                {
                    _workshopError = firstError;
                    _workshopStatus = (_settings.PreferScenery
                        ? "9 个未屏蔽标签各保留 100"
                        : WorkshopContentTags.AllCount + " 个 Steam 标签各保留 50")
                        + " · 去重后 " + _workshopItems.Count + " 张（部分数据未完成）";
                }
                else
                {
                    _workshopStatus = (_settings.PreferScenery
                        ? "9 个未屏蔽标签各保留 100"
                        : WorkshopContentTags.AllCount + " 个 Steam 标签各保留 50")
                        + " · 去重后 " + _workshopItems.Count + " 张 · "
                        + (_workshopItems.Count - CountInstalledWorkshop()) + " 个待订阅";
                }
                SetStatus("创意工坊标签候选已更新", false);
                RaiseWorkshopUpdated();
                int firstScreenPreviews = Math.Min(40, _workshopItems.Count);
                SetStatus("正在准备首屏工坊预览…", false);
                await EnsureWorkshopPreviewsAsync(firstScreenPreviews).ConfigureAwait(true);
                SetStatus("首屏已就绪，其余预览在后台补齐…", false);
                Task backgroundPreviews = EnsureWorkshopPreviewsAsync(_workshopItems.Count);
                Task visionTask = ReclassifyVisionAsync();
            }
            catch (Exception ex)
            {
                Log.Error("读取创意工坊失败", ex);
                _workshopError = ex.Message;
                _workshopStatus = "读取失败";
                if (_workshopItems.Count == 0) LoadWorkshopCache();
                SetStatus("创意工坊读取失败：" + ex.Message, true);
                RaiseWorkshopUpdated();
            }
            finally
            {
                _workshopBusy = false;
                RaiseWorkshopUpdated();
                RaiseStateChanged();
            }
        }

        private bool LoadWorkshopCache()
        {
            WorkshopCatalogDto catalog = WorkshopCatalogStore.Load();
            if (catalog == null || catalog.Items == null || catalog.Items.Count == 0) return false;
            if (catalog.ClassifierVersion < 9) return false;
            DateTime fetched = catalog.FetchedAt;
            if (fetched.Kind != DateTimeKind.Local) fetched = fetched.ToLocalTime();
            _workshopFetchedAt = fetched;
            _workshopPages = catalog.PagesFetched;
            _cacheIncludesAllTags = catalog.IncludesAllTags;
            BuildWorkshopItems(catalog.Items);
            if (catalog.PreviewQualityVersion < 3)
            {
                foreach (WallpaperItem qualityItem in _workshopItems)
                {
                    qualityItem.PreviewQualityChecked = false;
                    qualityItem.BlankPreview = false;
                }
            }
            foreach (WallpaperItem cachedItem in _workshopItems) ClassifyWorkshopItemQuick(cachedItem);
            ApplyVisionCache();
            LinkWorkshopToLocal();
            _workshopStatus = "缓存 · " + catalog.Items.Count + " 个候选";
            _workshopError = "";
            RaiseStateChanged();
            Task blankScan = ScanBlankPreviewsAsync();
            Task previewRepair = EnsureWorkshopPreviewsAsync(_workshopItems.Count);
            return true;
        }

        private bool IsWorkshopCacheFresh()
        {
            return _workshopFetchedAt != default(DateTime)
                && (DateTime.Now - _workshopFetchedAt).TotalHours < 12;
        }

        private static bool IsOversizedVideo(WorkshopItemDto dto)
        {
            return dto != null
                && String.Equals(dto.Type, "video", StringComparison.OrdinalIgnoreCase)
                && dto.FileSize > SteamWorkshopService.MaxVideoBytes;
        }

        private void ApplyProgressiveWorkshop(WorkshopFetchResult partial, HashSet<string> liveIds)
        {
            if (partial == null || partial.Items == null || partial.Items.Count == 0) return;
            int added = 0;
            foreach (WorkshopItemDto dto in partial.Items)
            {
                if (IsOversizedVideo(dto)) continue;
                if (!liveIds.Add(dto.Id)) continue;
                WallpaperItem item = CreateWorkshopItem(dto);
                ClassifyWorkshopItemQuick(item);
                _workshopItems.Add(item);
                added++;
            }
            if (added == 0) return;
            _workshopItems.Sort(delegate(WallpaperItem a, WallpaperItem b)
            {
                int subscriptions = b.WorkshopSubscriptions.CompareTo(a.WorkshopSubscriptions);
                if (subscriptions != 0) return subscriptions;
                int votes = b.WorkshopVotes.CompareTo(a.WorkshopVotes);
                if (votes != 0) return votes;
                return a.WorkshopRank.CompareTo(b.WorkshopRank);
            });
            for (int i = 0; i < _workshopItems.Count; i++) _workshopItems[i].WorkshopRank = i + 1;
            ApplyVisionCache();
            _workshopStatus = "边抓取边显示 · 已获取 " + _workshopItems.Count + " 张 · 最终按订阅人数排序";
            RaiseWorkshopUpdated();
            RaiseStateChanged();
        }

        private void BuildWorkshopItems(List<WorkshopItemDto> items)
        {
            _characterCache.Clear();
            _workshopItems = new List<WallpaperItem>();
            foreach (WorkshopItemDto dto in items)
            {
                if (IsOversizedVideo(dto)) continue;
                _workshopItems.Add(CreateWorkshopItem(dto));
            }
        }

        private WallpaperItem CreateWorkshopItem(WorkshopItemDto dto)
        {
            WallpaperItem item = new WallpaperItem();
            item.Id = dto.Id;
            item.Title = dto.Title;
            item.Description = dto.Description;
            item.Author = dto.Author;
            item.Type = dto.Type;
            item.Tags = dto.Tags == null ? new List<string>() : new List<string>(dto.Tags);
            item.AgeRating = AgeRatingInfo.FromTags(item.Tags);
            item.IsRemote = true;
            item.IsInstalled = false;
            item.WorkshopPage = dto.Page;
            item.WorkshopRank = dto.Rank;
            item.WorkshopVotes = dto.Votes;
            item.WorkshopSubscriptions = dto.Subscriptions;
            item.RemotePreviewUrl = dto.PreviewUrl;
            item.WorkshopUrl = dto.WorkshopUrl;
            item.HasPeople = dto.HasPeople;
            item.PeopleScanned = dto.PeopleScanned;
            item.BlankPreview = dto.BlankPreview;
            item.PreviewQualityChecked = dto.PreviewQualityChecked;
            if (item.BlankPreview)
            {
                item.IsSuitable = false;
                item.ExcludeReason = "预览为单色块，已过滤";
            }
            item.IsVideo = String.Equals(dto.Type, "video", StringComparison.OrdinalIgnoreCase);
            item.IsWeb = String.Equals(dto.Type, "web", StringComparison.OrdinalIgnoreCase);
            string preview = Path.Combine(AppPaths.WorkshopPreviewDirectory, dto.Id + ".jpg");
            if (File.Exists(preview)) item.PreviewPath = preview;
            return item;
        }

        private void ClassifyWorkshopItems()
        {
            foreach (WallpaperItem item in _workshopItems) ClassifyWorkshopItem(item);
        }

        private void ClassifyWorkshopItemQuick(WallpaperItem item)
        {
            WallpaperClassifier.SetAutoResult(item, WallpaperClassifier.ClassifyQuick(item));
        }

        private void ClassifyWorkshopItem(WallpaperItem item)
        {
            WallpaperClassification classification = WallpaperClassifier.ClassifyOne(item, _settings);
            WallpaperClassifier.SetAutoResult(item, classification);
            StoreVisionEntry(item);
        }

        private void StoreVisionEntry(WallpaperItem item)
        {
            if (item == null || String.IsNullOrEmpty(item.Id) || item.AutoScores == null) return;
            if (item.IsRemote)
            {
                _visionCache.Entries[item.Id] = VisionCacheStore.Create(item);
            }
            else
            {
                _visionCache.Entries["L:" + item.Id] = VisionCacheStore.Create(item);
            }
        }

        private bool TryApplyLocalVisionCache(WallpaperItem item)
        {
            if (item == null || item.IsRemote || String.IsNullOrEmpty(item.Id)) return false;
            VisionCacheEntry entry;
            if (!_visionCache.Entries.TryGetValue("L:" + item.Id, out entry)) return false;
            if (!VisionCacheStore.IsValid(entry, item)) return false;
            VisionCacheStore.Apply(entry, item, _settings);
            return true;
        }

        private void ApplyLocalVisionCache()
        {
            foreach (WallpaperItem item in _wallpapers) TryApplyLocalVisionCache(item);
        }

        private void ApplyVisionCache()
        {
            foreach (WallpaperItem item in _workshopItems)
            {
                VisionCacheEntry entry;
                if (!_visionCache.Entries.TryGetValue(item.Id, out entry)) continue;
                if (!VisionCacheStore.IsValid(entry, item)) continue;
                VisionCacheStore.Apply(entry, item, _settings);
            }
        }

        private void SaveVisionCache()
        {
            VisionCacheStore.Save(_visionCache);
        }

        private async Task RefineTopWorkshopAsync(int max)
        {
            List<WallpaperItem> items = new List<WallpaperItem>();
            foreach (WallpaperItem item in _workshopItems)
            {
                if (items.Count >= max) break;
                items.Add(item);
            }
            if (items.Count == 0) return;
            await Task.Run(delegate
            {
                foreach (WallpaperItem item in items) ClassifyWorkshopItem(item);
            }).ConfigureAwait(true);
            LinkWorkshopToLocal();
            SaveWorkshopCache();
            RaiseWorkshopUpdated();
            RaiseStateChanged();
        }

        private async Task EnsureWorkshopPreviewsAsync(int max)
        {
            try { await DownloadWorkshopPreviewsAsync(max, null).ConfigureAwait(true); }
            catch (Exception ex) { Log.Warn("补齐工坊预览失败：" + ex.Message); }
        }

        private async Task DownloadWorkshopPreviewsAsync(int max, IProgress<string> progress)
        {
            if (_previewBusy) return;
            _previewBusy = true;
            try
            {
                List<WallpaperItem> candidates = new List<WallpaperItem>();
                HashSet<string> addedIds = new HashSet<string>(StringComparer.Ordinal);
                // First guarantee the exact list the user is looking at (age/scenery/sort
                // filtered). Then fill with other age-allowed candidates for view toggles.
                foreach (WallpaperItem item in GetRankedWorkshop())
                {
                    if (candidates.Count >= 120) break;
                    if (addedIds.Add(item.Id)) candidates.Add(item);
                }
                List<WallpaperItem> all = new List<WallpaperItem>();
                foreach (WallpaperItem item in _workshopItems)
                    if (item.IsSuitable && IsAgeAllowed(item)) all.Add(item);
                all.Sort(delegate(WallpaperItem a, WallpaperItem b)
                {
                    return a.WorkshopRank.CompareTo(b.WorkshopRank);
                });
                foreach (WallpaperItem item in all)
                {
                    if (candidates.Count >= max) break;
                    if (addedIds.Add(item.Id)) candidates.Add(item);
                }
                List<WallpaperItem> targets = new List<WallpaperItem>();
                foreach (WallpaperItem item in candidates)
                {
                    if (targets.Count >= max) break;
                    if (String.IsNullOrEmpty(item.RemotePreviewUrl)) continue;
                    string path = Path.Combine(AppPaths.WorkshopPreviewDirectory, item.Id + ".jpg");
                    if (File.Exists(path) && new FileInfo(path).Length > 256) continue;
                    targets.Add(item);
                }
                if (targets.Count == 0)
                {
                    await ScanBlankPreviewsAsync().ConfigureAwait(true);
                    await RefineTopWorkshopAsync(160).ConfigureAwait(true);
                    return;
                }
                if (progress != null)
                    progress.Report("正在补齐工坊预览图（" + targets.Count + " 张）…");

                SemaphoreSlim gate = new SemaphoreSlim(6);
                List<Task> tasks = new List<Task>();
                foreach (WallpaperItem target in targets)
                {
                    WallpaperItem item = target;
                    tasks.Add(Task.Run(async delegate
                    {
                        await gate.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            string path = await _workshopService.EnsurePreviewAsync(item.Id, item.RemotePreviewUrl)
                                .ConfigureAwait(false);
                            if (!String.IsNullOrEmpty(path)) item.PreviewPath = path;
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }));
                }
                await Task.WhenAll(tasks).ConfigureAwait(true);
                gate.Dispose();
                await ScanBlankPreviewsAsync().ConfigureAwait(true);
                await RefineTopWorkshopAsync(160).ConfigureAwait(true);
                if (progress != null) progress.Report("工坊预览图已补齐");
            }
            finally
            {
                _previewBusy = false;
            }
        }

        private void LinkWorkshopToLocal()
        {
            if (_workshopItems.Count == 0 || _wallpapers.Count == 0) return;
            foreach (WallpaperItem remote in _workshopItems)
            {
                WallpaperItem local = FindWallpaper(remote.Id);
                if (local == null)
                {
                    remote.IsInstalled = false;
                    continue;
                }
                remote.IsInstalled = true;
                remote.LaunchPath = local.LaunchPath;
                remote.FilePath = local.FilePath;
                remote.ProjectFile = local.ProjectFile;
                remote.IsVideo = local.IsVideo;
                remote.IsWeb = local.IsWeb;
                remote.Type = local.Type;
                remote.Category = local.Category;
                remote.IsActive = local.IsActive;
                local.WorkshopRank = remote.WorkshopRank;
                local.WorkshopPage = remote.WorkshopPage;
                local.WorkshopVotes = remote.WorkshopVotes;
                local.WorkshopSubscriptions = remote.WorkshopSubscriptions;
                if (String.IsNullOrEmpty(remote.PreviewPath) && File.Exists(local.PreviewPath))
                {
                    remote.PreviewPath = local.PreviewPath;
                    WallpaperClassification classification = WallpaperClassifier.ClassifyOne(remote);
                    WallpaperClassifier.SetAutoResult(remote, classification);
                }
            }
        }

        private void UpdateWorkshopActiveState()
        {
            if (_workshopItems.Count == 0) return;
            foreach (WallpaperItem item in _workshopItems)
                item.IsActive = !String.IsNullOrEmpty(_activeId) && item.Id == _activeId;
        }

        private void SaveWorkshopCache()
        {
            WorkshopCatalogDto catalog = new WorkshopCatalogDto();
            catalog.FetchedAt = _workshopFetchedAt;
            catalog.PagesFetched = _workshopPages;
            catalog.ClassifierVersion = 9;
            catalog.PreviewQualityVersion = 3;
            catalog.IncludesAllTags = _cacheIncludesAllTags;
            foreach (WallpaperItem item in _workshopItems)
            {
                WorkshopItemDto dto = new WorkshopItemDto();
                dto.Id = item.Id;
                dto.Title = item.Title;
                dto.Description = item.Description;
                dto.Author = item.Author;
                dto.PreviewUrl = item.RemotePreviewUrl;
                dto.Type = item.Type;
                dto.Tags = new List<string>(item.Tags);
                dto.Page = item.WorkshopPage;
                dto.Rank = item.WorkshopRank;
                dto.Votes = item.WorkshopVotes;
                dto.Subscriptions = item.WorkshopSubscriptions;
                dto.WorkshopUrl = item.WorkshopUrl;
                dto.HasPeople = item.HasPeople;
                dto.PeopleScanned = item.PeopleScanned;
                dto.BlankPreview = item.BlankPreview;
                dto.PreviewQualityChecked = item.PreviewQualityChecked;
                catalog.Items.Add(dto);
            }
            WorkshopCatalogStore.Save(catalog);
        }

        private int CountInstalledWorkshop()
        {
            int count = 0;
            foreach (WallpaperItem item in _workshopItems) if (item.IsInstalled) count++;
            return count;
        }

        public List<WallpaperItem> GetRankedWorkshop()
        {
            List<WallpaperItem> list = new List<WallpaperItem>();
            foreach (WallpaperItem candidate in _workshopItems)
                if (candidate.IsSuitable && IsAgeAllowed(candidate) && !IsBlocked(candidate)
                    && (!_settings.PreferScenery || IsScenicWorkshopItem(candidate)))
                    list.Add(candidate);
            if (list.Count == 0 && !_settings.PreferScenery)
            {
                foreach (WallpaperItem candidate in _workshopItems)
                    if (candidate.IsSuitable && IsAgeAllowed(candidate) && !IsBlocked(candidate))
                        list.Add(candidate);
            }
            list.Sort(delegate(WallpaperItem a, WallpaperItem b)
            {
                int subscriptions = b.WorkshopSubscriptions.CompareTo(a.WorkshopSubscriptions);
                if (subscriptions != 0) return subscriptions;
                int votes = b.WorkshopVotes.CompareTo(a.WorkshopVotes);
                if (votes != 0) return votes;
                return a.WorkshopRank.CompareTo(b.WorkshopRank);
            });
            return list;
        }

        public WallpaperItem TopWorkshopItem
        {
            get
            {
                List<WallpaperItem> list = GetRankedWorkshop();
                return list.Count > 0 ? list[0] : null;
            }
        }

        public WallpaperItem FindWorkshop(string id)
        {
            if (String.IsNullOrEmpty(id)) return null;
            foreach (WallpaperItem item in _workshopItems)
                if (item.Id == id) return item;
            return null;
        }

        public async Task<bool> ApplyWorkshopItemAsync(WallpaperItem item)
        {
            if (item == null) return false;
            if (!item.IsInstalled || String.IsNullOrEmpty(item.LaunchPath))
            {
                OpenWorkshopPage(item);
                SetStatus("已打开 Steam 订阅页；下载完成后会自动加入壁纸库，无需重启。", false);
                return false;
            }
            WallpaperItem local = FindWallpaper(item.Id);
            if (local == null)
            {
                SetStatus("没有找到已安装的壁纸文件，请重新扫描。", true);
                return false;
            }
            return await ApplyWallpaperAsync(local, false, "从工坊精选应用").ConfigureAwait(true);
        }

        public void OpenWorkshopPage(WallpaperItem item)
        {
            if (item == null || String.IsNullOrEmpty(item.Id)) return;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "steam://url/CommunityFilePage/" + item.Id;
                info.UseShellExecute = true;
                Process.Start(info);
                SetStatus("已打开 Steam 创意工坊页面；订阅下载后会自动检测，无需重启。", false);
            }
            catch
            {
                try
                {
                    ProcessStartInfo info = new ProcessStartInfo();
                    info.FileName = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + item.Id;
                    info.UseShellExecute = true;
                    Process.Start(info);
                }
                catch (Exception ex)
                {
                    SetStatus("无法打开创意工坊：" + ex.Message, true);
                }
            }
        }

        private async Task ApplyStartupMatchAsync()
        {
            if (_weather == null) return;
            List<WallpaperItem> ranked = GetRanked(_weather.Current.Kind);
            if (ranked.Count == 0) return;
            if (CurrentWallpaperMatches(_weather.Current.Kind, CurrentTimeSlot)) return;
            WallpaperItem best = ranked[0];
            WallpaperItem current = ActiveItem;
            if (current != null && current.Id == best.Id) return;
            await ApplyWallpaperAsync(best, true, "启动时匹配当前天气与时段").ConfigureAwait(true);
        }

        public async Task RefreshWeatherAsync(bool manual)
        {            if (_refreshingWeather) return;
            if (!_settings.HasLocation)
            {
                await LocateByIpAsync().ConfigureAwait(true);
                return;
            }
            _refreshingWeather = true;
            SetStatus(manual ? "正在刷新室外天气…" : "正在更新天气…", false);
            RaiseStateChanged();
            try
            {
                LocationInfo location = BuildLocation();
                WeatherData data = await _weatherService.FetchWeatherAsync(location).ConfigureAwait(true);
                data.Location = location;
                data.FetchedAt = DateTime.Now;
                data.FromCache = false;
                _weather = data;
                _settings.LastWeatherFetch = data.FetchedAt;
                WeatherCacheStore.Save(data);
                _nextRefreshAt = DateTime.Now.AddHours(1);

                string kindText = data.Current.Kind.ToString();
                bool firstWeather = String.IsNullOrEmpty(_settings.LastWeatherKind);
                bool changed = !firstWeather && _settings.LastWeatherKind != kindText;
                string previousKind = _settings.LastWeatherKind;
                string slotText = CurrentTimeSlot.ToString();
                bool firstTime = String.IsNullOrEmpty(_settings.LastTimeSlot);
                bool timeChanged = _settings.UseTimeOfDay && !firstTime && _settings.LastTimeSlot != slotText;
                string previousSlot = _settings.LastTimeSlot;
                UpdateRecommendation();
                RaiseWeatherUpdated();

                bool autoSwitchNeeded = (changed || timeChanged) && _settings.AutoFollow && _settings.HasAppliedOnce;
                _settings.LastWeatherKind = kindText;
                if (_settings.UseTimeOfDay) _settings.LastTimeSlot = slotText;
                AppSettingsStore.Save(_settings);

                if (autoSwitchNeeded)
                {
                    string label = changed && timeChanged
                        ? "天气与时段同时变化" : (changed ? "天气转为「" + WeatherKindInfo.Name(data.Current.Kind) + "」"
                        : "时段进入「" + TimeSlotInfo.Name(CurrentTimeSlot) + "」");
                    SetStatus(label + "，正在匹配壁纸…", false);
                    bool handled = await AutoFollowAsync(data.Current.Kind).ConfigureAwait(true);
                    if (!handled)
                    {
                        _settings.LastWeatherKind = previousKind;
                        _settings.LastTimeSlot = previousSlot;
                        AppSettingsStore.Save(_settings);
                    }
                }
                else if (changed || timeChanged)
                {
                    SetStatus(changed
                        ? "天气已更新为「" + WeatherKindInfo.Name(data.Current.Kind) + "」"
                        : "时段进入「" + TimeSlotInfo.Name(CurrentTimeSlot) + "」", false);
                }
                else if (firstWeather || (_settings.UseTimeOfDay && firstTime))
                {
                    SetStatus("天气与时段的匹配已就绪", false);
                }
                else
                {
                    SetStatus("天气已刷新 · 当前 " + FormatTemperature(data.Current.Temperature)
                        + " · " + data.Current.Description, false);
                }

                if (_settings.AutoFollow && !_settings.HasAppliedOnce && _recommended != null)
                {
                    if (ActiveItem != null && ActiveItem.Id == _recommended.Id)
                    {
                        _settings.HasAppliedOnce = true;
                        _settings.LastTimeSlot = CurrentTimeSlot.ToString();
                        AppSettingsStore.Save(_settings);
                        SetStatus("当前壁纸已经匹配天气与时段 · 自动跟随已就绪", false);
                    }
                    else
                    {
                        SetStatus("自动跟随已开启，正在应用此刻的匹配壁纸…", false);
                        await ApplyWallpaperAsync(_recommended, true, "匹配当前天气与时段").ConfigureAwait(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("获取天气失败", ex);
                if (_weather == null)
                {
                    WeatherData cached = WeatherCacheStore.Load();
                    if (cached != null)
                    {
                        _weather = cached;
                        _weather.FromCache = true;
                        UpdateRecommendation();
                        RaiseWeatherUpdated();
                    }
                }
                SetStatus("天气更新失败：" + FriendlyError(ex), true);
            }
            finally
            {
                _refreshingWeather = false;
                if (_nextRefreshAt < DateTime.Now.AddMinutes(5))
                    _nextRefreshAt = DateTime.Now.AddMinutes(5);
                RaiseStateChanged();
            }
        }

        public async Task LocateByIpAsync()
        {
            if (_settings.LocationLocked && _settings.HasLocation)
            {
                SetStatus("定位已锁定，IP 变化不会修改当前位置。", false);
                return;
            }
            SetBusy(true);
            SetStatus("正在根据 IP 定位…", false);
            try
            {
                LocationInfo loc = await _weatherService.LocateByIpAsync().ConfigureAwait(true);
                ApplyLocation(loc);
                SetStatus("定位到 " + loc.DisplayName + (loc.IsApproximate ? "（IP 定位，可在设置中修正）" : ""), false);
                await RefreshWeatherAsync(false).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Error("IP 定位失败", ex);
                SetStatus("IP 定位失败：" + ex.Message, true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        public async Task SetLocationAsync(LocationInfo location)
        {
            if (location == null) return;
            await _weatherService.EnrichLocationAsync(location).ConfigureAwait(true);
            ApplyLocation(location);
            RememberLocation(location);
            SetStatus("位置已切换为 " + location.DisplayName, false);
            await RefreshWeatherAsync(true).ConfigureAwait(true);
        }

        public bool LocationLocked
        {
            get { return _settings.LocationLocked; }
        }

        public List<LocationInfo> GetLocationHistory()
        {
            if (_settings.LocationHistory == null) return new List<LocationInfo>();
            return new List<LocationInfo>(_settings.LocationHistory);
        }

        public void SetLocationLocked(bool locked)
        {
            if (locked && !_settings.HasLocation)
            {
                SetStatus("请先搜索并选择城市，再锁定定位。", true);
                RaiseStateChanged();
                return;
            }
            if (_settings.LocationLocked == locked) return;
            _settings.LocationLocked = locked;
            AppSettingsStore.Save(_settings);
            SetStatus(locked ? "定位已锁定，IP 变化不会修改当前位置。" : "定位已解锁，允许 IP 定位。", false);
            RaiseStateChanged();
        }

        private void RememberLocation(LocationInfo location)
        {
            if (location == null || String.IsNullOrEmpty(location.Name)) return;
            if (_settings.LocationHistory == null) _settings.LocationHistory = new List<LocationInfo>();
            _settings.LocationHistory.RemoveAll(delegate(LocationInfo item)
            {
                if (item == null) return true;
                return String.Equals(item.Name, location.Name, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(item.Admin1, location.Admin1, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(item.Latitude - location.Latitude) < 0.01
                    && Math.Abs(item.Longitude - location.Longitude) < 0.01;
            });
            LocationInfo stored = new LocationInfo();
            stored.Name = location.Name;
            stored.Admin1 = location.Admin1;
            stored.District = location.District;
            stored.Country = location.Country;
            stored.Latitude = location.Latitude;
            stored.Longitude = location.Longitude;
            stored.Timezone = location.Timezone;
            stored.IsApproximate = false;
            _settings.LocationHistory.Insert(0, stored);
            if (_settings.LocationHistory.Count > 8)
                _settings.LocationHistory.RemoveRange(8, _settings.LocationHistory.Count - 8);
            AppSettingsStore.Save(_settings);
        }

        private void ApplyLocation(LocationInfo location)
        {
            if (_settings.LocationLocked && _settings.HasLocation && location.IsApproximate)
                return;
            _settings.HasLocation = true;
            _settings.Latitude = location.Latitude;
            _settings.Longitude = location.Longitude;
            _settings.City = location.Name;
            _settings.Admin1 = location.Admin1;
            _settings.District = location.District;
            _settings.Country = location.Country;
            _settings.Timezone = location.Timezone;
            _settings.LocationApproximate = location.IsApproximate;
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
        }

        public async Task<List<LocationInfo>> SearchCitiesAsync(string query)
        {
            return await _weatherService.SearchCitiesAsync(query).ConfigureAwait(true);
        }

        private LocationInfo BuildLocation()
        {
            LocationInfo loc = new LocationInfo();
            loc.Name = _settings.City;
            loc.Admin1 = _settings.Admin1;
            loc.District = _settings.District;
            loc.Country = _settings.Country;
            loc.Latitude = _settings.Latitude;
            loc.Longitude = _settings.Longitude;
            loc.Timezone = _settings.Timezone;
            loc.IsApproximate = _settings.LocationApproximate;
            loc.Source = _settings.LocationApproximate ? "IP" : "手动";
            return loc;
        }

        public async Task<bool> AutoFollowAsync(WeatherKind kind)
        {
            if (FullscreenDetector.IsFullscreenApp())
            {
                _autoSwitchDeferred = true;
                SetStatus("检测到全屏程序，已暂停自动切换壁纸", false);
                return true;
            }
            TimeSlot slot = CurrentTimeSlot;
            List<WallpaperItem> ranked = GetRanked(kind);
            if (ranked.Count == 0)
            {
                SetStatus("没有可用的壁纸，无法自动匹配。", true);
                return true;
            }
            WallpaperItem best = ranked[0];
            WallpaperItem current = ActiveItem;
            string context = "天气「" + WeatherKindInfo.Name(kind) + "」";
            if (_settings.UseTimeOfDay)
                context += " · 时段「" + TimeSlotInfo.Name(slot) + "」";
            if (CurrentWallpaperMatches(kind, slot))
            {
                SetStatus(context + "，当前壁纸已经匹配，无需切换。", false);
                return true;
            }
            if (current != null && current.Id == best.Id)
            {
                SetStatus(context + "，当前壁纸《" + current.Title + "》已经匹配。", false);
                return true;
            }
            if (current != null && CombinedScore(current, kind, slot) >= CombinedScore(best, kind, slot) - 0.001)
            {
                SetStatus(context + "，当前壁纸仍然最匹配。", false);
                return true;
            }
            return await ApplyWallpaperAsync(best, true, "匹配" + context).ConfigureAwait(true);
        }

        private bool CurrentWallpaperMatches(WeatherKind kind, TimeSlot slot)
        {
            WallpaperItem current = ActiveItem;
            if (current == null || current.Hidden || IsBlocked(current) || !IsAgeAllowed(current))
                return false;
            if (current.MatchWeather != kind) return false;
            if (_settings.UseTimeOfDay && current.PrimaryTime != slot) return false;
            return true;
        }

        public async Task ResumeDeferredAutoFollowAsync()
        {
            if (!_autoSwitchDeferred || !_settings.AutoFollow || _weather == null) return;
            if (FullscreenDetector.IsFullscreenApp()) return;
            _autoSwitchDeferred = false;
            await AutoFollowAsync(_weather.Current.Kind).ConfigureAwait(true);
        }

        public async Task<bool> MatchCurrentWeatherAsync()
        {
            if (_weather == null)
            {
                SetStatus("天气数据还没准备好，请稍后再试。", true);
                return false;
            }
            WallpaperItem best = _recommended;
            if (best == null)
            {
                SetStatus("没有找到可用的壁纸。", true);
                return false;
            }
            if (ActiveItem != null && ActiveItem.Id == best.Id)
            {
                _settings.HasAppliedOnce = true;
                AppSettingsStore.Save(_settings);
                SetStatus("当前壁纸《" + best.Title + "》已经是最佳匹配。", false);
                RaiseStateChanged();
                return true;
            }
            return await ApplyWallpaperAsync(best, false, "手动匹配当前天气").ConfigureAwait(true);
        }

        public async Task<bool> ApplyWallpaperAsync(WallpaperItem item, bool automatic, string reason)
        {
            if (item == null) return false;
            if (automatic && FullscreenDetector.IsFullscreenApp())
            {
                _autoSwitchDeferred = true;
                SetStatus("检测到全屏程序，已暂停自动切换壁纸", false);
                return false;
            }
            if (_engine == null || !_engine.IsAvailable)
            {
                SetStatus("Wallpaper Engine 不可用，无法切换壁纸。", true);
                return false;
            }
            if (_applying)
            {
                SetStatus("正在切换壁纸，请稍候。", false);
                return false;
            }
            _applying = true;
            SetBusy(true);
            SetStatus(automatic ? "正在按天气切换壁纸…" : "正在切换壁纸…", false);
            try
            {
                WallpaperApplyResult result = await _engine.ApplyAsync(item).ConfigureAwait(true);
                if (!result.Success)
                {
                    SetStatus(result.Message, true);
                    return false;
                }
                SetBusy(false);
                _activeId = item.Id;
                _activePath = !String.IsNullOrEmpty(result.ActivePath) ? result.ActivePath : item.FilePath;
                _lastWallpaperChangeAt = DateTime.Now;
                MarkActiveItem();
                UpdateWorkshopActiveState();
                Task audioTask = ReapplyAudioAfterSwitchAsync();
                _settings.HasAppliedOnce = true;
                _settings.LastAppliedId = item.Id;
                _settings.LastAppliedKind = _weather != null ? _weather.Current.Kind.ToString() : "";
                _settings.LastWeatherKind = _weather != null ? _weather.Current.Kind.ToString() : _settings.LastWeatherKind;
                if (_settings.UseTimeOfDay) _settings.LastTimeSlot = CurrentTimeSlot.ToString();
                AppSettingsStore.Save(_settings);
                _sessionSwitches++;
                UpdateRecommendation();
                RaiseStateChanged();
                WallpaperAppliedEventArgs args = new WallpaperAppliedEventArgs(item, automatic, reason);
                EventHandler<WallpaperAppliedEventArgs> handler = WallpaperApplied;
                if (handler != null) handler(this, args);
                string prefix = automatic ? "已自动切换" : "已切换";
                SetStatus(prefix + "到《" + item.Title + "》· " + (result.Verified ? "桌面已更新" : "正在加载"), false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("切换壁纸失败", ex);
                SetStatus("切换壁纸失败：" + ex.Message, true);
                return false;
            }
            finally
            {
                _applying = false;
                SetBusy(false);
            }
        }

        public async Task<bool> RestoreOriginalAsync()
        {
            if (String.IsNullOrEmpty(_settings.OriginalWallpaperPath)
                || !File.Exists(_settings.OriginalWallpaperPath))
            {
                SetStatus("没有保存到原始壁纸，无法恢复。", true);
                return false;
            }
            if (_engine == null || !_engine.IsAvailable)
            {
                SetStatus("Wallpaper Engine 不可用。", true);
                return false;
            }
            SetBusy(true);
            SetStatus("正在恢复最初的壁纸…", false);
            try
            {
                WallpaperApplyResult result = await _engine.ApplyPathAsync(_settings.OriginalWallpaperPath).ConfigureAwait(true);
                if (!result.Success)
                {
                    SetStatus(result.Message, true);
                    return false;
                }
                _activeId = _settings.OriginalWallpaperId;
                _activePath = _settings.OriginalWallpaperPath;
                MarkActiveItem();
                _settings.LastAppliedId = "";
                _settings.LastAppliedKind = "";
                AppSettingsStore.Save(_settings);
                RaiseStateChanged();
                RaiseWallpapersUpdated();
                SetStatus("已恢复最初的壁纸。", false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("恢复原始壁纸失败", ex);
                SetStatus("恢复原始壁纸失败：" + ex.Message, true);
                return false;
            }
            finally
            {
                SetBusy(false);
            }
        }

        public void SetManualKind(WallpaperItem item, Nullable<WeatherKind> kind)
        {
            if (item == null) return;
            if (kind.HasValue)
                _settings.ManualKinds[item.Id] = (int)kind.Value;
            else
                _settings.ManualKinds.Remove(item.Id);
            WallpaperClassifier.ApplyManualOverride(item, _settings);
            AppSettingsStore.Save(_settings);
            UpdateRecommendation();
            RaiseWallpapersUpdated();
            RaiseStateChanged();
            SetStatus("《" + item.Title + "》" + (kind.HasValue
                ? "已锁定为「" + WeatherKindInfo.Name(kind.Value) + "」"
                : "已恢复自动识别"), false);
        }

        public void SetHidden(WallpaperItem item, bool hidden)
        {
            if (item == null) return;
            if (hidden)
            {
                if (!_settings.HiddenIds.Contains(item.Id)) _settings.HiddenIds.Add(item.Id);
            }
            else
            {
                _settings.HiddenIds.Remove(item.Id);
            }
            item.Hidden = hidden;
            AppSettingsStore.Save(_settings);
            UpdateRecommendation();
            RaiseWallpapersUpdated();
            RaiseStateChanged();
            SetStatus(hidden ? "已从匹配中排除《" + item.Title + "》" : "已恢复《" + item.Title + "》的匹配", false);
        }

        public void ResetManualMappings()
        {
            _settings.ManualKinds.Clear();
            foreach (WallpaperItem item in _wallpapers)
                WallpaperClassifier.ApplyManualOverride(item, _settings);
            AppSettingsStore.Save(_settings);
            UpdateRecommendation();
            RaiseWallpapersUpdated();
            RaiseStateChanged();
            SetStatus("已重置全部手动天气校准。", false);
        }

        public void ResetHiddenWallpapers()
        {
            _settings.HiddenIds.Clear();
            foreach (WallpaperItem item in _wallpapers) item.Hidden = false;
            AppSettingsStore.Save(_settings);
            UpdateRecommendation();
            RaiseWallpapersUpdated();
            RaiseStateChanged();
            SetStatus("已恢复被排除的壁纸。", false);
        }

        public void SetAutoFollow(bool enabled)
        {
            _settings.AutoFollow = enabled;
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
            SetStatus(enabled ? "自动跟随已开启 · 天气与时段变化时会切换壁纸" : "自动跟随已暂停", false);
        }

        public void SetUseTimeOfDay(bool enabled)
        {
            _settings.UseTimeOfDay = enabled;
            _settings.LastTimeSlot = CurrentTimeSlot.ToString();
            AppSettingsStore.Save(_settings);
            UpdateRecommendation();
            RaiseWallpapersUpdated();
            RaiseStateChanged();
            SetStatus(enabled ? "已结合当前时段选择壁纸" : "已仅按天气选择壁纸", false);
        }

        public async Task CheckTimeChangeAsync()
        {
            if (!_settings.UseTimeOfDay || _weather == null) return;
            string slotText = CurrentTimeSlot.ToString();
            if (String.Equals(_settings.LastTimeSlot, slotText, StringComparison.Ordinal)) return;
            string previousSlot = _settings.LastTimeSlot;
            _settings.LastTimeSlot = slotText;
            AppSettingsStore.Save(_settings);
            UpdateRecommendation();
            RaiseWallpapersUpdated();
            if (_settings.AutoFollow && _settings.HasAppliedOnce)
            {
                SetStatus("时段进入「" + TimeSlotInfo.Name(CurrentTimeSlot) + "」，正在匹配壁纸…", false);
                bool handled = await AutoFollowAsync(_weather.Current.Kind).ConfigureAwait(true);
                if (!handled)
                {
                    _settings.LastTimeSlot = previousSlot;
                    AppSettingsStore.Save(_settings);
                }
            }
            else
            {
                SetStatus("时段进入「" + TimeSlotInfo.Name(CurrentTimeSlot) + "」· 推荐已更新", false);
                RaiseStateChanged();
            }
        }

        public void SetRefreshMinutes(int minutes)
        {
            _settings.RefreshMinutes = 60;
            _nextRefreshAt = DateTime.Now.AddHours(1);
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
        }

        public void SetRecommendationRotateEnabled(bool enabled)
        {
            _settings.RecommendationRotateEnabled = enabled;
            _lastWallpaperChangeAt = DateTime.Now;
            AppSettingsStore.Save(_settings);
            SetStatus(enabled
                ? "自动更换壁纸已开启，请填写分钟数"
                : "自动更换壁纸已关闭", false);
            RaiseStateChanged();
        }

        public void SetRecommendationRotateMinutes(int minutes)
        {
            if (minutes < 0) minutes = 0;
            if (minutes > 1440) minutes = 1440;
            _settings.RecommendationRotateMinutes = minutes;
            _lastWallpaperChangeAt = DateTime.Now;
            AppSettingsStore.Save(_settings);
            if (_settings.RecommendationRotateEnabled)
                SetStatus(minutes <= 0 ? "请填写自动更换的分钟数"
                    : "每隔 " + minutes + " 分钟自动更换壁纸", false);
            RaiseStateChanged();
        }

        public async Task CheckRecommendationRotationAsync()
        {
            int minutes = _settings.RecommendationRotateMinutes;
            if (!_settings.RecommendationRotateEnabled || minutes <= 0 || _applying
                || !_settings.AutoFollow || _weather == null) return;
            if (FullscreenDetector.IsFullscreenApp())
            {
                _autoSwitchDeferred = true;
                return;
            }
            if ((DateTime.Now - _lastWallpaperChangeAt).TotalMinutes < minutes) return;
            List<WallpaperItem> ranked = GetRanked(_weather.Current.Kind);
            if (ranked.Count == 0) return;
            int current = -1;
            for (int i = 0; i < ranked.Count; i++)
            {
                if (ranked[i].IsActive || (!String.IsNullOrEmpty(_activeId) && ranked[i].Id == _activeId))
                {
                    current = i;
                    break;
                }
            }
            WallpaperItem next = current >= 0
                ? ranked[(current + 1) % ranked.Count]
                : ranked[0];
            if (next == null) return;
            if (ranked.Count == 1 && next.IsActive) return;
            await ApplyWallpaperAsync(next, true, "此刻推荐定时更换").ConfigureAwait(true);
        }

        public void SetStartWithWindows(bool enabled)
        {
            _settings.StartWithWindows = enabled;
            StartupManager.SetEnabled(enabled);
            AppSettingsStore.Save(_settings);
            RaiseStateChanged();
        }

        public void SaveSettings()
        {
            AppSettingsStore.Save(_settings);
        }

        public bool IsRefreshDue()
        {
            return _weather == null || DateTime.Now >= _nextRefreshAt;
        }

        public async Task RefreshIfDueAsync()
        {
            if (IsRefreshDue() && !_refreshingWeather)
            {
                if (!_settings.HasLocation) await LocateByIpAsync().ConfigureAwait(true);
                else await RefreshWeatherAsync(false).ConfigureAwait(true);
            }
        }

        public List<WallpaperItem> GetVisibleWallpapers()
        {
            List<WallpaperItem> result = new List<WallpaperItem>();
            foreach (WallpaperItem item in _wallpapers)
            {
                if (!item.Hidden && !IsBlocked(item) && item.IsSuitable && IsAgeAllowed(item)
                    && (!_settings.PreferScenery || IsSceneItem(item)))
                {
                    UpgradeLocalPreview(item);
                    result.Add(item);
                }
            }
            return result;
        }

        public bool IsWhitelisted(WallpaperItem item)
        {
            return item != null && !String.IsNullOrEmpty(item.Id)
                && _settings.WhitelistIds != null && _settings.WhitelistIds.Contains(item.Id);
        }

        private void EnsureWhitelistFromLocal()
        {
            if (_settings.WhitelistIds == null) _settings.WhitelistIds = new List<string>();
            bool changed = false;
            foreach (WallpaperItem item in _wallpapers)
            {
                if (item == null || String.IsNullOrEmpty(item.Id)) continue;
                if (_settings.WhitelistIds.Contains(item.Id)) continue;
                _settings.WhitelistIds.Add(item.Id);
                changed = true;
            }
            if (changed) AppSettingsStore.Save(_settings);
        }

        public List<WallpaperItem> GetLibraryWallpapers()
        {
            List<WallpaperItem> result = new List<WallpaperItem>();
            foreach (WallpaperItem item in _wallpapers)
            {
                if (item.Hidden || IsBlocked(item) || !IsAgeAllowed(item)) continue;
                if (IsWhitelisted(item)
                    || (item.IsSuitable && (!_settings.PreferScenery || IsSceneItem(item))))
                {
                    UpgradeLocalPreview(item);
                    result.Add(item);
                }
            }
            return result;
        }

        public List<WallpaperItem> GetLibraryWorkshopSupplements(WeatherKind? filter, string query)
        {
            List<WallpaperItem> result = new List<WallpaperItem>();
            List<WallpaperItem> locals = GetLibraryWallpapers();
            HashSet<string> localIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<WeatherKind, int> counts = new Dictionary<WeatherKind, int>();
            foreach (WeatherKind kind in WeatherKindInfo.All) counts[kind] = 0;
            foreach (WallpaperItem item in locals)
            {
                localIds.Add(item.Id);
                counts[item.MatchWeather] = counts[item.MatchWeather] + 1;
            }
            string lowered = String.IsNullOrEmpty(query) ? "" : query.Trim().ToLowerInvariant();
            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                if (filter.HasValue && filter.Value != kind) continue;
                int localCount = counts[kind];
                if (localCount >= 9) continue;
                int added = 0;
                foreach (WallpaperItem item in _workshopItems)
                {
                    if (added >= 9 - localCount) break;
                    if (item.MatchWeather != kind || !item.IsSuitable || !IsAgeAllowed(item)) continue;
                    if (IsBlocked(item) || localIds.Contains(item.Id)) continue;
                    if (_settings.PreferScenery && !IsScenicWorkshopItem(item)) continue;
                    if (String.IsNullOrEmpty(item.PreviewPath) || !File.Exists(item.PreviewPath)) continue;
                    if (lowered.Length > 0)
                    {
                        string haystack = (item.Title + " " + String.Join(" ", item.Tags.ToArray()) + " "
                            + item.Description).ToLowerInvariant();
                        if (haystack.IndexOf(lowered, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    }
                    result.Add(item);
                    added++;
                }
            }
            return result;
        }

        private void UpgradeLocalPreview(WallpaperItem item)
        {
            if (item == null || item.IsRemote || String.IsNullOrEmpty(item.PreviewPath)) return;
            WallpaperItem remote = FindWorkshop(item.Id);
            if (remote == null || String.IsNullOrEmpty(remote.PreviewPath)) return;
            try
            {
                if (!File.Exists(remote.PreviewPath) || !File.Exists(item.PreviewPath)) return;
                if (new FileInfo(remote.PreviewPath).Length > new FileInfo(item.PreviewPath).Length)
                    item.PreviewPath = remote.PreviewPath;
            }
            catch
            {
            }
        }

        public bool IsAgeAllowed(WallpaperItem item)
        {
            return item != null && (_settings.AgeRatingMask & AgeRatingInfo.MaskOf(item.AgeRating)) != 0;
        }

        public bool IsBlocked(WallpaperItem item)
        {
            return item != null && _settings.BlockedIds != null && _settings.BlockedIds.Contains(item.Id);
        }

        public int BlockedCount
        {
            get { return _settings.BlockedIds == null ? 0 : _settings.BlockedIds.Count; }
        }

        public List<WallpaperItem> BlockedWallpapers
        {
            get
            {
                List<WallpaperItem> result = new List<WallpaperItem>();
                if (_settings.BlockedIds == null) return result;
                foreach (string id in _settings.BlockedIds)
                {
                    WallpaperItem item = FindWorkshop(id);
                    if (item == null) item = FindWallpaper(id);
                    if (item == null)
                    {
                        item = new WallpaperItem();
                        item.Id = id;
                        item.Title = "已屏蔽的壁纸 #" + id;
                    }
                    result.Add(item);
                }
                return result;
            }
        }

        public void BlockWallpaper(WallpaperItem item)
        {
            if (item == null || String.IsNullOrEmpty(item.Id)) return;
            if (_settings.BlockedIds == null) _settings.BlockedIds = new List<string>();
            if (!_settings.BlockedIds.Contains(item.Id)) _settings.BlockedIds.Add(item.Id);
            AppSettingsStore.Save(_settings);
            _characterCache.Remove(item.Id);
            RaiseWorkshopUpdated();
            RaiseStateChanged();
            SetStatus("已屏蔽《" + item.Title + "》，可在设置中取消屏蔽", false);
        }

        public void UnblockWallpaper(string id)
        {
            if (String.IsNullOrEmpty(id) || _settings.BlockedIds == null) return;
            _settings.BlockedIds.Remove(id);
            AppSettingsStore.Save(_settings);
            RaiseWorkshopUpdated();
            RaiseStateChanged();
            SetStatus("已取消屏蔽该壁纸", false);
        }

        public bool IsCharacterItem(WallpaperItem item)
        {            if (item == null) return false;
            if (item.HasPeople) return true;
            if (item.PeopleScanned) return item.CharacterFocus;
            string key = item.Id ?? "";
            bool cached;
            if (key.Length > 0 && _characterCache.TryGetValue(key, out cached)) return cached;
            bool result = item.CharacterFocus || WallpaperClassifier.LooksLikeCharacter(item);
            if (!result && item.Tags != null)
            {
                foreach (string tag in item.Tags)
                {
                    string value = (tag ?? "").Trim().ToLowerInvariant();
                    if (value == "furry" || value == "sports" || value == "people"
                        || value == "portrait" || value == "vtuber" || value == "cosplay")
                    {
                        result = true;
                        break;
                    }
                }
            }
            if (key.Length > 0) _characterCache[key] = result;
            return result;
        }

        private bool IsSceneItem(WallpaperItem item)
        {
            return item != null && !IsCharacterItem(item);
        }

        private bool IsScenicWorkshopItem(WallpaperItem item)
        {
            if (item == null) return false;
            if (WorkshopTagFilter.IsExcluded(item.Tags)) return false;
            return IsSceneItem(item);
        }

        public int AgeFilteredCount
        {
            get
            {
                int count = 0;
                foreach (WallpaperItem item in _wallpapers)
                {
                    if (!item.Hidden && item.IsSuitable && !IsAgeAllowed(item)) count++;
                }
                return count;
            }
        }

        public int WorkshopAgeFilteredCount
        {
            get
            {
                int count = 0;
                foreach (WallpaperItem item in _workshopItems)
                {
                    if (item.IsSuitable && !IsAgeAllowed(item)) count++;
                }
                return count;
            }
        }

        public void SetAgeRatings(int mask)
        {
            mask &= 7;
            if (mask == 0) mask = AgeRatingInfo.MaskOf(AgeRating.Everyone);
            if (_settings.AgeRatingMask == mask) return;
            _settings.AgeRatingMask = mask;
            AppSettingsStore.Save(_settings);
            UpdateRecommendation();
            RaiseWallpapersUpdated();
            RaiseWorkshopUpdated();
            RaiseStateChanged();
            if (_settings.WorkshopTopEnabled)
            {
                Task ignored = RefreshWorkshopAsync(true);
            }
            SetStatus("内容分级筛选：" + AgeRatingInfo.DescribeMask(mask), false);
        }

        public int AutoExcludedCount
        {
            get
            {
                int count = 0;
                foreach (WallpaperItem item in _wallpapers)
                    if (!item.Hidden && !item.IsSuitable) count++;
                return count;
            }
        }

        public int WorkshopExcludedCount
        {
            get
            {
                int count = 0;
                foreach (WallpaperItem item in _workshopItems)
                    if (!item.IsSuitable) count++;
                return count;
            }
        }

        public int WorkshopSuitableCount
        {
            get
            {
                int count = 0;
                foreach (WallpaperItem item in _workshopItems)
                    if (item.IsSuitable) count++;
                return count;
            }
        }

        public int WorkshopSceneryFilteredCount
        {
            get
            {
                int count = 0;
                if (!_settings.PreferScenery) return 0;
                foreach (WallpaperItem item in _workshopItems)
                    if (item.IsSuitable && IsAgeAllowed(item)
                        && (IsCharacterItem(item) || WorkshopTagFilter.IsExcluded(item.Tags))) count++;
                return count;
            }
        }

        public int WorkshopDisplayCount
        {
            get
            {
                int count = 0;
                foreach (WallpaperItem item in _workshopItems)
                    if (item.IsSuitable && IsAgeAllowed(item) && !IsBlocked(item)
                        && (!_settings.PreferScenery || IsScenicWorkshopItem(item))) count++;
                return count;
            }
        }

        public List<WallpaperItem> GetRecommendationOptions()
        {
            WeatherKind kind = _weather != null ? _weather.Current.Kind : WeatherKind.Cloudy;
            List<WallpaperItem> ranked = GetRanked(kind);
            List<WallpaperItem> result = new List<WallpaperItem>();
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);
            if (_recommended != null && added.Add(_recommended.Id)) result.Add(_recommended);
            foreach (WallpaperItem item in ranked)
            {
                if (result.Count >= 40) break;
                if (item.MatchWeather != kind) continue;
                if (added.Add(item.Id)) result.Add(item);
            }
            if (result.Count < 2)
            {
                foreach (WallpaperItem item in ranked)
                {
                    if (result.Count >= 12) break;
                    if (added.Add(item.Id)) result.Add(item);
                }
            }
            return result;
        }

        public List<WallpaperItem> GetRanked(WeatherKind kind)
        {
            TimeSlot slot = CurrentTimeSlot;
            bool useTime = _settings.UseTimeOfDay;
            List<WallpaperItem> ranked = GetVisibleWallpapers();
            ranked.RemoveAll(delegate(WallpaperItem item) { return item.BlankPreview; });
            if (_settings.PreferScenery)
            {
                List<WallpaperItem> scenery = new List<WallpaperItem>();
                foreach (WallpaperItem item in ranked)
                    if (IsSceneItem(item)) scenery.Add(item);
                ranked = scenery;
            }
            ranked.Sort(delegate(WallpaperItem a, WallpaperItem b)
            {
                double scoreA = CombinedScore(a, kind, slot);
                double scoreB = CombinedScore(b, kind, slot);
                int scoreCompare = scoreB.CompareTo(scoreA);
                if (scoreCompare != 0) return scoreCompare;
                if (!useTime)
                {
                    bool aExact = a.MatchWeather == kind;
                    bool bExact = b.MatchWeather == kind;
                    if (aExact != bExact) return aExact ? -1 : 1;
                }
                int confCompare = b.Confidence.CompareTo(a.Confidence);
                if (confCompare != 0) return confCompare;
                if (a.IsManual != b.IsManual) return a.IsManual ? -1 : 1;
                return String.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
            });
            return ranked;
        }

        private double CombinedScore(WallpaperItem item, WeatherKind kind, TimeSlot slot)
        {
            double score = item.MatchScore(kind);
            if (_settings.UseTimeOfDay)
            {
                score += item.TimeScore(slot) * 1.05;
                if (item.PrimaryTime == slot) score += 2.5;
                if (item.MatchWeather == kind) score += 2.0;
                if (item.PrimaryTime == TimeSlot.Night && slot != TimeSlot.Night) score -= 2.2;
                if (item.PrimaryTime != TimeSlot.Night && slot == TimeSlot.Night) score -= 0.8;
            }
            else
            {
                if (item.MatchWeather == kind) score += 2.0;
            }
            if (_settings.WorkshopTopEnabled && item.WorkshopRank > 0)
            {
                // Steam's default page order is "trend"; the real popularity signal is
                // the subscription count, so it carries more weight than raw page rank.
                score += Math.Min(3.2, Math.Log10(1.0 + item.WorkshopSubscriptions) / 1.55);
            }
            if (item.CharacterFocus) score -= 1.8;
            return score;
        }

        public bool HasExactMatch(WeatherKind kind)
        {
            foreach (WallpaperItem item in GetVisibleWallpapers())
                if (!item.BlankPreview && item.MatchWeather == kind) return true;
            return false;
        }

        public bool RecommendationIsApproximate
        {
            get
            {
                return _weather != null && _recommended != null
                    && _recommended.MatchWeather != _weather.Current.Kind;
            }
        }

        public bool RecommendationTimeIsApproximate
        {
            get
            {
                return _weather != null && _recommended != null && _settings.UseTimeOfDay
                    && _recommended.PrimaryTime != CurrentTimeSlot;
            }
        }

        public Dictionary<WeatherKind, int> CountByKind()
        {
            Dictionary<WeatherKind, int> counts = new Dictionary<WeatherKind, int>();
            foreach (WeatherKind kind in WeatherKindInfo.All) counts[kind] = 0;
            foreach (WallpaperItem item in GetVisibleWallpapers())
            {
                if (item.BlankPreview) continue;
                counts[item.MatchWeather] = counts[item.MatchWeather] + 1;
            }
            return counts;
        }

        public Dictionary<WeatherKind, int> CountByKindForLibrary()
        {
            Dictionary<WeatherKind, int> counts = new Dictionary<WeatherKind, int>();
            foreach (WeatherKind kind in WeatherKindInfo.All) counts[kind] = 0;
            foreach (WallpaperItem item in GetLibraryWallpapers())
                counts[item.MatchWeather] = counts[item.MatchWeather] + 1;
            return counts;
        }

        public WallpaperItem GetRepresentativeWallpaper(WeatherKind kind)
        {
            List<WallpaperItem> items = GetRepresentativeWallpapers(kind);
            return items.Count == 0 ? null : items[0];
        }

        public List<WallpaperItem> GetRepresentativeWallpapers(WeatherKind kind)
        {
            List<WallpaperItem> result = new List<WallpaperItem>();
            HashSet<string> seen = new HashSet<string>();
            foreach (WallpaperItem item in GetVisibleWallpapers())
            {
                if (item.MatchWeather != kind || String.IsNullOrEmpty(item.PreviewPath)) continue;
                if (item.BlankPreview) continue;
                if (!File.Exists(item.PreviewPath)) continue;
                if (_settings.PreferScenery && IsCharacterItem(item)) continue;
                if (seen.Add(item.Id)) result.Add(item);
            }
            foreach (WallpaperItem item in _workshopItems)
            {
                if (!item.IsSuitable || !IsAgeAllowed(item) || item.MatchWeather != kind) continue;
                if (item.BlankPreview) continue;
                if (IsBlocked(item)) continue;
                if (String.IsNullOrEmpty(item.PreviewPath) || !File.Exists(item.PreviewPath)) continue;
                if (_settings.PreferScenery && !IsScenicWorkshopItem(item)) continue;
                if (seen.Add(item.Id)) result.Add(item);
            }
            result.Sort(delegate(WallpaperItem a, WallpaperItem b)
            {
                int compare = a.IsRemote.CompareTo(b.IsRemote);
                if (compare != 0) return compare;
                compare = IsCharacterItem(a).CompareTo(IsCharacterItem(b));
                if (compare != 0) return compare;
                return b.Confidence.CompareTo(a.Confidence);
            });
            if (result.Count == 0 && !_settings.PreferScenery)
            {
                foreach (WallpaperItem item in GetVisibleWallpapers())
                {
                    if (item.BlankPreview) continue;
                    if (item.MatchWeather == kind && seen.Add(item.Id)) result.Add(item);
                }
                foreach (WallpaperItem item in _workshopItems)
                {
                    if (item.BlankPreview) continue;
                    if (item.IsSuitable && IsAgeAllowed(item) && item.MatchWeather == kind
                        && !IsBlocked(item) && seen.Add(item.Id)) result.Add(item);
                }
            }
            return result;
        }

        public int ManualCount
        {
            get
            {
                int count = 0;
                foreach (WallpaperItem item in _wallpapers) if (item.IsManual) count++;
                return count;
            }
        }

        public int HiddenCount
        {
            get
            {
                int count = 0;
                foreach (WallpaperItem item in _wallpapers) if (item.Hidden) count++;
                return count;
            }
        }

        public WallpaperItem FindWallpaper(string id)
        {
            if (String.IsNullOrEmpty(id)) return null;
            foreach (WallpaperItem item in _wallpapers)
                if (item.Id == id) return item;
            return null;
        }

        private void UpdateRecommendation()
        {
            if (_weather == null)
            {
                _recommended = null;
                return;
            }
            List<WallpaperItem> ranked = GetRanked(_weather.Current.Kind);
            _recommended = ranked.Count > 0 ? ranked[0] : null;
        }

        private void MarkActiveItem()
        {
            bool found = false;
            foreach (WallpaperItem item in _wallpapers)
            {
                bool active = (!String.IsNullOrEmpty(_activeId) && item.Id == _activeId)
                    || WallpaperScanner.PathEquals(item.FilePath, _activePath)
                    || WallpaperScanner.PathEquals(item.ProjectFile, _activePath);
                item.IsActive = active;
                if (active) found = true;
            }
            if (!found && !String.IsNullOrEmpty(_activeId))
            {
                foreach (WallpaperItem item in _wallpapers)
                {
                    if (item.Id == _activeId)
                    {
                        item.IsActive = true;
                        break;
                    }
                }
            }
        }

        private bool SameLocation(LocationInfo location, AppSettings settings)
        {
            if (location == null) return false;
            return Math.Abs(location.Latitude - settings.Latitude) < 0.35
                && Math.Abs(location.Longitude - settings.Longitude) < 0.35;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            RaiseStateChanged();
        }

        private void SetStatus(string message, bool error)
        {
            _status = message;
            RaiseStateChanged();
            EventHandler<AppStatusEventArgs> handler = StatusChanged;
            if (handler != null) handler(this, new AppStatusEventArgs(message, error));
        }

        private void RaiseWeatherUpdated()
        {
            EventHandler handler = WeatherUpdated;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseWallpapersUpdated()
        {
            EventHandler handler = WallpapersUpdated;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseWorkshopUpdated()
        {
            EventHandler handler = WorkshopUpdated;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseStateChanged()
        {
            EventHandler handler = StateChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static string FormatTemperature(double value)
        {
            return Math.Round(value).ToString("0") + "°";
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex == null) return "未知错误";
            string message = ex.Message;
            if (message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("超时", StringComparison.OrdinalIgnoreCase) >= 0)
                return "网络超时，请检查网络连接";
            if (message.IndexOf("remote name", StringComparison.OrdinalIgnoreCase) >= 0)
                return "无法连接天气服务";
            if (message.IndexOf("块的长度", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("block length", StringComparison.OrdinalIgnoreCase) >= 0)
                return "天气服务返回了不兼容的压缩数据，请稍后重试";
            return message;
        }
    }
}

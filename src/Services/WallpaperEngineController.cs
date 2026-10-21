using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SkyMatch
{
    public class WallpaperApplyResult
    {
        public bool Success { get; set; }
        public bool Verified { get; set; }
        public string Message { get; set; }
        public string ActivePath { get; set; }

        public WallpaperApplyResult()
        {
            Message = "";
            ActivePath = "";
        }
    }

    public class WallpaperEngineController
    {
        private readonly string _enginePath;

        public WallpaperEngineController(string enginePath)
        {
            _enginePath = enginePath;
        }

        public string EnginePath
        {
            get { return _enginePath; }
        }

        public bool IsRunning
        {
            get { return FindEngineProcess() != null; }
        }

        public bool IsAvailable
        {
            get
            {
                return !String.IsNullOrEmpty(_enginePath)
                    && (File.Exists(Path.Combine(_enginePath, "wallpaper64.exe"))
                        || File.Exists(Path.Combine(_enginePath, "wallpaper32.exe")));
            }
        }

        public string GetActivePath()
        {
            return WallpaperScanner.ReadActiveWallpaperPath(_enginePath);
        }

        public async Task<WallpaperApplyResult> ApplyAsync(WallpaperItem item)
        {
            if (item == null) throw new ArgumentNullException("item");
            string target = !String.IsNullOrEmpty(item.LaunchPath) ? item.LaunchPath : item.ProjectFile;
            return await ApplyPathAsync(target).ConfigureAwait(false);
        }

        public async Task<WallpaperApplyResult> ApplyPathAsync(string target)
        {
            WallpaperApplyResult result = new WallpaperApplyResult();
            if (String.IsNullOrEmpty(target) || !File.Exists(target))
            {
                result.Message = "壁纸文件不存在，Wallpaper Engine 无法加载。";
                return result;
            }
            if (!IsAvailable)
            {
                result.Message = "没有找到 Wallpaper Engine 可执行文件。";
                return result;
            }
            if (!await EnsureRunningAsync().ConfigureAwait(false))
            {
                result.Message = "Wallpaper Engine 未运行，自动启动失败。请先打开 Wallpaper Engine。";
                return result;
            }

            string exe = File.Exists(Path.Combine(_enginePath, "wallpaper64.exe"))
                ? Path.Combine(_enginePath, "wallpaper64.exe")
                : Path.Combine(_enginePath, "wallpaper32.exe");
            string arguments = "-control openWallpaper -file " + Quote(target) + " -monitor 0";
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exe;
                info.Arguments = arguments;
                info.WorkingDirectory = _enginePath;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                using (Process process = Process.Start(info))
                {
                    // The command is an IPC hand-off. Waiting for the helper
                    // process (and polling the config for seconds) made the
                    // whole UI feel frozen after every switch. Fire it and let
                    // Wallpaper Engine load asynchronously.
                    if (process == null)
                    {
                        result.Message = "无法启动 Wallpaper Engine 控制进程。";
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("调用 Wallpaper Engine 失败", ex);
                result.Message = "切换命令执行失败：" + ex.Message;
                return result;
            }

            result.Success = true;
            result.Verified = false;
            result.ActivePath = target;
            result.Message = "切换命令已发送，Wallpaper Engine 正在加载。";
            return result;
        }

        public async Task<bool> EnsureRunningAsync()
        {
            if (IsRunning) return true;
            if (!IsAvailable) return false;
            try
            {
                string exe = File.Exists(Path.Combine(_enginePath, "wallpaper64.exe"))
                    ? Path.Combine(_enginePath, "wallpaper64.exe")
                    : Path.Combine(_enginePath, "wallpaper32.exe");
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exe;
                info.Arguments = "-silent";
                info.WorkingDirectory = _enginePath;
                info.UseShellExecute = true;
                Process.Start(info);
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    if (IsRunning) return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("启动 Wallpaper Engine 失败", ex);
            }
            return false;
        }

        private Process FindEngineProcess()
        {
            Process[] processes = Process.GetProcessesByName("wallpaper64");
            if (processes.Length > 0) return processes[0];
            processes = Process.GetProcessesByName("wallpaper32");
            if (processes.Length > 0) return processes[0];
            return null;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static bool PathCovers(string active, string target)
        {
            if (String.IsNullOrEmpty(active) || String.IsNullOrEmpty(target)) return false;
            if (WallpaperScanner.PathEquals(active, target)) return true;
            try
            {
                string activeDir = Directory.Exists(active) ? active : Path.GetDirectoryName(active);
                string targetDir = Path.GetDirectoryName(target);
                if (String.IsNullOrEmpty(activeDir) || String.IsNullOrEmpty(targetDir)) return false;
                return WallpaperScanner.PathEquals(activeDir, targetDir);
            }
            catch
            {
                return false;
            }
        }
    }
}

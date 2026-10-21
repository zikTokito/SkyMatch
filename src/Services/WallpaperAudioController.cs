using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SkyMatch
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        int GetAudioSessionControl(ref Guid audioSessionGuid, int streamFlags, out IntPtr sessionControl);
        int GetSimpleAudioVolume(ref Guid audioSessionGuid, int streamFlags, out IntPtr audioVolume);
        int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        int GetCount(out int sessionCount);
        int GetSession(int sessionIndex, out IAudioSessionControl2 session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        int GetState(out int state);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
        int GetGroupingParam(out Guid groupingId);
        int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);
        int RegisterAudioSessionNotification(IntPtr client);
        int UnregisterAudioSessionNotification(IntPtr client);
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionId);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string instanceId);
        int GetProcessId(out uint processId);
        int IsSystemSoundsSession();
        int SetDuckingPreference(bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        int SetMasterVolume(float level, ref Guid eventContext);
        int GetMasterVolume(out float level);
        int SetMute(bool mute, ref Guid eventContext);
        int GetMute(out bool mute);
    }

    public class WallpaperAudioController
    {
        public bool TryGetState(out int volume, out bool muted)
        {
            volume = 100;
            muted = false;
            try
            {
                int[] pids = WallpaperProcessIds();
                if (pids.Length == 0) return false;
                IAudioSessionEnumerator sessions = GetSessions();
                if (sessions == null) return false;
                int count;
                if (sessions.GetCount(out count) < 0) return false;
                bool found = false;
                float total = 0;
                int matched = 0;
                bool anyMute = false;
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 session;
                    if (sessions.GetSession(i, out session) < 0 || session == null) continue;
                    uint pid;
                    if (session.GetProcessId(out pid) < 0 || !Contains(pids, (int)pid)) continue;
                    ISimpleAudioVolume simple = GetVolumeInterface(session);
                    if (simple == null) continue;
                    float level;
                    bool isMuted;
                    if (simple.GetMasterVolume(out level) >= 0)
                    {
                        total += level;
                        matched++;
                        found = true;
                    }
                    if (simple.GetMute(out isMuted) >= 0 && isMuted) anyMute = true;
                    Release(simple);
                }
                if (!found) return false;
                volume = (int)Math.Round(100.0 * total / matched);
                muted = anyMute;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("读取壁纸音量失败：" + ex.Message);
                return false;
            }
        }

        public bool SetVolume(int volume)
        {
            try
            {
                float level = Math.Max(0, Math.Min(100, volume)) / 100f;
                int changed = 0;
                int[] pids = WallpaperProcessIds();
                IAudioSessionEnumerator sessions = GetSessions();
                if (sessions == null) return false;
                int count;
                sessions.GetCount(out count);
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 session;
                    if (sessions.GetSession(i, out session) < 0 || session == null) continue;
                    uint pid;
                    if (session.GetProcessId(out pid) < 0 || !Contains(pids, (int)pid)) continue;
                    ISimpleAudioVolume simple = GetVolumeInterface(session);
                    if (simple == null) continue;
                    Guid ctx = Guid.Empty;
                    if (simple.SetMasterVolume(level, ref ctx) >= 0) changed++;
                    Release(simple);
                }
                return changed > 0;
            }
            catch (Exception ex)
            {
                Log.Warn("设置壁纸音量失败：" + ex.Message);
                return false;
            }
        }

        public bool SetMute(bool mute)
        {
            try
            {
                int changed = 0;
                int[] pids = WallpaperProcessIds();
                IAudioSessionEnumerator sessions = GetSessions();
                if (sessions == null) return false;
                int count;
                sessions.GetCount(out count);
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 session;
                    if (sessions.GetSession(i, out session) < 0 || session == null) continue;
                    uint pid;
                    if (session.GetProcessId(out pid) < 0 || !Contains(pids, (int)pid)) continue;
                    ISimpleAudioVolume simple = GetVolumeInterface(session);
                    if (simple == null) continue;
                    Guid ctx = Guid.Empty;
                    if (simple.SetMute(mute, ref ctx) >= 0) changed++;
                    Release(simple);
                }
                return changed > 0;
            }
            catch (Exception ex)
            {
                Log.Warn("设置壁纸静音失败：" + ex.Message);
                return false;
            }
        }

        private static IAudioSessionEnumerator GetSessions()
        {
            IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice device;
            if (enumerator.GetDefaultAudioEndpoint(0, 0, out device) < 0 || device == null) return null;
            Guid iid = typeof(IAudioSessionManager2).GUID;
            object managerObject;
            if (device.Activate(ref iid, 23, IntPtr.Zero, out managerObject) < 0 || managerObject == null)
                return null;
            IAudioSessionManager2 manager = (IAudioSessionManager2)managerObject;
            IAudioSessionEnumerator sessions;
            if (manager.GetSessionEnumerator(out sessions) < 0) return null;
            return sessions;
        }

        private static ISimpleAudioVolume GetVolumeInterface(IAudioSessionControl2 session)
        {
            ISimpleAudioVolume direct = session as ISimpleAudioVolume;
            if (direct != null) return direct;
            IntPtr unknown = IntPtr.Zero;
            IntPtr volumeUnknown = IntPtr.Zero;
            try
            {
                unknown = Marshal.GetIUnknownForObject(session);
                Guid iid = typeof(ISimpleAudioVolume).GUID;
                if (Marshal.QueryInterface(unknown, ref iid, out volumeUnknown) < 0) return null;
                return (ISimpleAudioVolume)Marshal.GetObjectForIUnknown(volumeUnknown);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (volumeUnknown != IntPtr.Zero) Marshal.Release(volumeUnknown);
                if (unknown != IntPtr.Zero) Marshal.Release(unknown);
            }
        }

        private static int[] WallpaperProcessIds()
        {
            List<int> pids = new List<int>();
            AddProcessIds("wallpaper64", pids);
            AddProcessIds("wallpaper32", pids);
            return pids.ToArray();
        }

        private static void AddProcessIds(string name, List<int> pids)
        {
            foreach (Process process in Process.GetProcessesByName(name))
            {
                try { pids.Add(process.Id); }
                catch { }
            }
        }

        private static bool Contains(int[] values, int value)
        {
            foreach (int item in values) if (item == value) return true;
            return false;
        }

        private static void Release(object comObject)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject)) Marshal.ReleaseComObject(comObject);
            }
            catch
            {
            }
        }
    }
}

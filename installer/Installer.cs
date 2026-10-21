using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkyMatchSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(args));
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly string[] _args;
        private readonly Label _status;
        private readonly ProgressBar _progress;
        private readonly string _destination;
        private readonly bool _noLaunch;
        private readonly bool _noShortcuts;
        private bool _closing;

        public SetupForm(string[] args)
        {
            _args = args ?? new string[0];
            _destination = ReadArg("--dir=");
            if (String.IsNullOrEmpty(_destination))
                _destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "SkyMatch");
            _noLaunch = HasArg("--no-launch");
            _noShortcuts = HasArg("--no-shortcuts");

            Text = "SkyMatch 1.2.0 Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 168);
            BackColor = Color.FromArgb(15, 19, 26);
            ForeColor = Color.FromArgb(235, 239, 245);
            Font = new Font("Microsoft YaHei UI", 9.5f);
            DoubleBuffered = true;

            Label title = new Label();
            title.Text = "SkyMatch 1.2.0";
            title.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(247, 184, 92);
            title.AutoSize = true;
            title.Location = new Point(24, 20);
            Controls.Add(title);

            _status = new Label();
            _status.Text = "Preparing installation...";
            _status.AutoSize = false;
            _status.Location = new Point(24, 58);
            _status.Size = new Size(430, 24);
            _status.ForeColor = Color.FromArgb(190, 200, 213);
            Controls.Add(_status);

            _progress = new ProgressBar();
            _progress.Location = new Point(24, 92);
            _progress.Size = new Size(430, 16);
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Minimum = 0;
            _progress.Maximum = 100;
            Controls.Add(_progress);

            Label note = new Label();
            note.Text = "Clean build - no cache, history, or user settings";
            note.AutoSize = false;
            note.Location = new Point(24, 120);
            note.Size = new Size(430, 22);
            note.ForeColor = Color.FromArgb(120, 133, 151);
            note.Font = new Font("Microsoft YaHei UI", 8.5f);
            Controls.Add(note);
        }

        private bool HasArg(string name)
        {
            foreach (string arg in _args)
                if (String.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private string ReadArg(string prefix)
        {
            foreach (string arg in _args)
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(prefix.Length).Trim('"');
            return "";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Task.Run(new Action(Extract));
        }

        private void Report(int percent, string text)
        {
            if (_closing || IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (_closing || IsDisposed) return;
                    _progress.Value = Math.Max(0, Math.Min(100, percent));
                    _status.Text = text;
                }));
            }
            catch { }
        }

        private void Extract()
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("SkyMatch"))
                {
                    string runningPath = "";
                    try { runningPath = process.MainModule.FileName; } catch { }
                    if (String.Equals(runningPath, Path.Combine(_destination, "SkyMatch.exe"),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        BeginInvoke(new Action(delegate
                        {
                            MessageBox.Show(this, "SkyMatch is running. Please close it before installing.", "SkyMatch",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            Close();
                        }));
                        return;
                    }
                }

                Report(2, "Removing the previous version...");
                if (Directory.Exists(_destination)) Directory.Delete(_destination, true);
                Directory.CreateDirectory(_destination);

                using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                {
                    if (payload == null) throw new InvalidOperationException("The installer payload is corrupted.");
                    using (ZipArchive archive = new ZipArchive(payload, ZipArchiveMode.Read))
                    {
                        int total = Math.Max(1, archive.Entries.Count);
                        int index = 0;
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            index++;
                            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                            string target = Path.Combine(_destination, relative);
                            if (String.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(target);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(target));
                                using (Stream input = entry.Open())
                                using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write,
                                    FileShare.None))
                                {
                                    input.CopyTo(output);
                                }
                            }
                            Report(5 + (int)(index * 85.0 / total),
                                "Installing " + index + " / " + total + " …");
                        }
                    }
                }

                Report(94, "Creating shortcuts...");
                BeginInvoke(new Action(FinishInstall));
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(delegate
                {
                    if (_closing) return;
                    MessageBox.Show(this, "Installation failed: " + ex.Message, "SkyMatch",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                }));
            }
        }

        private void FinishInstall()
        {
            try
            {
                string exe = Path.Combine(_destination, "SkyMatch.exe");
                if (!File.Exists(exe)) throw new InvalidOperationException("Installation files are incomplete.");
                if (!_noShortcuts)
                {
                    CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                        "SkyMatch.lnk"), exe);
                    CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                        "SkyMatch.lnk"), exe);
                }
                Report(100, "Installation complete.");
                if (!_noLaunch) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = _destination });
                _closing = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to create shortcuts: " + ex.Message, "SkyMatch",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _closing = true;
                Close();
            }
        }

        private void CreateShortcut(string path, string exe)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { path });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                new object[] { _destination });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                new object[] { exe + ",0" });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
                new object[] { "SkyMatch Weather Wallpaper" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
    }
}

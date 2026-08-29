using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PortableDropper
{
    // ============================================================
    //  PortableDropper - 便携软件安装器
    //  拖拽文件夹 / 压缩包 → 装入 %LOCALAPPDATA%\Programs 并生成开始菜单快捷方式
    //  使用方式：
    //    * 把文件夹或压缩包直接拖到本 exe 图标上（批处理模式，完成后自动退出）
    //    * 双击打开窗口，把文件拖进窗口区域
    //  特性：
    //    * PerMonitorV2 DPI 感知（高分辨率下不模糊）
    //    * Win11 Mica 毛玻璃背景 + 深色模式跟随系统（WinUI 观感）
    //    * 内置 7-Zip（7z.exe/7z.dll，LGPL），.7z/.rar 无需额外安装
    //    * 多个 exe 时弹出选择窗口，不再盲选
    //  编译（Windows 自带 .NET Framework csc）：
    //    csc /nologo /target:winexe /codepage:65001
    //        /win32icon:PortableDropper.ico
    //        /win32manifest:PortableDropper.manifest
    //        /r:System.IO.Compression.FileSystem.dll /r:Microsoft.VisualBasic.dll /r:Microsoft.CSharp.dll
    //        /res:7z.exe,PortableDropper.7zexe /res:7z.dll,PortableDropper.7zdll
    //        /out:PortableDropper.exe PortableDropper.cs
    // ============================================================

    // ------------------------------------------------------------
    //  主题（Win11 Mica / 深色跟随系统）
    // ------------------------------------------------------------
    internal static class Theme
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38; // 2 = Mica, 3 = MicaAlt, 4 = Acrylic

        public static bool IsDark()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("AppsUseLightTheme", 1);
                        return (v is int) && ((int)v) == 0;
                    }
                }
            }
            catch { }
            return false;
        }

        // 对窗口应用 Mica 背景 + 深色标题栏（旧系统上自动忽略，不报错）
        public static void Apply(IntPtr hwnd)
        {
            bool dark = IsDark();
            try
            {
                int v = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
            }
            catch { }
            try
            {
                int v = 2; // Mica
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref v, sizeof(int));
            }
            catch { }
        }

        public static Color Back { get { return IsDark() ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243); } }
        public static Color PanelBack { get { return IsDark() ? Color.FromArgb(44, 44, 48) : Color.FromArgb(255, 255, 255); } }
        public static Color PanelBackAlt { get { return IsDark() ? Color.FromArgb(38, 38, 42) : Color.FromArgb(238, 247, 255); } }
        public static Color Text { get { return IsDark() ? Color.FromArgb(242, 242, 242) : Color.FromArgb(16, 16, 16); } }
    }

    // ------------------------------------------------------------
    //  多 exe 选择窗口
    // ------------------------------------------------------------
    internal class ExePickerForm : Form
    {
        private readonly List<string> _exes;
        private readonly ListBox _list;
        public string SelectedPath { get; private set; }

        public ExePickerForm(string appName, List<string> exes, string defaultPath)
        {
            _exes = exes;
            Text = "选择主程序 - " + appName;
            Width = 480;
            Height = 400;
            MinimumSize = new Size(380, 300);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;

            var lbl = new Label
            {
                Text = "文件夹里发现多个 exe，请选择主程序（快捷方式将指向它）：",
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(12, 10, 0, 0)
            };

            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                BackColor = Theme.PanelBack,
                ForeColor = Theme.Text,
                ItemHeight = 40,
                Font = new Font(Font.FontFamily, 10.5F)
            };
            _list.Items.AddRange(exes.Select(p => Path.GetFileName(p)).ToArray());
            _list.SelectedIndex = Math.Max(0, exes.IndexOf(defaultPath));

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            var ok = new Button { Text = "确定", AutoSize = true, Location = new Point(160, 12) };
            ok.Click += (s, e) => Finish(true);
            var skip = new Button { Text = "跳过快捷方式", AutoSize = true, Location = new Point(248, 12) };
            skip.Click += (s, e) => Finish(false);
            bottom.Controls.Add(ok);
            bottom.Controls.Add(skip);

            _list.DoubleClick += (s, e) => Finish(true);
            AcceptButton = ok;

            Controls.Add(_list);
            Controls.Add(lbl);
            Controls.Add(bottom);
        }

        private void Finish(bool chosen)
        {
            if (chosen && _list.SelectedIndex >= 0) SelectedPath = _exes[_list.SelectedIndex];
            DialogResult = chosen && SelectedPath != null ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Options opt = Options.Parse(args);

            // 没有参数 → 打开图形窗口
            if (opt.Items.Count == 0)
            {
                Application.Run(new MainForm(new Engine(opt), opt));
                return;
            }

            Engine engine = new Engine(opt);
            engine.OnLog += line => { };

            // 批处理模式（拖到 exe 图标）：处理完自动退出
            if (!opt.ShowGui)
            {
                foreach (string item in opt.Items)
                {
                    engine.ProcessItem(item);
                }
                if (opt.LogPath != null)
                {
                    try { File.WriteAllLines(opt.LogPath, engine.Log); } catch { }
                }
                return;
            }

            // -Gui：打开窗口并顺带处理传入项
            Application.Run(new MainForm(engine, opt));
        }
    }

    internal class Options
    {
        public List<string> Items = new List<string>();
        public string Destination = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
        public string StartMenuFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs");
        public bool KeepArchive;
        public bool NoShortcut;
        public bool AutoPick;   // 多个 exe 时不弹窗，直接取启发式结果
        public bool ShowGui;
        public string LogPath;

        public static Options Parse(string[] args)
        {
            Options o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.Equals("-Keep", StringComparison.OrdinalIgnoreCase)) o.KeepArchive = true;
                else if (a.Equals("-NoShortcut", StringComparison.OrdinalIgnoreCase)) o.NoShortcut = true;
                else if (a.Equals("-AutoPick", StringComparison.OrdinalIgnoreCase)) o.AutoPick = true;
                else if (a.Equals("-Gui", StringComparison.OrdinalIgnoreCase)) o.ShowGui = true;
                else if (a.Equals("-Destination", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) o.Destination = args[++i];
                else if (a.Equals("-StartMenuFolder", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) o.StartMenuFolder = args[++i];
                else if (a.Equals("-Log", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) o.LogPath = args[++i];
                else o.Items.Add(a);
            }
            return o;
        }
    }

    // ============================================================
    //  核心引擎
    // ============================================================
    internal class Engine
    {
        private static readonly string[] ArchiveExts =
            { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".tbz2", ".txz" };

        private static readonly Regex BadExe = new Regex(
            @"(?i)(unins|uninstall|uninst|setup|install|redist|helper|crash|update|patch|repair|卸载)");

        public readonly Options Opt;
        public readonly List<string> Log = new List<string>();
        private readonly object _lock = new object();

        public event Action<string> OnLog;

        public Engine(Options opt) { Opt = opt; }

        private void AddLog(string line)
        {
            lock (_lock) Log.Add(line);
            Action<string> h = OnLog;
            if (h != null) h(line);
        }

        public void RunProcessAsync(string[] items, Action done)
        {
            var bw = new BackgroundWorker();
            bw.DoWork += (s, e) => { foreach (string it in items) ProcessItem(it); };
            bw.RunWorkerCompleted += (s, e) => { Action d = done; if (d != null) d(); };
            bw.RunWorkerAsync();
        }

        // --------------------------------------------------------
        //  处理单个拖入项
        // --------------------------------------------------------
        public void ProcessItem(string raw)
        {
            try
            {
                if (!File.Exists(raw) && !Directory.Exists(raw))
                {
                    AddLog("✖ 路径不存在: " + raw);
                    return;
                }

                string full = Path.GetFullPath(raw);
                bool isDir = (File.GetAttributes(full) & FileAttributes.Directory) == FileAttributes.Directory;
                string name = Path.GetFileName(full.TrimEnd('\\', '/'));

                if (isDir)
                {
                    string parent = Path.GetDirectoryName(full);
                    if (parent != null && string.Equals(parent.TrimEnd('\\'), Opt.Destination.TrimEnd('\\'),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        AddLog("· 已在目标目录，跳过移动： " + name);
                        TryShortcut(full, name);
                        return;
                    }
                    string target = UniquePath(Path.Combine(Opt.Destination, name));
                    MoveDirectory(full, target);
                    AddLog("✔ 已移动文件夹 → " + target);
                    TryShortcut(target, name);
                    return;
                }

                string ext = Path.GetExtension(full).ToLowerInvariant();
                if (Array.IndexOf(ArchiveExts, ext) >= 0)
                {
                    string appName = StripName(name);
                    string target = UniquePath(Path.Combine(Opt.Destination, appName));
                    Directory.CreateDirectory(target);
                    if (!Extract(full, target, ext))
                    {
                        AddLog("✖ 解压失败: " + name);
                        try { Directory.Delete(target, true); } catch { }
                        return;
                    }
                    AddLog("✔ 已解压 " + name + " → " + target);
                    if (!Opt.KeepArchive)
                    {
                        if (Recycle(full)) AddLog("· 原压缩包已移入回收站");
                        else AddLog("△ 未能回收原压缩包，已保留在原处: " + full);
                    }
                    TryShortcut(target, appName);
                    return;
                }

                // 普通文件：直接移动
                string dest = UniquePath(Path.Combine(Opt.Destination, name));
                MoveFile(full, dest);
                AddLog("✔ 已移动文件 → " + dest);
                if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    TryShortcut(Path.GetDirectoryName(dest), Path.GetFileNameWithoutExtension(name));
            }
            catch (Exception ex)
            {
                AddLog("✖ 失败: " + raw + " → " + ex.Message);
            }
        }

        // --------------------------------------------------------
        //  解压（zip 原生；7z/rar 用内置 7-Zip）
        // --------------------------------------------------------
        private bool Extract(string src, string dest, string ext)
        {
            try
            {
                if (ext == ".zip")
                {
                    ZipFile.ExtractToDirectory(src, dest);
                    return true;
                }
                if (ext == ".7z" || ext == ".rar")
                {
                    string z;
                    if (Ensure7Zip(out z))
                    {
                        if (Run(z, "x -y -o\"" + dest + "\" \"" + src + "\"")) return true;
                    }
                    string installed = Find7zInstalled();
                    if (installed != null)
                    {
                        AddLog("△ 内置 7-Zip 失败，改用系统安装的 7-Zip");
                        return Run(installed, "x -y -o\"" + dest + "\" \"" + src + "\"");
                    }
                    AddLog("△ 未找到可用的 7-Zip（内置资源缺失或运行失败）");
                    return false;
                }
                // tar / gz / bz2 / xz / tgz / tbz2 / txz（Windows 10+ 自带 bsdtar）
                return Run("tar.exe", "-xf \"" + src + "\" -C \"" + dest + "\"");
            }
            catch (Exception ex)
            {
                AddLog("△ 解压异常: " + ex.Message);
                return false;
            }
        }

        // 从自身资源提取内置 7-Zip（7z.exe + 7z.dll）到 %TEMP%\PortableDropper\7zip
        private static bool Ensure7Zip(out string exePath)
        {
            exePath = null;
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "PortableDropper", "7zip");
                Directory.CreateDirectory(dir);
                Assembly asm = typeof(Program).Assembly;
                string[] resourceName = { "PortableDropper.7zexe", "PortableDropper.7zdll" };
                foreach (string res in resourceName)
                {
                    using (Stream s = asm.GetManifestResourceStream(res))
                    {
                        if (s == null) return false; // 未内嵌（重新编译时漏了 /res）
                        string fp = Path.Combine(dir, res.EndsWith(".7zexe") ? "7z.exe" : "7z.dll");
                        if (!File.Exists(fp) || new FileInfo(fp).Length != s.Length)
                        {
                            using (FileStream f = File.Create(fp))
                            {
                                s.CopyTo(f);
                            }
                        }
                    }
                }
                exePath = Path.Combine(dir, "7z.exe");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Find7zInstalled()
        {
            string pfx = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string[] candidates =
            {
                Path.Combine(pfx, "7-Zip", "7z.exe"),
                Path.Combine(pfx86, "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "7-Zip", "7z.exe")
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static bool Run(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        // --------------------------------------------------------
        //  快捷方式
        // --------------------------------------------------------
        private void TryShortcut(string dir, string appName)
        {
            if (Opt.NoShortcut) return;
            try
            {
                List<string> root = RootExes(dir);
                List<string> candidates = root.Count > 0
                    ? root
                    : RecursiveExes(dir, appName);

                if (candidates.Count == 0)
                {
                    AddLog("△ 未找到主程序 (.exe)，未创建快捷方式: " + dir);
                    return;
                }

                string exe;
                if (candidates.Count == 1)
                {
                    exe = candidates[0];
                }
                else if (Opt.AutoPick)
                {
                    exe = PickMainExe(dir, appName);
                    AddLog("· 多个 exe，自动选择 (-AutoPick): " + Path.GetFileName(exe));
                }
                else
                {
                    // 弹出选择窗口让用户自己定
                    string heuristic = PickMainExe(dir, appName);
                    using (var picker = new ExePickerForm(appName, candidates, heuristic))
                    {
                        if (picker.ShowDialog() != DialogResult.OK || picker.SelectedPath == null)
                        {
                            AddLog("· 用户取消了快捷方式创建: " + dir);
                            return;
                        }
                        exe = picker.SelectedPath;
                    }
                    AddLog("· 手动选择主程序: " + Path.GetFileName(exe));
                }

                Directory.CreateDirectory(Opt.StartMenuFolder);
                string clean = CleanName(appName);
                string lnk = UniquePath(Path.Combine(Opt.StartMenuFolder, clean + ".lnk"));

                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                dynamic sc = shell.CreateShortcut(lnk);
                sc.TargetPath = exe;
                sc.WorkingDirectory = Path.GetDirectoryName(exe);
                sc.IconLocation = exe + ",0";
                sc.Description = clean + "（便携版）";
                sc.Save();
                AddLog("✔ 快捷方式: " + lnk + "  →  " + Path.GetFileName(exe));
            }
            catch (Exception ex)
            {
                AddLog("△ 创建快捷方式失败: " + ex.Message);
            }
        }

        // 根目录下的候选 exe（已排除 unins/setup/helper 等）
        private static List<string> RootExes(string dir)
        {
            try
            {
                return Directory.GetFiles(dir, "*.exe")
                    .Where(f => !BadExe.IsMatch(Path.GetFileNameWithoutExtension(f)))
                    .OrderBy(f => Path.GetFileNameWithoutExtension(f).Length)
                    .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        // 根目录没有 exe 时，递归子目录找名称包含应用名的
        private static List<string> RecursiveExes(string dir, string appName)
        {
            try
            {
                string key = KeyOf(appName);
                return Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                    .Where(f => !BadExe.IsMatch(Path.GetFileNameWithoutExtension(f)))
                    .Where(f => KeyOf(f).Contains(key))
                    .OrderBy(f => f.Length)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        // 启发式主程序选择（多 exe 时作为弹窗默认项 / -AutoPick 的直接结果）：
        //   1) 名称完全匹配  2) 以前缀匹配  3) 名称包含（取最短）  4) 取名字最短
        private static string PickMainExe(string dir, string appName)
        {
            string appKey = KeyOf(appName);
            List<string> root = RootExes(dir);
            if (root.Count > 0)
            {
                List<string> exact = root.Where(f => KeyOf(f) == appKey).ToList();
                if (exact.Count > 0) return exact[0];
                List<string> prefix = root.Where(f => KeyOf(f).StartsWith(appKey, StringComparison.Ordinal)).ToList();
                if (prefix.Count > 0) return prefix[0];
                List<string> contains = root.Where(f => KeyOf(f).Contains(appKey)).ToList();
                if (contains.Count > 0) return contains[0];
                return root[0];
            }
            List<string> sub = RecursiveExes(dir, appName);
            return sub.Count > 0 ? sub[0] : null;
        }

        private static string KeyOf(string exePath)
        {
            return Normalize(CleanName(Path.GetFileNameWithoutExtension(exePath)));
        }

        private static string Normalize(string s)
        {
            return s.Replace(" ", "").Replace("-", "").Replace("_", "").Replace(".", "")
                .ToLowerInvariant();
        }

        // 快捷方式命名 / exe 匹配时移除的常见后缀
        private static readonly string[] NameSuffixes =
        {
            // 平台 / 架构
            "x64", "x86", "amd64", "arm64", "win64", "win32", "win10", "win11", "windows", "win",
            "64bit", "32bit", "64", "32",
            // 形态标签
            "portable", "portableapps", "green", "free", "setup", "installer",
            // 发布渠道
            "stable", "release", "final", "beta", "alpha", "preview"
        };

        // 去掉名字末尾的版本号与常见后缀（带分隔符才移除），
        // 例如: Obsidian-1.6.7-win-x64 → Obsidian；LocalSend_v1.16.1_windows-x64 → LocalSend
        private static string CleanName(string name)
        {
            string n = name;
            bool changed;
            do
            {
                changed = false;
                Match m = Regex.Match(n, @"[-_. ]v?\d+(\.\d+)+$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    n = n.Substring(0, m.Index);
                    changed = true;
                    continue;
                }
                foreach (string sfx in NameSuffixes)
                {
                    if (Regex.IsMatch(n, @"[-_. ]" + Regex.Escape(sfx) + "$", RegexOptions.IgnoreCase))
                    {
                        n = n.Substring(0, n.Length - sfx.Length - 1);
                        changed = true;
                        break;
                    }
                }
            } while (changed);
            return string.IsNullOrWhiteSpace(n.Trim()) ? name : n.Trim();
        }

        // --------------------------------------------------------
        //  工具函数
        // --------------------------------------------------------
        private static string StripName(string name)
        {
            string n = Path.GetFileNameWithoutExtension(name);
            if (n.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(0, n.Length - 4);
            return n;
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 2; ; i++)
            {
                string cand = Path.Combine(dir, string.Format("{0} ({1}){2}", name, i, ext));
                if (!File.Exists(cand) && !Directory.Exists(cand)) return cand;
            }
        }

        private static void MoveDirectory(string src, string dst)
        {
            try
            {
                Directory.Move(src, dst);
            }
            catch
            {
                CopyDirectory(src, dst);
                Directory.Delete(src, true);
            }
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (string d in Directory.GetDirectories(src))
                CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        private static void MoveFile(string src, string dst)
        {
            try
            {
                File.Move(src, dst);
            }
            catch
            {
                File.Copy(src, dst, true);
                File.Delete(src);
            }
        }

        private static bool Recycle(string path)
        {
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // ============================================================
    //  图形窗口
    // ============================================================
    internal class MainForm : Form
    {
        private readonly Engine _engine;
        private readonly Options _opt;
        private TextBox _log;
        private Label _status;

        public MainForm(Engine engine, Options opt)
        {
            _engine = engine;
            _opt = opt;
            BuildUi();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Apply(Handle); // Mica + 深色标题栏（Win11）
        }

        private void BuildUi()
        {
            Text = "便携软件安装器 PortableDropper";
            Width = 640;
            Height = 480;
            MinimumSize = new Size(480, 360);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _engine.OnLog += line =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(() => Append(line)));
                else Append(line);
            };

            var drop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                AllowDrop = true,
                BackColor = Theme.PanelBackAlt
            };
            drop.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            drop.DragDrop += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    ProcessPaths(files);
                }
            };
            drop.Paint += (s, e) =>
            {
                using (var b = new SolidBrush(Theme.Text))
                {
                    e.Graphics.DrawString(
                        "把文件夹或压缩包拖到这里，或直接拖到本程序图标上\n" +
                        "支持: 文件夹 / .zip / .7z / .rar（内置 7-Zip，无需安装）/ .tar .gz 等\n" +
                        "自动装入 " + _opt.Destination + "\n" +
                        "开始菜单快捷方式自动创建；多个 exe 时会弹窗让你选择",
                        Font, b, 22, 30);
                }
            };

            _log = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Theme.PanelBack,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None
            };

            _status = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Text = "目标目录: " + _opt.Destination
            };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 42 };
            var btnOpen = new Button { Text = "打开目标目录", AutoSize = true, Location = new Point(10, 9) };
            btnOpen.Click += (s, e) => { try { Process.Start("explorer.exe", _opt.Destination); } catch { } };
            var btnClear = new Button { Text = "清空日志", AutoSize = true, Location = new Point(140, 9) };
            btnClear.Click += (s, e) => _log.Clear();
            var btnQuit = new Button { Text = "退出", AutoSize = true, Location = new Point(250, 9) };
            btnQuit.Click += (s, e) => Close();
            bottom.Controls.Add(btnOpen);
            bottom.Controls.Add(btnClear);
            bottom.Controls.Add(btnQuit);

            Controls.Add(_log);
            Controls.Add(drop);
            Controls.Add(bottom);
            Controls.Add(_status);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_opt.Items.Count > 0) ProcessPaths(_opt.Items.ToArray());
        }

        private void ProcessPaths(string[] paths)
        {
            Append("==== 开始处理 " + paths.Length + " 项 ====");
            _status.Text = "处理中…";
            _engine.RunProcessAsync(paths, () =>
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action(() => _status.Text = "处理完成，详见日志"));
            });
        }

        private void Append(string line)
        {
            _log.AppendText(line + Environment.NewLine);
        }
    }
}
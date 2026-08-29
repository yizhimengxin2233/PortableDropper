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
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PortableDropper
{
    // ============================================================
    //  PortableDropper - 便携软件安装器 / portable app installer
    //  拖拽文件夹 / 压缩包 → 装入 %LOCALAPPDATA%\Programs，生成开始菜单快捷方式，
    //  并注册到「应用和功能」列表（内置卸载器、-List 清单、多 exe 弹窗选择）。
    //  中英双语：跟随系统语言自动切换；窗口内可手动切换；命令行 -Lang zh|en 覆盖。
    //  使用方式：
    //    * 把文件夹或压缩包直接拖到本 exe 图标上（批处理模式，完成后自动退出）
    //    * 双击打开窗口，把文件拖进窗口区域
    //    * 命令行：-List / -Uninstall <名称> / -Desktop / -NoRegister / -KeepFiles / -NoShortcut / -Lang zh|en
    //  特性：
    //    * PerMonitorV2 DPI 感知（高分辨率不模糊）
    //    * 深色模式跟随系统（Win11 深色标题栏）
    //    * 内置 7-Zip（7z.exe/7z.dll，LGPL）：.7z/.rar 无需额外安装
    //    * 多个 exe 时弹出选择窗口；无 exe 时支持 .bat/.cmd/.vbs
    //    * 自动清理名称后缀（x64/windows/版本号…）
    //  许可证：本项目代码 MIT；内嵌 7-Zip 为 LGPL（见 THIRD-PARTY-NOTICES.md）
    //  编译（Windows 自带 .NET Framework csc）：
    //    csc /nologo /target:winexe /codepage:65001
    //        /win32icon:PortableDropper.ico /win32manifest:PortableDropper.manifest
    //        /r:System.IO.Compression.FileSystem.dll /r:Microsoft.VisualBasic.dll /r:Microsoft.CSharp.dll
    //        /res:7z.exe,PortableDropper.7zexe /res:7z.dll,PortableDropper.7zdll
    //        /out:PortableDropper.exe PortableDropper.cs
    // ============================================================

    // ------------------------------------------------------------
    //  主题（深色标题栏跟随系统）
    // ------------------------------------------------------------
    internal static class Theme
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static bool IsDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
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

        public static void Apply(IntPtr hwnd)
        {
            // 仅应用深色标题栏（跟随系统深浅色）；Mica 毛玻璃效果已移除
            bool dark = IsDark();
            try
            {
                int v = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
            }
            catch { }
        }

        public static Color Back { get { return IsDark() ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243); } }
        public static Color PanelBack { get { return IsDark() ? Color.FromArgb(44, 44, 48) : Color.FromArgb(255, 255, 255); } }
        public static Color PanelBackAlt { get { return IsDark() ? Color.FromArgb(38, 38, 42) : Color.FromArgb(238, 247, 255); } }
        public static Color Text { get { return IsDark() ? Color.FromArgb(242, 242, 242) : Color.FromArgb(16, 16, 16); } }
    }

    // ------------------------------------------------------------
    //  国际化（中文主 / 英文可选）
    // ------------------------------------------------------------
    internal static class L10n
    {
        public static bool Zh = true;

        public static void Init(string lang)
        {
            if (!string.IsNullOrEmpty(lang))
            {
                Zh = lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                return;
            }
            try
            {
                Zh = Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            }
            catch { Zh = true; }
        }

        public static string T(string key, params object[] args)
        {
            string s;
            if (Zh) { if (!ZhMap.TryGetValue(key, out s)) s = key; }
            else { if (!EnMap.TryGetValue(key, out s)) s = key; }
            if (args != null && args.Length > 0)
            {
                try { s = string.Format(s, args); } catch { }
            }
            return s;
        }

        private static readonly Dictionary<string, string> EnMap = new Dictionary<string, string>
        {
            // 主窗口
            { "title", "PortableDropper" },
            { "dropHint1", "Drag a folder or archive here, or onto the program icon" },
            { "dropHint2", "Supports: folders / .zip / .7z / .rar (built-in 7-Zip) / .tar .gz etc." },
            { "dropHint3", "Installs into {0}" },
            { "dropHint4", "Auto-creates a Start Menu shortcut and registers into Apps & features; a picker appears when several executables are found" },
            { "btnOpenDir", "Open target folder" },
            { "btnManage", "Manage registered apps" },
            { "btnClearLog", "Clear log" },
            { "btnQuit", "Exit" },
            { "chkDesktop", "Also create a desktop shortcut" },
            { "statusTarget", "Target folder: {0}" },
            { "statusWorking", "Working…" },
            { "statusDone", "Done — see log below" },
            { "startProcessing", "==== Processing {0} item(s) ====" },
            // 选择窗口
            { "pickerTitle", "Choose main program - {0}" },
            { "pickerHint", "Multiple executables found — choose the main program:" },
            { "btnOk", "OK" },
            { "btnSkipShortcut", "Skip shortcut" },
            // 管理窗口
            { "manageTitle", "Registered portable apps" },
            { "colName", "Name" },
            { "colVersion", "Version" },
            { "colPublisher", "Publisher" },
            { "colLocation", "Location" },
            { "btnUninstallSel", "Uninstall selected" },
            { "btnOpenFolder", "Open folder" },
            { "btnRefresh", "Refresh" },
            { "btnClose", "Close" },
            { "confirmUninstall", "Uninstall {0}?\n(This removes the registry entry, shortcuts and the app folder)" },
            // 日志
            { "errNoPath", "✖ Path does not exist: {0}" },
            { "alreadyInTarget", "· Already in the target folder, skipping move: {0}" },
            { "movedDir", "✔ Moved folder → {0}" },
            { "extractFail", "✖ Extraction failed: {0}" },
            { "extracted", "✔ Extracted {0} → {1}" },
            { "recycled", "· Original archive moved to Recycle Bin" },
            { "recycleFailed", "△ Could not recycle the original archive, kept at: {0}" },
            { "movedFile", "✔ Moved file → {0}" },
            { "fail", "✖ Failed: {0} → {1}" },
            { "fallback7z", "△ Embedded 7-Zip failed; falling back to installed 7-Zip" },
            { "no7z", "△ No usable 7-Zip found (embedded resources missing or failed)" },
            { "extractError", "△ Extraction error: {0}" },
            { "noMain", "△ No main program (.exe/.bat/.cmd/.vbs) found, no shortcut created: {0}" },
            { "autoPick", "· Multiple executables, auto-picked (-AutoPick): {0}" },
            { "pickCancelled", "· Shortcut creation cancelled by user: {0}" },
            { "pickedManual", "· Manually chosen main program: {0}" },
            { "shortcut", "✔ Shortcut: {0} → {1}" },
            { "desktopShortcut", "✔ Desktop shortcut: {0}" },
            { "desktopFail", "△ Failed to create desktop shortcut: {0}" },
            { "shortcutFail", "△ Failed to create shortcut: {0}" },
            { "registered", "✔ Registered to Apps & features: {0} ({1} / {2})" },
            { "registerFail", "△ Failed to register to Apps & features: {0}" },
            { "uninstNotFound", "✖ No registered app found: {0}" },
            { "uninstRemoved", "✔ Removed Apps & features entry: {0}" },
            { "delDesktopShortcut", "✔ Deleted desktop shortcut: {0}" },
            { "keepFiles", "· Files kept (-KeepFiles): {0}" },
            { "deletedFile", "✔ Deleted file: {0}" },
            { "skipFileDel", "△ Skipped deletion: could not locate the target file" },
            { "deletedFolder", "✔ Deleted folder: {0}" },
            { "skipFolderDel", "△ Skipped folder deletion (outside the target folder): {0}" },
            { "uninstFail", "✖ Uninstall failed: {0}" },
            { "delShortcut", "✔ Deleted shortcut: {0}" },
            { "listEmpty", "(no apps registered via PortableDropper)" },
            { "pubDefault", "Portable" },
            { "lnkDesc", "{0} (portable)" }
        };

        private static readonly Dictionary<string, string> ZhMap = new Dictionary<string, string>
        {
            { "title", "便携软件安装器 PortableDropper" },
            { "dropHint1", "把文件夹或压缩包拖到这里，或直接拖到本程序图标上" },
            { "dropHint2", "支持: 文件夹 / .zip / .7z / .rar（内置 7-Zip）/ .tar .gz 等" },
            { "dropHint3", "自动装入 {0}" },
            { "dropHint4", "自动生成开始菜单快捷方式并注册到「应用和功能」；多个 exe 会弹窗让你选择" },
            { "btnOpenDir", "打开目标目录" },
            { "btnManage", "管理已注册应用" },
            { "btnClearLog", "清空日志" },
            { "btnQuit", "退出" },
            { "chkDesktop", "同时创建桌面快捷方式" },
            { "statusTarget", "目标目录: {0}" },
            { "statusWorking", "处理中…" },
            { "statusDone", "处理完成，详见日志" },
            { "startProcessing", "==== 开始处理 {0} 项 ====" },
            { "pickerTitle", "选择主程序 - {0}" },
            { "pickerHint", "文件夹里发现多个可执行文件，请选择主程序：" },
            { "btnOk", "确定" },
            { "btnSkipShortcut", "跳过快捷方式" },
            { "manageTitle", "已注册的便携应用" },
            { "colName", "名称" },
            { "colVersion", "版本" },
            { "colPublisher", "发布者" },
            { "colLocation", "位置" },
            { "btnUninstallSel", "卸载所选" },
            { "btnOpenFolder", "打开目录" },
            { "btnRefresh", "刷新" },
            { "btnClose", "关闭" },
            { "confirmUninstall", "确定卸载 {0} ？\n（将删除注册项、快捷方式及所在文件夹）" },
            { "errNoPath", "✖ 路径不存在: {0}" },
            { "alreadyInTarget", "· 已在目标目录，跳过移动： {0}" },
            { "movedDir", "✔ 已移动文件夹 → {0}" },
            { "extractFail", "✖ 解压失败: {0}" },
            { "extracted", "✔ 已解压 {0} → {1}" },
            { "recycled", "· 原压缩包已移入回收站" },
            { "recycleFailed", "△ 未能回收原压缩包，已保留在原处: {0}" },
            { "movedFile", "✔ 已移动文件 → {0}" },
            { "fail", "✖ 失败: {0} → {1}" },
            { "fallback7z", "△ 内置 7-Zip 失败，改用系统安装的 7-Zip" },
            { "no7z", "△ 未找到可用的 7-Zip（内置资源缺失或运行失败）" },
            { "extractError", "△ 解压异常: {0}" },
            { "noMain", "△ 未找到主程序 (.exe/.bat/.cmd/.vbs)，未创建快捷方式: {0}" },
            { "autoPick", "· 多个可执行文件，自动选择 (-AutoPick): {0}" },
            { "pickCancelled", "· 用户取消了快捷方式创建: {0}" },
            { "pickedManual", "· 手动选择主程序: {0}" },
            { "shortcut", "✔ 快捷方式: {0}  →  {1}" },
            { "desktopShortcut", "✔ 桌面快捷方式: {0}" },
            { "desktopFail", "△ 桌面快捷方式创建失败: {0}" },
            { "shortcutFail", "△ 创建快捷方式失败: {0}" },
            { "registered", "✔ 已注册到「应用和功能」: {0}  ({1} / {2})" },
            { "registerFail", "△ 注册「应用和功能」失败: {0}" },
            { "uninstNotFound", "✖ 未找到已注册应用: {0}" },
            { "uninstRemoved", "✔ 已移除「应用和功能」注册项: {0}" },
            { "delDesktopShortcut", "✔ 已删除桌面快捷方式: {0}" },
            { "keepFiles", "· 已保留文件（-KeepFiles）: {0}" },
            { "deletedFile", "✔ 已删除文件: {0}" },
            { "skipFileDel", "△ 跳过删除：无法定位目标文件" },
            { "deletedFolder", "✔ 已删除文件夹: {0}" },
            { "skipFolderDel", "△ 跳过文件夹删除（位置不在目标目录内）: {0}" },
            { "uninstFail", "✖ 卸载失败: {0}" },
            { "delShortcut", "✔ 已删除快捷方式: {0}" },
            { "listEmpty", "（没有已通过 PortableDropper 注册的应用）" },
            { "pubDefault", "绿色软件" },
            { "lnkDesc", "{0}（便携版）" }
        };
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
            Text = L10n.T("pickerTitle", appName);
            Width = 480;
            Height = 400;
            MinimumSize = new Size(380, 300);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;

            var lbl = new Label
            {
                Text = L10n.T("pickerHint"),
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
            var ok = new Button { Text = L10n.T("btnOk"), AutoSize = true, Location = new Point(160, 12) };
            ok.Click += (s, e) => Finish(true);
            var skip = new Button { Text = L10n.T("btnSkipShortcut"), AutoSize = true, Location = new Point(248, 12) };
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

    // ------------------------------------------------------------
    //  已注册应用管理窗口
    // ------------------------------------------------------------
    internal class AppsListForm : Form
    {
        private readonly Engine _engine;
        private ListView _list;

        public AppsListForm(Engine engine)
        {
            _engine = engine;
            Text = L10n.T("manageTitle");
            Width = 720;
            Height = 420;
            MinimumSize = new Size(560, 300);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.PanelBack,
                ForeColor = Theme.Text
            };
            _list.Columns.Add(L10n.T("colName"), 180);
            _list.Columns.Add(L10n.T("colVersion"), 90);
            _list.Columns.Add(L10n.T("colPublisher"), 130);
            _list.Columns.Add(L10n.T("colLocation"), 270);
            _list.DoubleClick += (s, e) => OpenSelected();

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 46 };
            var btnUn = new Button { Text = L10n.T("btnUninstallSel"), AutoSize = true, Location = new Point(10, 10) };
            btnUn.Click += (s, e) => UninstallSelected();
            var btnOpen = new Button { Text = L10n.T("btnOpenFolder"), AutoSize = true, Location = new Point(120, 10) };
            btnOpen.Click += (s, e) => OpenSelected();
            var btnRefresh = new Button { Text = L10n.T("btnRefresh"), AutoSize = true, Location = new Point(210, 10) };
            btnRefresh.Click += (s, e) => Reload();
            var btnClose = new Button { Text = L10n.T("btnClose"), AutoSize = true, Location = new Point(280, 10) };
            btnClose.Click += (s, e) => Close();
            bottom.Controls.Add(btnUn);
            bottom.Controls.Add(btnOpen);
            bottom.Controls.Add(btnRefresh);
            bottom.Controls.Add(btnClose);

            Controls.Add(_list);
            Controls.Add(bottom);
            Reload();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Apply(Handle);
        }

        private void Reload()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (string[] r in _engine.ListRegistered())
            {
                var it = new ListViewItem(r[0]);
                it.SubItems.Add(r[1]);
                it.SubItems.Add(r[2]);
                it.SubItems.Add(r[3]);
                it.Tag = r[3];
                _list.Items.Add(it);
            }
            _list.EndUpdate();
        }

        private ListViewItem Selected()
        {
            return _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null;
        }

        private void OpenSelected()
        {
            ListViewItem it = Selected();
            if (it == null || it.Tag == null) return;
            string loc = it.Tag.ToString();
            try { if (Directory.Exists(loc)) Process.Start("explorer.exe", loc); } catch { }
        }

        private void UninstallSelected()
        {
            ListViewItem it = Selected();
            if (it == null) return;
            string name = it.Text;
            if (MessageBox.Show(L10n.T("confirmUninstall", name), "PortableDropper",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _engine.UninstallApp(name);
            Reload();
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
            L10n.Init(opt.Lang);
            Engine engine = new Engine(opt);

            // -List：列出已注册应用
            if (opt.ListMode)
            {
                List<string> lines = new List<string>();
                List<string[]> rows = engine.ListRegistered();
                if (rows.Count == 0) lines.Add(L10n.T("listEmpty"));
                foreach (string[] r in rows) lines.Add(r[0] + " | " + r[1] + " | " + r[2] + " | " + r[3]);
                foreach (string l in lines) Console.WriteLine(l);
                if (opt.LogPath != null)
                {
                    try { File.WriteAllLines(opt.LogPath, lines); } catch { }
                }
                return;
            }

            // -Uninstall <名称>：内置卸载
            if (opt.UninstallName != null)
            {
                engine.OnLog += line => { };
                engine.UninstallApp(opt.UninstallName);
                if (opt.LogPath != null)
                {
                    try { File.WriteAllLines(opt.LogPath, engine.Log); } catch { }
                }
                return;
            }

            // 没有参数 → 打开图形窗口
            if (opt.Items.Count == 0)
            {
                Application.Run(new MainForm(engine, opt));
                return;
            }

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
        public bool NoRegister;      // 不注册到「应用和功能」
        public bool AddDesktop;      // 额外创建桌面快捷方式
        public bool AutoPick;        // 多个 exe 时不弹窗，直接取启发式结果
        public bool KeepFiles;       // 卸载时保留文件
        public bool ListMode;        // -List
        public string UninstallName; // -Uninstall <名称>
        public string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public string Lang = "";     // -Lang zh|en（默认跟随系统）
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
                else if (a.Equals("-NoRegister", StringComparison.OrdinalIgnoreCase)) o.NoRegister = true;
                else if (a.Equals("-Desktop", StringComparison.OrdinalIgnoreCase)) o.AddDesktop = true;
                else if (a.Equals("-AutoPick", StringComparison.OrdinalIgnoreCase)) o.AutoPick = true;
                else if (a.Equals("-KeepFiles", StringComparison.OrdinalIgnoreCase)) o.KeepFiles = true;
                else if (a.Equals("-List", StringComparison.OrdinalIgnoreCase)) o.ListMode = true;
                else if (a.Equals("-Gui", StringComparison.OrdinalIgnoreCase)) o.ShowGui = true;
                else if (a.Equals("-Uninstall", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) o.UninstallName = args[++i];
                else if (a.Equals("-Lang", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) o.Lang = args[++i];
                else if (a.Equals("-DesktopFolder", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) o.DesktopPath = args[++i];
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

        private static readonly string[] ScriptExts =
            { "*.bat", "*.cmd", "*.vbs" };

        private static readonly Regex BadExe = new Regex(
            @"(?i)(unins|uninstall|uninst|setup|install|redist|helper|crash|update|patch|repair|卸载)");

        private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

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
                    AddLog(L10n.T("errNoPath", raw));
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
                        AddLog(L10n.T("alreadyInTarget", name));
                        TryShortcut(full, name, null);
                        return;
                    }
                    string target = UniquePath(Path.Combine(Opt.Destination, name));
                    MoveDirectory(full, target);
                    AddLog(L10n.T("movedDir", target));
                    TryShortcut(target, name, null);
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
                        AddLog(L10n.T("extractFail", name));
                        try { Directory.Delete(target, true); } catch { }
                        return;
                    }
                    AddLog(L10n.T("extracted", name, target));
                    if (!Opt.KeepArchive)
                    {
                        if (Recycle(full)) AddLog(L10n.T("recycled"));
                        else AddLog(L10n.T("recycleFailed", full));
                    }
                    TryShortcut(target, appName, null);
                    return;
                }

                // 普通文件：直接移动
                string dest = UniquePath(Path.Combine(Opt.Destination, name));
                MoveFile(full, dest);
                AddLog(L10n.T("movedFile", dest));
                if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    TryShortcut(Path.GetDirectoryName(dest), Path.GetFileNameWithoutExtension(name), dest);
            }
            catch (Exception ex)
            {
                AddLog(L10n.T("fail", raw, ex.Message));
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
                        AddLog(L10n.T("fallback7z"));
                        return Run(installed, "x -y -o\"" + dest + "\" \"" + src + "\"");
                    }
                    AddLog(L10n.T("no7z"));
                    return false;
                }
                return Run("tar.exe", "-xf \"" + src + "\" -C \"" + dest + "\"");
            }
            catch (Exception ex)
            {
                AddLog(L10n.T("extractError", ex.Message));
                return false;
            }
        }

        private static bool Ensure7Zip(out string exePath)
        {
            exePath = null;
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "PortableDropper", "7zip");
                Directory.CreateDirectory(dir);
                Assembly asm = typeof(Program).Assembly;
                foreach (string res in new[] { "PortableDropper.7zexe", "PortableDropper.7zdll" })
                {
                    using (Stream s = asm.GetManifestResourceStream(res))
                    {
                        if (s == null) return false;
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
        //  快捷方式 + 「应用和功能」注册
        // --------------------------------------------------------
        private void TryShortcut(string dir, string appName, string knownExe)
        {
            if (Opt.NoShortcut) return;
            try
            {
                List<string> candidates;
                if (knownExe != null)
                {
                    candidates = new List<string> { knownExe };
                }
                else
                {
                    List<string> root = RootExes(dir);
                    if (root.Count > 0) candidates = root;
                    else
                    {
                        List<string> sub = RecursiveExes(dir, appName);
                        if (sub.Count > 0) candidates = sub;
                        else candidates = NonExeCandidates(dir);
                    }
                }

                if (candidates.Count == 0)
                {
                    AddLog(L10n.T("noMain", dir));
                    return;
                }

                string exe;
                if (candidates.Count == 1)
                {
                    exe = candidates[0];
                }
                else if (Opt.AutoPick)
                {
                    string h = PickMainExe(dir, appName);
                    exe = h ?? candidates[0];
                    AddLog(L10n.T("autoPick", Path.GetFileName(exe)));
                }
                else
                {
                    string heuristic = PickMainExe(dir, appName) ?? candidates[0];
                    using (var picker = new ExePickerForm(appName, candidates, heuristic))
                    {
                        if (picker.ShowDialog() != DialogResult.OK || picker.SelectedPath == null)
                        {
                            AddLog(L10n.T("pickCancelled", dir));
                            return;
                        }
                        exe = picker.SelectedPath;
                    }
                    AddLog(L10n.T("pickedManual", Path.GetFileName(exe)));
                }

                string clean = CleanName(appName);
                Directory.CreateDirectory(Opt.StartMenuFolder);
                string lnk = UniquePath(Path.Combine(Opt.StartMenuFolder, clean + ".lnk"));
                CreateLnk(lnk, exe, clean);
                AddLog(L10n.T("shortcut", lnk, Path.GetFileName(exe)));

                if (Opt.AddDesktop)
                {
                    try
                    {
                        Directory.CreateDirectory(Opt.DesktopPath);
                        string dlnk = UniquePath(Path.Combine(Opt.DesktopPath, clean + ".lnk"));
                        CreateLnk(dlnk, exe, clean);
                        AddLog(L10n.T("desktopShortcut", dlnk));
                    }
                    catch (Exception dex)
                    {
                        AddLog(L10n.T("desktopFail", dex.Message));
                    }
                }

                RegisterAppsAndFeatures(exe, dir, appName);
            }
            catch (Exception ex)
            {
                AddLog(L10n.T("shortcutFail", ex.Message));
            }
        }

        private static void CreateLnk(string lnkPath, string target, string appName)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(t);
            dynamic sc = shell.CreateShortcut(lnkPath);
            sc.TargetPath = target;
            sc.WorkingDirectory = Path.GetDirectoryName(target);
            sc.IconLocation = target + ",0";
            sc.Description = L10n.T("lnkDesc", appName);
            sc.Save();
        }

        // 注册到 Windows「应用和功能」列表（HKCU，无需管理员）
        private void RegisterAppsAndFeatures(string exe, string dir, string appName)
        {
            if (Opt.NoRegister) return;
            try
            {
                string clean = CleanName(appName);
                string keyPath = UninstallRoot + "\\" + UniqueKeyName(clean);
                string self = Application.ExecutablePath;

                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(exe);
                string ver = !string.IsNullOrEmpty(vi.FileVersion) ? vi.FileVersion : vi.ProductVersion;
                if (string.IsNullOrEmpty(ver)) ver = "1.0.0";
                string pub = vi.CompanyName;
                if (string.IsNullOrEmpty(pub)) pub = L10n.T("pubDefault");

                long sizeKb = DirSizeKB(exe, dir);

                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    k.SetValue("DisplayName", clean);
                    k.SetValue("DisplayVersion", ver);
                    k.SetValue("Publisher", pub);
                    k.SetValue("DisplayIcon", exe);
                    k.SetValue("InstallLocation", dir);
                    k.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("PortableDropper", 1, RegistryValueKind.DWord);
                    k.SetValue("UninstallString", "\"" + self + "\" -Uninstall \"" + clean + "\"");
                }
                AddLog(L10n.T("registered", clean, ver, pub));
            }
            catch (Exception ex)
            {
                AddLog(L10n.T("registerFail", ex.Message));
            }
        }

        // --------------------------------------------------------
        //  卸载（-Uninstall <名称> / 管理窗口）
        // --------------------------------------------------------
        public void UninstallApp(string name)
        {
            try
            {
                string matchKey = null;
                string disp = name;
                string installLoc = null;
                string exe = null;

                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(UninstallRoot))
                {
                    if (root == null)
                    {
                        AddLog(L10n.T("uninstNotFound", name));
                        return;
                    }
                    foreach (string sub in root.GetSubKeyNames())
                    {
                        using (RegistryKey k = root.OpenSubKey(sub))
                        {
                            if (k == null) continue;
                            object pd = k.GetValue("PortableDropper");
                            if (pd == null || (int)pd != 1) continue;
                            object dn = k.GetValue("DisplayName");
                            string d = dn == null ? sub : dn.ToString();
                            if (string.Equals(d, name, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(CleanName(d), CleanName(name), StringComparison.OrdinalIgnoreCase))
                            {
                                matchKey = sub;
                                disp = d;
                                object il = k.GetValue("InstallLocation");
                                if (il != null && Directory.Exists(il.ToString())) installLoc = il.ToString();
                                object ic = k.GetValue("DisplayIcon");
                                if (ic != null) exe = ic.ToString();
                                break;
                            }
                        }
                    }
                }

                if (matchKey == null)
                {
                    AddLog(L10n.T("uninstNotFound", name));
                    return;
                }

                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(UninstallRoot, true))
                {
                    if (root != null) root.DeleteSubKeyTree(matchKey, false);
                }
                AddLog(L10n.T("uninstRemoved", disp));

                // 快捷方式（开始菜单 + 可能的 (2) 后缀 + 桌面）
                DeleteShortcutsNamed(CleanName(disp));
                try
                {
                    string de = Path.Combine(Opt.DesktopPath, CleanName(disp) + ".lnk");
                    if (File.Exists(de)) { File.Delete(de); AddLog(L10n.T("delDesktopShortcut", de)); }
                }
                catch { }

                // 文件夹 / 单文件
                if (installLoc != null)
                {
                    bool isRoot = string.Equals(installLoc.TrimEnd('\\'), Opt.Destination.TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase);
                    if (Opt.KeepFiles)
                    {
                        AddLog(L10n.T("keepFiles", installLoc));
                    }
                    else if (isRoot)
                    {
                        if (exe != null && File.Exists(exe) &&
                            string.Equals(Path.GetDirectoryName(exe).TrimEnd('\\'),
                                Opt.Destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(exe);
                            AddLog(L10n.T("deletedFile", exe));
                        }
                        else AddLog(L10n.T("skipFileDel"));
                    }
                    else
                    {
                        string parent = Path.GetDirectoryName(installLoc.TrimEnd('\\'));
                        if (parent != null && string.Equals(parent.TrimEnd('\\'),
                                Opt.Destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.Delete(installLoc, true);
                            AddLog(L10n.T("deletedFolder", installLoc));
                        }
                        else AddLog(L10n.T("skipFolderDel", installLoc));
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog(L10n.T("uninstFail", ex.Message));
            }
        }

        private void DeleteShortcutsNamed(string cleanName)
        {
            try
            {
                foreach (string f in Directory.GetFiles(Opt.StartMenuFolder, "*.lnk"))
                {
                    string baseN = Path.GetFileNameWithoutExtension(f);
                    if (baseN.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                        Regex.IsMatch(baseN, "^" + Regex.Escape(cleanName) + " \\(\\d+\\)$",
                            RegexOptions.IgnoreCase))
                    {
                        File.Delete(f);
                        AddLog(L10n.T("delShortcut", f));
                    }
                }
            }
            catch { }
        }

        // --------------------------------------------------------
        //  已注册应用清单（-List / 管理窗口）
        // --------------------------------------------------------
        public List<string[]> ListRegistered()
        {
            var rows = new List<string[]>();
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(UninstallRoot))
                {
                    if (root == null) return rows;
                    foreach (string sub in root.GetSubKeyNames())
                    {
                        using (RegistryKey k = root.OpenSubKey(sub))
                        {
                            if (k == null) continue;
                            object pd = k.GetValue("PortableDropper");
                            if (pd == null || (int)pd != 1) continue;
                            object dn = k.GetValue("DisplayName");
                            string d = dn == null ? sub : dn.ToString();
                            object v = k.GetValue("DisplayVersion");
                            object pb = k.GetValue("Publisher");
                            object il = k.GetValue("InstallLocation");
                            rows.Add(new[]
                            {
                                d,
                                v == null ? "" : v.ToString(),
                                pb == null ? "" : pb.ToString(),
                                il == null ? "" : il.ToString()
                            });
                        }
                    }
                }
            }
            catch { }
            rows.Sort((a, b) => string.CompareOrdinal(a[0], b[0]));
            return rows;
        }

        // --------------------------------------------------------
        //  候选查找
        // --------------------------------------------------------
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

        // 无 exe 时支持 .bat/.cmd/.vbs 作为主程序
        private static List<string> NonExeCandidates(string dir)
        {
            var list = new List<string>();
            try
            {
                foreach (string pat in ScriptExts)
                {
                    foreach (string f in Directory.GetFiles(dir, pat))
                    {
                        if (!BadExe.IsMatch(Path.GetFileNameWithoutExtension(f))) list.Add(f);
                    }
                }
            }
            catch { }
            return list
                .OrderBy(f => Path.GetFileNameWithoutExtension(f).Length)
                .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

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

        private static readonly string[] NameSuffixes =
        {
            "x64", "x86", "amd64", "arm64", "win64", "win32", "win10", "win11", "windows", "win",
            "64bit", "32bit", "64", "32",
            "portable", "portableapps", "green", "free", "setup", "installer",
            "stable", "release", "final", "beta", "alpha", "preview"
        };

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

        private static string UniqueKeyName(string baseName)
        {
            string key = baseName;
            for (int i = 2; ; i++)
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallRoot + "\\" + key))
                {
                    if (k == null) return key;
                }
                key = baseName + " (" + i + ")";
            }
        }

        private long DirSizeKB(string exe, string dir)
        {
            bool singleFile = string.Equals(dir.TrimEnd('\\'),
                Opt.Destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            if (singleFile)
            {
                try { return new FileInfo(exe).Length / 1024; } catch { return 0; }
            }
            long total = 0;
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return total / 1024;
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
    //  主窗口
    // ============================================================
    internal class MainForm : Form
    {
        private readonly Engine _engine;
        private readonly Options _opt;
        private TextBox _log;
        private Label _status;
        private CheckBox _chkDesktop;
        private ComboBox _langCombo;
        private Panel _drop;
        private Button _btnOpen;
        private Button _btnApps;
        private Button _btnClear;
        private Button _btnQuit;

        public MainForm(Engine engine, Options opt)
        {
            _engine = engine;
            _opt = opt;
            BuildUi();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Apply(Handle);
        }

        private void ApplyUiText()
        {
            Text = L10n.T("title");
            _btnOpen.Text = L10n.T("btnOpenDir");
            _btnApps.Text = L10n.T("btnManage");
            _btnClear.Text = L10n.T("btnClearLog");
            _btnQuit.Text = L10n.T("btnQuit");
            _chkDesktop.Text = L10n.T("chkDesktop");
            _status.Text = L10n.T("statusTarget", _opt.Destination);
            _drop.Invalidate();
        }

        private void BuildUi()
        {
            Width = 740;
            Height = 490;
            MinimumSize = new Size(560, 370);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _engine.OnLog += line =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(() => Append(line)));
                else Append(line);
            };

            _drop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                AllowDrop = true,
                BackColor = Theme.PanelBackAlt
            };
            _drop.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            _drop.DragDrop += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    ProcessPaths(files);
                }
            };
            _drop.Paint += (s, e) =>
            {
                using (var b = new SolidBrush(Theme.Text))
                {
                    e.Graphics.DrawString(
                        L10n.T("dropHint1") + "\n" +
                        L10n.T("dropHint2") + "\n" +
                        L10n.T("dropHint3", _opt.Destination) + "\n" +
                        L10n.T("dropHint4"),
                        Font, b, 22, 26);
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
                Height = 22
            };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 42 };
            _btnOpen = new Button { AutoSize = true, Location = new Point(10, 9) };
            _btnOpen.Click += (s, e) => { try { Process.Start("explorer.exe", _opt.Destination); } catch { } };
            _btnApps = new Button { AutoSize = true, Location = new Point(140, 9) };
            _btnApps.Click += (s, e) =>
            {
                using (var f = new AppsListForm(_engine)) { f.ShowDialog(this); }
            };
            _btnClear = new Button { AutoSize = true, Location = new Point(280, 9) };
            _btnClear.Click += (s, e) => _log.Clear();
            _btnQuit = new Button { AutoSize = true, Location = new Point(400, 9) };
            _btnQuit.Click += (s, e) => Close();
            _chkDesktop = new CheckBox
            {
                AutoSize = true,
                Checked = _opt.AddDesktop,
                Location = new Point(470, 12)
            };
            _langCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(640, 9),
                Width = 92
            };
            _langCombo.Items.Add("中文");
            _langCombo.Items.Add("English");
            _langCombo.SelectedIndex = L10n.Zh ? 0 : 1;
            _langCombo.SelectedIndexChanged += (s, e) =>
            {
                L10n.Zh = _langCombo.SelectedIndex == 0;
                ApplyUiText();
            };
            bottom.Controls.Add(_btnOpen);
            bottom.Controls.Add(_btnApps);
            bottom.Controls.Add(_btnClear);
            bottom.Controls.Add(_btnQuit);
            bottom.Controls.Add(_chkDesktop);
            bottom.Controls.Add(_langCombo);

            Controls.Add(_log);
            Controls.Add(_drop);
            Controls.Add(bottom);
            Controls.Add(_status);

            ApplyUiText();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_opt.Items.Count > 0) ProcessPaths(_opt.Items.ToArray());
        }

        private void ProcessPaths(string[] paths)
        {
            _opt.AddDesktop = _chkDesktop.Checked;
            Append(L10n.T("startProcessing", paths.Length));
            _status.Text = L10n.T("statusWorking");
            _engine.RunProcessAsync(paths, () =>
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action(() => _status.Text = L10n.T("statusDone")));
            });
        }

        private void Append(string line)
        {
            _log.AppendText(line + Environment.NewLine);
        }
    }
}
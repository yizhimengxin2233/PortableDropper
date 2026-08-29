## 中文

# PortableDropper v1.6.0 发布说明

**由 DeepSeek AI 助手（V4 会话）编写并验证**，C# WinForms 单文件 exe，MIT 协议。

### v1.6.0 更新内容

- **拖拽安装不再自动猜测**：每次拖入（拖到图标或拖进窗口）先弹「安装方式」询问（全新安装 / 更新 / 取消）；
  选「更新」后弹出**已注册应用列表**，由你挑一个要替换的旧版本——版本号相同/不可读的包（如 Unity 构建）也能正确更新替换。
- **更新替换**：旧文件夹自动移入回收站，快捷方式与「应用和功能」注册项原位更新（不再产生 ` (2)` 重复项）；
  命令行 `-InstallAsNew` 全新并存 / `-UpdateTarget <名称>` 显式更新目标 / `-KeepOld` 保留旧版并存。
- **卸载进回收站**：卸载时程序文件夹移入回收站（可恢复，非永久删除）；`-KeepFiles` 仍可保留文件。
- **语言选择持久化**：GUI 里选的语言保存到 HKCU 注册表，之后拖图标安装沿用同一语言；
  `-Lang zh|en` 仍可临时覆盖且不改写存档。
- **双语界面布局修复**：语言栏与各弹窗按钮全部改用 FlowLayout 自动排布，中文/英文下不再互相覆盖错位。
- **主程序识别兜底**：找不到与包名关联的 exe 时，接受任意非安装类 exe（最短路径优先），
  子目录打包（如 `build\`）的绿色软件也能正常注册。
- **一键编译脚本**：新增 `build.bat`（csc 不在 PATH，需完整路径调用）。

### v1.6.0 新增命令行参数

```
-InstallAsNew            全新安装（不自动替换同名旧版）
-UpdateTarget <名称>     更新替换指定已注册应用
```

### 使用

1. Assets 下载 `PortableDropper-win-x64.zip`，解压得单文件 `PortableDropper.exe`。
2. 把绿色软件文件夹/压缩包拖到图标上 → 选择「全新安装」或「更新」（更新再挑旧版本）。
3. 卸载：设置 → 应用和功能 → 卸载（文件夹进回收站）。

### 构建 / 许可

见 README「重新编译」（可用 `build.bat`）；本项目 MIT，内嵌 7-Zip 为 LGPL（见 THIRD-PARTY-NOTICES.md）。

---

## English

# PortableDropper v1.6.0 — Release Notes

**Written and verified by a DeepSeek AI assistant (V4 session)**, C# WinForms
single-file exe, MIT licensed.

### What's new in v1.6.0

- **No more guessing on drop**: every drop (onto the icon or into the window) first
  asks the install mode (Install as new / Update / Cancel); choosing Update shows the
  **list of registered apps** and you pick the old version to replace — works even when
  version numbers are identical or unreadable (e.g. Unity builds).
- **Update replacement**: the old folder moves to the Recycle Bin, the shortcut and the
  Apps & features entry are updated in place (no more ` (2)` duplicates);
  command line: `-InstallAsNew` side-by-side, `-UpdateTarget <name>` explicit target,
  `-KeepOld` keep the old version alongside.
- **Uninstall goes to the Recycle Bin**: the app folder is recycled (recoverable, not
  permanently deleted); `-KeepFiles` still keeps the files.
- **Language preference is persisted**: the language chosen in the GUI is saved to the
  HKCU registry, so icon-drop installs reuse it; `-Lang zh|en` still overrides for one
  run without changing the saved value.
- **Bilingual layout fixes**: the language bar and all dialog buttons now use FlowLayout,
  so nothing overlaps in Chinese or English.
- **Main-exe fallback**: when no exe matches the package name, any non-installer exe is
  accepted (shortest path first), so subfolder-packed apps (like `build\`) register correctly.
- **One-click build script**: new `build.bat` (csc is not on PATH — full path required).

### New command-line options

```
-InstallAsNew            Install as a new app (no auto-replace)
-UpdateTarget <name>     Update-replace the given registered app
```

### Usage

1. Download `PortableDropper-win-x64.zip` from Assets; extract the single-file
   `PortableDropper.exe`.
2. Drop a portable folder/archive onto the icon → choose "Install as new" or
   "Update" (then pick the old version to replace).
3. Uninstall: Settings → Apps & features → Uninstall (folder goes to the Recycle Bin).

### Build / License

See README "Rebuild" (use `build.bat`); this project is MIT, embedded 7-Zip is LGPL
(see THIRD-PARTY-NOTICES.md).
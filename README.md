# PortableDropper 便携软件安装器

> **Language**: [English](README.en.md) | 中文

> **作者注**：本工具由 **DeepSeek AI 助手**（V4 会话）编写并验证。
> 源码（`PortableDropper.cs`）为 C# 5，可直接用 Windows 自带的 .NET Framework 编译器重新编译。

把下载的**绿色软件文件夹 / 压缩包**拖进去，自动装入 `%LOCALAPPDATA%\Programs`，
生成**开始菜单快捷方式**，并**注册到 Windows「应用和功能」列表**——像管普通安装程序一样管理绿色软件。
单文件 `.exe`（内嵌图标、7-Zip 引擎、DPI 清单），零依赖、免安装。

## 文件

| 文件 | 作用 |
|---|---|
| `PortableDropper.exe` | 主程序（单文件，约 2.4MB，拷走它即可用） |
| `PortableDropper.cs` | 源码 |
| `PortableDropper.ico` / `.manifest` | 图标 / DPI 清单（编译输入） |
| `7z.exe` / `7z.dll` | 内置 7-Zip 引擎源文件（已嵌入 exe，仅编译需要） |
| `LICENSE` | 本项目代码许可（MIT） |
| `THIRD-PARTY-NOTICES.md` | 第三方组件声明（内嵌 7-Zip，LGPL） |
| `RELEASE.md` / `publish-gh.ps1` | 发布说明 / 一键发布脚本 |
| `README.md` / `README.en.md` | 本说明（中文主文档 / 英文可选） |

## 界面

- **中英双语**：默认跟随系统语言；窗口右下角下拉框可随时切换；命令行 `-Lang zh|en` 强制指定。
- **深色/浅色模式**：跟随系统主题（Win11 上为深色标题栏），高 DPI 下清晰不模糊（PerMonitorV2 + GDI 文本渲染）。
- **内置图标** + 内置**「管理已注册应用」窗口**（浏览 / 打开目录 / 卸载）。

## 用法

1. **最常用**：把文件夹或压缩包**直接拖到 `PortableDropper.exe` 图标上**——同样先弹「安装方式」询问
   （全新安装 / 更新），处理完自动退出。
2. 双击打开窗口，把文件**拖进蓝色区域**（可连拖多次；
   勾选窗口底部「**同时创建桌面快捷方式**」可额外在桌面生成快捷方式）。
   **每次拖入都会先弹出「安装方式」询问**：
   - **全新安装** → 作为独立新应用安装；
   - **更新** → 弹出「已注册应用」列表，挑一个要替换的旧版本 → 旧文件夹进回收站、
     快捷方式与注册项原位更新（不再问版本号猜新旧，由你决定）；
   - **取消** → 跳过该项。
3. 命令行（批处理/脚本）：
   ```
   PortableDropper.exe -List                      列出已注册应用
   PortableDropper.exe -Uninstall "AppName"       内置卸载（清注册项+快捷方式+文件夹）
   PortableDropper.exe "D:\下载\App.zip" -Desktop 带桌面快捷方式安装
   ```

## 处理规则

- **文件夹** → 整体移动到 `%LOCALAPPDATA%\Programs\`（剪切）。
- **压缩包** → 解压到 `Programs\<包名>\`，成功后原包自动进回收站：
  - `.zip` 原生支持；`.7z` / `.rar` **内置 7-Zip**（无需安装）；`.tar/.gz/.bz2/.xz` 系统自带。
- **其他文件**（单个 exe）→ 直接移动。
- **主程序识别** → exe 优先（排除 `unins/setup/helper` 等）；没有 exe 时支持 `.bat/.cmd/.vbs`；
  多个候选时**弹窗让你选择**（`-AutoPick` 跳过）。
- **快捷方式** → 开始菜单**根目录**，名字自动去掉版本号与常见后缀
  （`x64/windows/win/portable/stable/beta/64/32…`，如 `Obsidian-1.6.7-win-x64` → **Obsidian**）；
  **桌面快捷方式为可选项**：命令行加 `-Desktop`，或窗口里勾选「同时创建桌面快捷方式」。
- **「应用和功能」注册** → 自动写入当前用户卸载表（HKCU，无需管理员）：
  名称、版本、发布者（自动读 exe 文件信息）、图标、位置、占用大小，
  「卸载」按钮直接调用本程序 `-Uninstall` 完成清理。
- **更新替换** → **GUI 由你决定**（每次拖入弹「全新安装 / 更新」；选「更新」后从
  已注册应用列表挑旧版本，旧文件夹自动移入回收站、快捷方式与注册项原位更新）；
  命令行模式默认**同名自动替换**（`-UpdateTarget <名称>` 显式指定更新目标，
  `-InstallAsNew` 强制全新安装并存，`-KeepOld` 保留旧版并存）。
- **卸载重名清理** → `-Uninstall "名称"` 会**一次清除所有同名注册项**（含历史遗留的 ` (2)` 条目）。

## 卸载

三种方式，效果相同（移除注册项 + 开始菜单/桌面快捷方式 + 程序文件夹）：
- 设置 → 应用和功能 → 找到应用 → 卸载（会调用本程序）；
- 窗口「管理已注册应用」→ 选中 → 卸载所选；
- 命令行 `PortableDropper.exe -Uninstall "名称"`（加 `-KeepFiles` 可保留文件；
  同名多版本并存时会一次全部清理）。

## 命令行参数

| 参数 | 作用 |
|---|---|
| `-Destination <路径>` | 自定义安装目录（默认 `%LOCALAPPDATA%\Programs`） |
| `-StartMenuFolder <路径>` | 自定义快捷方式目录（默认 开始菜单\Programs） |
| `-DesktopFolder <路径>` | 自定义桌面目录（默认真实桌面） |
| `-Uninstall <名称>` | 内置卸载 |
| `-List` | 列出已注册应用（配合 `-Log` 可写文件） |
| `-Keep` | 解压后保留原压缩包 |
| `-KeepFiles` | 卸载时保留程序文件 |
| `-KeepOld` | 更新时保留旧版本，新旧并存（不替换） |
| `-NoShortcut` / `-NoRegister` | 跳过快捷方式 / 跳过「应用和功能」注册 |
| `-AutoPick` | 多 exe 时不弹窗 |
| `-InstallAsNew` | 全新安装（作为新应用，不自动替换同名旧版） |
| `-UpdateTarget <名称>` | 更新替换指定已注册应用（旧文件夹进回收站，快捷方式/注册项原位更新） |
| `-Lang zh\|en` | 界面语言（默认跟随系统） |
| `-Log <文件>` | 日志输出到文件 |
| `-Gui` | 带参数时仍打开窗口 |

## 许可证

- **本项目代码**：[MIT License](LICENSE)（Copyright (c) 2025 yizhimengxin2233），可自由使用/修改/商用，保留版权声明即可。
- **内嵌 7-Zip**（`7z.exe`/`7z.dll`）：**LGPL**（作者 Igor Pavlov），未修改、仅用于解压，
  随本工具整体分发，详见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 与 [7-Zip 官网](https://www.7-zip.org/)。

## 重新编译（可选，Windows 自带 csc，无需 SDK）

```
csc /nologo /target:winexe /codepage:65001
    /win32icon:PortableDropper.ico /win32manifest:PortableDropper.manifest
    /r:System.IO.Compression.FileSystem.dll /r:Microsoft.VisualBasic.dll /r:Microsoft.CSharp.dll
    /res:7z.exe,PortableDropper.7zexe /res:7z.dll,PortableDropper.7zdll
    /out:PortableDropper.exe PortableDropper.cs
```

## 小提示

- 绿色软件配置一般存在自己文件夹里——整体搬移不丢配置。
- 建议把 `PortableDropper.exe` 放到方便位置（如 `D:\Tools`），桌面继续不放绿色软件。
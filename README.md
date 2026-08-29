# PortableDropper 便携软件安装器

> **作者注**：本工具由 **DeepSeek AI 助手**（V4 会话）编写并验证。
> 源码（`PortableDropper.cs`）为 C# 5，可直接用 Windows 自带的 .NET Framework 编译器重新编译。

免安装小工具：把下载的**绿色软件文件夹 / 压缩包**拖进去，自动装入 `%LOCALAPPDATA%\Programs`
（即 `C:\Users\<你的用户名>\AppData\Local\Programs`）并在**开始菜单根目录**生成快捷方式。
单文件（`.exe` 内已包含图标、主题、7-Zip 引擎），无任何安装和运行时依赖。

## 文件

| 文件 | 作用 |
|---|---|
| `PortableDropper.exe` | 主程序（单文件，约 2.4MB，用完只拷走它即可） |
| `PortableDropper.cs` | 源码 |
| `PortableDropper.ico` / `.manifest` | 图标 / DPI 清单（编译输入） |
| `7z.exe` / `7z.dll` | 内置 7-Zip 引擎源文件（已嵌入 exe，仅编译需要） |
| `README.md` | 本说明 |

## 界面

- **WinUI 风格观感**：Win11 上自动启用 **Mica 毛玻璃背景** + 跟随系统的**深色/浅色模式**，
  窗口采用标准 WinUI 配色；Win10 自动降级为普通背景，不影响功能。
- **高 DPI 清晰**：PerMonitorV2 DPI 感知 + GDI 文本渲染，4K/150% 缩放下不模糊。
- **图标**：内置应用图标（蓝绿渐变 + 收纳箭头），exe 和窗口共用。

> 说明：真正的 Qt / WinUI3 无法做成单文件（Qt 要整套 DLL，WinUI3 要额外运行时），
> 所以用「WinForms + DWM(Mica) + DPI 感知」达到接近的效果，保持单文件。

## 用法（三种方式任选）

1. **最常用**：把文件夹或压缩包**直接拖到 `PortableDropper.exe` 图标上**，处理完自动退出。
2. 双击打开窗口，把文件**拖进窗口的蓝色区域**（可多次拖，窗口不关）。
3. 双击打开窗口后，点「打开目标目录」看装好的软件。

## 处理规则

- **文件夹** → 整体移动到 `%LOCALAPPDATA%\Programs\`（剪切，原位置不留）。
- **压缩包** → 解压到 `%LOCALAPPDATA%\Programs\<包名>\`，解压成功后**原压缩包自动移入回收站**。
  - `.zip`：系统原生支持。
  - `.7z` / `.rar`：**内置 7-Zip 引擎**（7z.exe/7z.dll 内嵌于 exe，LGPL），**无需安装任何东西**；
    内置引擎失效时才自动改用系统安装的 7-Zip。
  - `.tar` / `.tar.gz` / `.tgz` / `.bz2` / `.xz`：Windows 10/11 自带支持。
- **其他文件**（单个 exe 等）→ 直接移动到目标目录。
- **快捷方式** → 自动放进**开始菜单根目录**（不再单独建文件夹），名字自动**去掉版本号和常见后缀**：
  - `Obsidian-1.6.7-win-x64` → 快捷方式 **Obsidian**
  - `localsend_v1.16.1_windows-x64` → 快捷方式 **LocalSend**（文件夹名保持原样）
  - 可移除后缀：`x64 x86 amd64 arm64 win64 win32 win10 win11 windows win 64bit 32bit 64 32
    portable green free setup installer stable release final beta alpha preview` 及版本号（如 `-1.2.3`、`_v2.0`）
- **重名处理** → 目标目录 / 快捷方式已有同名时自动追加 ` (2)`，不会覆盖。
- **跨盘拖动** → 自动改为「复制后删除」，行为等价于移动。

## 文件夹里多个 exe 时

**默认会弹出选择窗口**，列出所有候选 exe 让你自己挑（默认选中启发式推荐的那个）——
不再盲选。启发式优先级（仅作为弹窗的默认项 / `-AutoPick` 的选法）：

1. **名称完全匹配**（去后缀归一后，如 `MyApp.x64.exe` ⇢ `MyApp.exe`）
2. **以前缀匹配**（`MyAppLauncher.exe` vs `MyApp.exe` → 选 `MyApp.exe`）
3. **名称包含**（取名字最短，避免选中 `MyAppHelper.exe`、`MyAppConfig.exe`）
4. **都不匹配** → 取根目录名字最短的（如 `App.exe` 而非 `AppLauncher.exe`）
5. **根目录没有 exe** → 递归子目录找名称包含应用名的，取路径最浅的

始终排除 `unins* / uninstall* / setup* / install* / redist* / helper* / crash* / update* / patch* / repair* / 卸载*`。

批处理/脚本场景可用 `-AutoPick` 跳过弹窗直接取启发式结果。

## 卸载 / 清理

便携软件不用「卸载程序」：删掉 `AppData\Local\Programs\<程序名>` 文件夹，
再到开始菜单根目录删掉对应快捷方式即可。

## 高级参数（拖到图标时可在后面附带，如命令行/快捷方式目标）

| 参数 | 作用 |
|---|---|
| `-Destination <路径>` | 自定义安装目录（默认 `%LOCALAPPDATA%\Programs`） |
| `-StartMenuFolder <路径>` | 自定义快捷方式目录（默认 开始菜单\Programs 根目录） |
| `-Keep` | 解压后**保留**原压缩包，不移入回收站 |
| `-NoShortcut` | 不创建快捷方式 |
| `-AutoPick` | 多个 exe 时不弹窗，直接取启发式推荐 |
| `-Log <文件>` | 把处理日志写入指定文件 |
| `-Gui` | 即使带参数也打开窗口并处理 |

## 重新编译（可选，Windows 自带 csc，无需 SDK）

```
csc /nologo /target:winexe /codepage:65001
    /win32icon:PortableDropper.ico /win32manifest:PortableDropper.manifest
    /r:System.IO.Compression.FileSystem.dll /r:Microsoft.VisualBasic.dll /r:Microsoft.CSharp.dll
    /res:7z.exe,PortableDropper.7zexe /res:7z.dll,PortableDropper.7zdll
    /out:PortableDropper.exe PortableDropper.cs
```

**内置 7-Zip 版权说明**：内置的 `7z.exe`/`7z.dll` 来自 7-Zip（作者 Igor Pavlov，LGPL 许可），
仅用于解压，随本工具整体分发；其许可文本见 7-Zip 官网。

## 小提示

- 软件是「绿色」的，配置一般存在自己文件夹里——整体搬文件夹不会丢配置。
- 建议把 `PortableDropper.exe` 放到好找的地方（比如 `D:\Tools` 或下载目录），桌面可以继续不放绿色软件。
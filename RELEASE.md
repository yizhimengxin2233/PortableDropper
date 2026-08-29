# PortableDropper v1.1.0 — Release Notes

**由 DeepSeek AI 助手（V4 会话）编写并验证**，基于 C# WinForms，单文件 exe，MIT 协议。

## 相比 v1.0.0 新增

- **注册到 Windows「应用和功能」列表**：自动写入当前用户卸载表（HKCU），
  显示名称/版本/发布者/图标/位置/占用大小；「设置 → 应用和功能」里可直接卸载。
- **内置卸载器**：`-Uninstall <名称>` 一键清理注册项 + 开始菜单/桌面快捷方式 + 程序文件夹
  （安全守卫：只允许删除安装目录内的内容；`-KeepFiles` 保留文件）。
- **已注册应用管理窗口**：主窗口「管理已注册应用」→ 浏览/打开目录/卸载；`-List` 命令行清单。
- **支持 `.bat/.cmd/.vbs` 作为主程序**（无 exe 时）。
- **桌面快捷方式**：`-Desktop`（可 `-DesktopFolder` 定向）。
- 版本/发布者/占用大小自动从 exe 元数据与目录计算（零依赖）。
- 新增参数：`-Uninstall` / `-List` / `-NoRegister` / `-KeepFiles` / `-Desktop` / `-DesktopFolder`。

## 既有能力（v1.0.0 起）

- 拖拽文件夹/压缩包即装；内置 7-Zip（.7z/.rar 无需安装）；zip/tar/gz 原生。
- 开始菜单根目录快捷方式，名字自动去版本号与 `x64/windows` 等后缀。
- 多 exe 弹窗选择；Win11 Mica + 深色模式；PerMonitorV2 DPI 清晰；内置图标。

## 使用

1. 从 Assets 下载 `PortableDropper-win-x64.zip`，解压得 `PortableDropper.exe`（单文件）。
2. 拖绿色软件文件夹/压缩包到图标上即完成：装入 Programs + 开始菜单快捷方式 + 应用和功能注册。
3. 卸载：设置 → 应用和功能 → 卸载（或 `-Uninstall`）。

## 构建 / 许可

见 README「重新编译」；本项目 MIT，内嵌 7-Zip 为 LGPL（见 THIRD-PARTY-NOTICES.md）。
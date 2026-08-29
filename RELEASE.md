# PortableDropper v1.0.0 — Release Notes

**由 DeepSeek AI 助手（V4 会话）编写并验证**，基于 C# WinForms，单文件 exe。

## 这是什么

把绿色软件（文件夹 / 压缩包）直接拖到图标上，自动装入 `%LOCALAPPDATA%\Programs`，
在开始菜单根目录生成快捷方式；压缩包支持 zip/7z/rar/tar/gz 等，内置 7-Zip 无需安装。

## 本次发布（v1.0.0）功能

- 🖱 拖拽文件夹 / 压缩包到 exe 图标或窗口即可安装
- 📦 内置 7-Zip 引擎（7z.exe + 7z.dll，LGPL，完整版支持 .7z / .rar），无需单独安装
- 📁 文件夹整体剪切移动；压缩包解压成功后原包自动进回收站
- 📌 开始菜单**根目录**快捷方式（不再建子文件夹），名字自动去掉版本号与
  `x64 / windows / win / portable / stable / beta` 等常见后缀
- 🎯 文件夹内多个 exe 时**弹窗让你自己选**（启发式作为默认项；`-AutoPick` 可跳过弹窗）
- 🖼 WinUI 风格观感：Win11 Mica 毛玻璃 + 跟随系统深色模式；PerMonitorV2 DPI 感知（不模糊）
- 🏷 内置应用图标

## 参数

`-Destination <路径>` `-StartMenuFolder <路径>` `-Keep` `-NoShortcut` `-AutoPick` `-Log <文件>` `-Gui`

详见项目 README.md。

## 使用

1. 从本 Release 下载 `PortableDropper-win-x64.zip`，解压得到 `PortableDropper.exe`（单文件）。
2. 把它放到方便的位置（如 `D:\Tools`），把绿色软件文件夹/压缩包拖到它上面即可。
3. 卸载某个软件 = 删 `AppData\Local\Programs\<程序名>` + 删开始菜单里对应快捷方式。

## 构建

源码 `PortableDropper.cs` 为 C# 5，用 Windows 自带 .NET Framework csc 编译，无需 SDK，
命令见 README「重新编译」一节。
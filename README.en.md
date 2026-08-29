# PortableDropper

> **Author note**: This tool was written and verified by a **DeepSeek AI assistant** (V4 session).
> The source (`PortableDropper.cs`) is C# 5 and can be rebuilt with the .NET Framework compiler
> that ships with Windows — no SDK required.

> **Language**: English | [中文](README.md)

A tiny, dependency-free Windows utility for "green" (portable) software:
**drag a folder or an archive onto the app icon**, and it will:

- move the folder (or extract the archive) into `%LOCALAPPDATA%\Programs`
- create a **Start Menu shortcut** in the root Programs folder with a cleaned-up name
  (strips version numbers and common suffixes such as `x64`, `windows`, `win`, `portable`, `stable`, `beta`…)
- **register the app into Windows "Apps & features"** (per-user, no admin rights),
  with version / publisher / icon / location / size auto-detected from the executable
- support archives: `.zip` (native), `.7z` / `.rar` (**embedded 7-Zip**, nothing to install),
  `.tar` / `.tar.gz` / `.tgz` / `.bz2` / `.xz` (built into Windows 10/11)

Single-file `.exe` with an embedded icon, the 7-Zip engine and a DPI manifest.
Zero dependencies, no installation.

## Files

| File | Purpose |
|---|---|
| `PortableDropper.exe` | Main program (single file, ~2.4 MB — just copy this one around) |
| `PortableDropper.cs` | Source code |
| `PortableDropper.ico` / `.manifest` | Icon / DPI manifest (build inputs) |
| `7z.exe` / `7z.dll` | Embedded 7-Zip engine sources (already embedded in the exe, only needed to rebuild) |
| `LICENSE` | MIT license for this project's code |
| `THIRD-PARTY-NOTICES.md` | Third-party notice (embedded 7-Zip, LGPL) |
| `RELEASE.md` / `publish-gh.ps1` | Release notes / one-command publish script |
| `README.md` / `README.en.md` | This README (中文 / English) |

## UI

- **Bilingual UI (中文 / English)**: follows the system language; switch anytime from the dropdown
  at the bottom-right, or force it with `-Lang zh|en`.
- **Dark/light theme** follows the system (dark title bar on Windows 11).
- **High-DPI crisp**: PerMonitorV2 DPI awareness + GDI text rendering — sharp on 4K / 150% scaling.
- Built-in app icon, and a built-in **"Manage registered apps"** window (browse / open folder / uninstall).

## Usage

1. **Easiest**: drag a folder or archive onto the `PortableDropper.exe` icon. It processes and exits.
2. Double-click to open the window, then drop files into the blue drop area (multiple drops OK).
   Tick **"Also create a desktop shortcut"** in the footer to additionally get a desktop shortcut.
3. Command line (batch / automation):
   ```
   PortableDropper.exe -List                       List registered apps
   PortableDropper.exe -Uninstall "AppName"        Built-in uninstaller
   PortableDropper.exe "D:\Downloads\App.zip" -Desktop   Install with a desktop shortcut
   ```

## Behavior

- **Folder** → moved (cut) into `%LOCALAPPDATA%\Programs\`, original location left empty.
- **Archive** → extracted to `Programs\<name>\`; the original archive goes to the Recycle Bin on success
  (`-Keep` keeps it). `.zip` native; `.7z`/`.rar` via the embedded 7-Zip; tar-family via the system.
- **Other files** (a single `.exe`, etc.) → moved directly into the target folder.
- **Main-program detection**: `.exe` first (excludes `unins*/setup*/helper*` etc.);
  falls back to `.bat/.cmd/.vbs` when there is no exe;
  when several candidates exist a **picker dialog** lets you choose (`-AutoPick` skips it).
- **Shortcut name cleanup**: `Obsidian-1.6.7-win-x64` → **Obsidian** (folder name stays untouched).
- **"Apps & features" registration**: writes a per-user uninstall entry (HKCU);
  its Uninstall button invokes this program's `-Uninstall` for a clean removal.
- **Name collisions**: folders / shortcuts / registry entries get a ` (2)` suffix — never overwritten.
- **Cross-drive drops**: automatically fall back to copy-then-delete, same net result.

## Uninstall

Three equivalent ways (removes the registry entry + Start Menu / desktop shortcuts + the app folder):

- Settings → Apps → Apps & features → find the app → **Uninstall** (invokes this program)
- Main window → **Manage registered apps** → select → **Uninstall selected**
- CLI: `PortableDropper.exe -Uninstall "Name"` (add `-KeepFiles` to keep the files)

## Command-line options

| Option | Effect |
|---|---|
| `-Destination <path>` | Custom install folder (default `%LOCALAPPDATA%\Programs`) |
| `-StartMenuFolder <path>` | Custom shortcut folder (default Start Menu\Programs) |
| `-DesktopFolder <path>` | Custom desktop folder (default the real desktop) |
| `-Uninstall <name>` | Built-in uninstaller |
| `-List` | List registered apps (combine with `-Log <file>` to write to a file) |
| `-Keep` | Keep the original archive after extraction |
| `-KeepFiles` | Keep the app files during uninstall |
| `-NoShortcut` / `-NoRegister` | Skip the shortcut / skip Apps & features registration |
| `-AutoPick` | Pick the main exe automatically when several are found |
| `-Lang <zh\|en>` | UI language (default: follow the system) |
| `-Log <file>` | Write the log to a file |
| `-Gui` | Open the window even when arguments are given |

## License

- **This project's code**: [MIT License](LICENSE) (Copyright (c) 2025 yizhimengxin2233) —
  free to use, modify and use commercially as long as the copyright notice is retained.
- **Embedded 7-Zip** (`7z.exe` / `7z.dll`): **LGPL** (by Igor Pavlov), unmodified, used only for
  extraction, distributed as part of this tool — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
  and [7-Zip's website](https://www.7-zip.org/).

## Rebuild (optional — uses the csc built into Windows, no SDK)

```
csc /nologo /target:winexe /codepage:65001
    /win32icon:PortableDropper.ico /win32manifest:PortableDropper.manifest
    /r:System.IO.Compression.FileSystem.dll /r:Microsoft.VisualBasic.dll /r:Microsoft.CSharp.dll
    /res:7z.exe,PortableDropper.7zexe /res:7z.dll,PortableDropper.7zdll
    /out:PortableDropper.exe PortableDropper.cs
```

## Tips

- Portable apps usually keep their config inside their own folder — moving the whole folder
  doesn't lose any settings.
- Keep `PortableDropper.exe` somewhere handy (e.g. `D:\Tools`) and leave your desktop clean.
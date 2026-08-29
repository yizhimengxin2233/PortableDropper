@echo off
rem PortableDropper one-click build (no VS/SDK needed, only .NET Framework)
rem NOTE: csc.exe is NOT on PATH - full path is required below
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found - .NET Framework 4.x runtime missing
    exit /b 1
)
"%CSC%" /nologo /target:winexe /codepage:65001 /win32icon:PortableDropper.ico /win32manifest:PortableDropper.manifest /r:System.IO.Compression.FileSystem.dll /r:Microsoft.VisualBasic.dll /r:Microsoft.CSharp.dll /res:7z.exe,PortableDropper.7zexe /res:7z.dll,PortableDropper.7zdll /out:PortableDropper.exe PortableDropper.cs
if errorlevel 1 (
    echo [ERROR] build failed
    exit /b 1
)
echo [OK] PortableDropper.exe generated
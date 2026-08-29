# PortableDropper 一键发布脚本（在你自己的终端里运行，需要有外网）
# 前置：安装并登录 GitHub CLI（https://cli.github.com），然后 gh auth login
# 用法:  powershell -ExecutionPolicy Bypass -File publish-gh.ps1
#       或 pwsh -File publish-gh.ps1 -Tag v1.1.0 -Title "PortableDropper v1.1.0"

param(
    [string]$Tag = 'v1.1.0',
    [string]$Title = 'PortableDropper v1.1.0',
    [string]$NotesFile = 'RELEASE.md',
    [string]$Asset = 'dist/PortableDropper-win-x64.zip'
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error '未安装 GitHub CLI：winget install GitHub.cli  然后再 gh auth login'
}
gh auth status 2>$null
if ($LASTEXITCODE -ne 0) { Write-Error '未登录 GitHub：请先运行 gh auth login' }

if (-not (gh repo view PortableDropper 2>$null)) {
    gh repo create PortableDropper --public --source . --push
} else {
    Write-Host '仓库已存在，仅推送更新'
    git add -A
    git commit -m "$Title（由 DeepSeek AI 助手编写）" --allow-empty
    git push -u origin master 2>$null
    if ($LASTEXITCODE -ne 0) { git push -u origin main }
}

gh release create $Tag $Asset --title $Title --notes-file $NotesFile

Write-Host ''
Write-Host "完成！发布页: https://github.com/<你的用户名>/PortableDropper/releases/tag/$Tag"
# PortableDropper 一键发布脚本（在你自己的终端里运行，需要有外网）
# 前置：安装并登录 GitHub CLI（https://cli.github.com），然后:
#   gh auth login
# 然后运行:  powershell -ExecutionPolicy Bypass -File publish-gh.ps1
# 或直接:   pwsh -File publish-gh.ps1

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

# 1. 检查 gh
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error '未安装 GitHub CLI：winget install GitHub.cli  然后再 gh auth login'
}

# 2. 检查登录
gh auth status 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error '未登录 GitHub：请先运行 gh auth login'
}

# 3. 创建仓库（public），推源码
if (-not (gh repo view PortableDropper 2>$null)) {
    gh repo create PortableDropper --public --source . --push
} else {
    Write-Host '仓库已存在，仅推送更新'
    git add -A
    git commit -m 'PortableDropper v1.0.0' --allow-empty
    git push -u origin master 2>$null
    if ($LASTEXITCODE -ne 0) { git push -u origin main }
}

# 4. 创建 Release（含 Windows x64 发布包）
gh release create v1.0.0 "dist/PortableDropper-win-x64.zip" `
    --title "PortableDropper v1.0.0" `
    --notes-file RELEASE.md

Write-Host ''
Write-Host '完成！仓库: https://github.com/<你的用户名>/PortableDropper'
Write-Host '发布页: https://github.com/<你的用户名>/PortableDropper/releases'
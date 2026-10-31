#requires -Version 5.1
<#
.SYNOPSIS
    一键构建 FunctionPlotter（WinUI 3 / Windows App SDK）。

.DESCRIPTION
    需要 Windows 10 1809+ 与 .NET 8 SDK（或 Visual Studio 2022 17.8+）。
    产物是自包含的、非打包（unpackaged）应用：publish 目录里已经带上了 .NET 与
    Windows App SDK 运行时，整个目录拷到任何 Win10/11 机器上双击 exe 即可运行。

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Debug -Run
    .\build.ps1 -Platform ARM64
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',

    # 构建完成后直接启动
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\FunctionPlotter.WinUI\FunctionPlotter.WinUI.csproj'
$rid = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
$outDir = Join-Path $root "publish\$Platform"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '没有找到 dotnet 命令。请先安装 .NET 8 SDK：https://dotnet.microsoft.com/download'
}

# WinUI 3 的 PRI 打包任务随 Visual Studio 分发；找到 VS 后通过 VSINSTALLDIR 告诉 MSBuild（见 Directory.Build.props）
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path $vswhere) {
    $vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Workload.ManagedDesktop -property installationPath
    if ($vsPath) {
        $env:VSINSTALLDIR = "$vsPath\"
        Write-Host "==> 使用 Visual Studio 打包组件：$vsPath" -ForegroundColor DarkGray
    }
}

Write-Host "==> 发布 $Configuration / $Platform ($rid)" -ForegroundColor Cyan
Write-Host "    dotnet publish $project -c $Configuration -p:Platform=$Platform -r $rid" -ForegroundColor DarkGray

dotnet publish $project -c $Configuration -p:Platform=$Platform -r $rid -o $outDir
if ($LASTEXITCODE -ne 0) { throw '构建失败，请查看上面的错误信息。' }

$exe = Join-Path $outDir 'FunctionPlotter.exe'
if (-not (Test-Path $exe)) { throw "构建完成但没有找到 $exe" }

Write-Host ''
Write-Host "可执行文件：$exe" -ForegroundColor Green
Write-Host "整个 publish\$Platform 目录即为绿色版，可直接拷贝运行。" -ForegroundColor DarkGray

if ($Run) {
    Write-Host '==> 启动' -ForegroundColor Cyan
    Start-Process $exe
}

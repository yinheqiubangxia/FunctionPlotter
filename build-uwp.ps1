#requires -Version 5.1
<#
.SYNOPSIS
    一键构建 FunctionPlotter UWP 版。

.DESCRIPTION
    UWP 的 XAML 编译器是 .NET Framework 组件，只能用 Visual Studio 自带的构建引擎，
    不能用 dotnet CLI。本脚本会自动找到本机 Visual Studio 的构建引擎再执行。

    产物是一个未签名的 UWP 应用包布局（AppxManifest.xml + 文件），
    开发机注册后即可运行：
      Add-AppxPackage -Register <AppxManifest.xml 的完整路径>

.EXAMPLE
    .\build-uwp.ps1
    .\build-uwp.ps1 -Configuration Release
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\FunctionPlotter.UWP\FunctionPlotter.UWP.csproj'

# 通过 vswhere 定位 Visual Studio 与它的构建引擎
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw '没有找到 vswhere（说明本机没有安装 Visual Studio）。'
}

$vsPath = & $vswhere -products '*' -requires Microsoft.VisualStudio.ComponentGroup.UWP.Support -property installationPath
if (-not $vsPath) {
    $vsPath = & $vswhere -products '*' -property installationPath
}
if (-not $vsPath) {
    throw '没有找到 Visual Studio 安装。'
}

$engine = Join-Path $vsPath 'MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $engine)) {
    throw "没有找到 Visual Studio 的构建引擎：$engine"
}

Write-Host "==> 构建 UWP $Configuration / $Platform" -ForegroundColor Cyan
Write-Host "    项目：$project"
Write-Host "    VS：  $vsPath"

& $engine $project /restore /p:Configuration=$Configuration /p:Platform=$Platform /v:m
if ($LASTEXITCODE -ne 0) { throw '构建失败，请查看上面的错误信息。' }

$layout = Join-Path $root "src\FunctionPlotter.UWP\bin\$Platform\$Configuration\AppxManifest.xml"
Write-Host ''
if (Test-Path $layout) {
    Write-Host "应用包布局：$layout" -ForegroundColor Green
    Write-Host "注册运行（开发者模式已开启时）：" -ForegroundColor DarkGray
    Write-Host "  Add-AppxPackage -Register `"$layout`"" -ForegroundColor DarkGray
} else {
    Write-Host "构建完成，但在预期位置没有找到 AppxManifest.xml，请在 bin\$Platform\$Configuration 下查找。" -ForegroundColor Yellow
}

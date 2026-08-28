# WinTweaker 构建与发布脚本
# 用法: 以管理员权限运行 PowerShell

param(
    [ValidateSet("Build", "Light", "Full", "Both", "Run")]
    [string]$Mode = "Build"
)

$ErrorActionPreference = "Stop"
$ProjectPath = Join-Path $PSScriptRoot "WinTweaker\WinTweaker.csproj"

function Write-Step($msg) {
    Write-Host "`n[>] $msg" -ForegroundColor Cyan
}

function Write-Ok($msg) {
    Write-Host "[✓] $msg" -ForegroundColor Green
}

function Write-Err($msg) {
    Write-Host "[✗] $msg" -ForegroundColor Red
}

switch ($Mode) {
    "Build" {
        Write-Step "编译项目 (Debug)"
        dotnet build $ProjectPath -c Debug
        if ($LASTEXITCODE -eq 0) { Write-Ok "编译成功" }
        else { Write-Err "编译失败"; exit 1 }
    }

    "Light" {
        Write-Step "发布轻量框架依赖版 (3-8MB)"
        dotnet publish $ProjectPath -c Release `
            /p:SelfContained=false `
            /p:PublishSingleFile=true `
            -o "$PSScriptRoot\publish-light"
        if ($LASTEXITCODE -eq 0) {
            $size = (Get-Item "$PSScriptRoot\publish-light\WinTweaker.exe").Length / 1MB
            Write-Ok "轻量版发布完成：$([math]::Round($size, 2)) MB"
        } else { Write-Err "发布失败"; exit 1 }
    }

    "Full" {
        Write-Step "发布自包含完整版 (55-80MB)"
        dotnet publish $ProjectPath -c Release `
            /p:SelfContained=true `
            /p:PublishSingleFile=true `
            /p:EnableCompressionInSingleFile=true `
            /p:IncludeNativeLibrariesForSelfExtract=true `
            -o "$PSScriptRoot\publish-full"
        if ($LASTEXITCODE -eq 0) {
            $size = (Get-Item "$PSScriptRoot\publish-full\WinTweaker.exe").Length / 1MB
            Write-Ok "完整版发布完成：$([math]::Round($size, 2)) MB"
        } else { Write-Err "发布失败"; exit 1 }
    }

    "Both" {
        Write-Step "发布双模式"
        & $PSScriptRoot\build.ps1 -Mode Light
        & $PSScriptRoot\build.ps1 -Mode Full
        Write-Ok "双版本发布完毕"
    }

    "Run" {
        Write-Step "编译并运行 (需要管理员权限)"
        dotnet build $ProjectPath -c Debug
        if ($LASTEXITCODE -ne 0) { Write-Err "编译失败"; exit 1 }

        $exe = Join-Path $PSScriptRoot "WinTweaker\bin\Debug\net9.0-windows\win-x64\WinTweaker.exe"
        Write-Step "启动 $exe"
        Start-Process -FilePath $exe -Verb RunAs
    }
}

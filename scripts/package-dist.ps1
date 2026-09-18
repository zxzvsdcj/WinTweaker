# WinTweaker customer package script
# Output: dist/light and dist/full (+ zip)
# Usage: powershell -ExecutionPolicy Bypass -File .\scripts\package-dist.ps1

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$proj = Join-Path $root "WinTweaker\WinTweaker.csproj"
$guide = Join-Path $root "WinTweaker\使用教程.md"
$license = Join-Path $root "WinTweaker\license_config.json"
$distRoot = Join-Path $root "dist"
$lightOut = Join-Path $distRoot "轻量版"
$fullOut = Join-Path $distRoot "完整版"

function Write-Step($msg) { Write-Host "`n[>] $msg" -ForegroundColor Cyan }
function Write-Ok($msg) { Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " WinTweaker customer package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (-not (Test-Path $proj)) { throw "Project not found: $proj" }
if (-not (Test-Path $guide)) { throw "Guide not found: $guide" }

Write-Step "Docs sync gate (HelpPage + 使用教程)"
$helpPage = Join-Path $root "WinTweaker\Views\HelpPage.xaml"
if (-not (Test-Path $helpPage)) { throw "HelpPage.xaml missing: $helpPage" }
$helpText = Get-Content $helpPage -Raw -Encoding UTF8
$guideText = Get-Content $guide -Raw -Encoding UTF8
$requiredDocHints = @(
    @{ Name = "HVCI / 内存完整性"; Pattern = "内存完整性" },
    @{ Name = "配置方案"; Pattern = "导出方案|配置方案" },
    @{ Name = "作者微信"; Pattern = "zxzvsdcj" }
)
foreach ($hint in $requiredDocHints) {
    if ($helpText -notmatch $hint.Pattern) {
        throw "HelpPage.xaml missing docs for: $($hint.Name) (pattern: $($hint.Pattern)). Update help before package."
    }
    if ($guideText -notmatch $hint.Pattern) {
        throw "使用教程.md missing docs for: $($hint.Name) (pattern: $($hint.Pattern)). Update guide before package."
    }
}
Write-Ok "Docs gate passed"

Write-Step "Clean dist"
if (Test-Path $distRoot) { Remove-Item -Recurse -Force $distRoot }
New-Item -ItemType Directory -Path $lightOut | Out-Null
New-Item -ItemType Directory -Path $fullOut | Out-Null

Write-Step "Publish light (framework-dependent, needs .NET 9)"
dotnet publish $proj -c Release -p:PublishProfile=Light -o $lightOut --nologo
if ($LASTEXITCODE -ne 0) { throw "Light publish failed" }

Write-Step "Publish full (self-contained)"
dotnet publish $proj -c Release -p:PublishProfile=Full -o $fullOut --nologo
if ($LASTEXITCODE -ne 0) { throw "Full publish failed" }

function Complete-Package($dir, $label) {
    Copy-Item $guide (Join-Path $dir "使用教程.md") -Force
    $readme = @"
Win-Tweaker
Author WeChat: zxzvsdcj

Package: $label

Run: right-click WinTweaker.exe -> Run as administrator
Docs: 使用教程.md  |  in-app Help page
Customization: WeChat zxzvsdcj
"@
    Set-Content -Path (Join-Path $dir "请先阅读.txt") -Value $readme -Encoding UTF8

    if (Test-Path $license) {
        Write-Ok "build input license_config.json present (embedded at compile, not copied)"
    } else {
        Write-Warn "license_config.json missing; published exe will have empty credentials"
    }

    Get-ChildItem $dir -Filter *.pdb -File -ErrorAction SilentlyContinue | Remove-Item -Force
    $leaked = Join-Path $dir "license_config.json"
    if (Test-Path $leaked) { throw "$label must not ship license_config.json" }
    if (Get-ChildItem $dir -Filter *.pdb -File -ErrorAction SilentlyContinue) {
        throw "$label must not ship pdb"
    }

    $exe = Join-Path $dir "WinTweaker.exe"
    if (-not (Test-Path $exe)) { throw "$label missing WinTweaker.exe" }
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
    Write-Ok "$label size: $mb MB -> $dir"
}

Complete-Package $lightOut "Light (needs .NET 9 Desktop Runtime)"
Complete-Package $fullOut "Full (self-contained)"

Write-Step "Zip packages"
$lightZip = Join-Path $distRoot "WinTweaker-轻量版.zip"
$fullZip = Join-Path $distRoot "WinTweaker-完整版.zip"
if (Test-Path $lightZip) { Remove-Item $lightZip -Force }
if (Test-Path $fullZip) { Remove-Item $fullZip -Force }
Compress-Archive -Path (Join-Path $lightOut "*") -DestinationPath $lightZip
Compress-Archive -Path (Join-Path $fullOut "*") -DestinationPath $fullZip
Write-Ok "ZIP: $lightZip"
Write-Ok "ZIP: $fullZip"

Write-Host ""
Write-Host "dist: $distRoot" -ForegroundColor Green
Write-Host "ALL PASS" -ForegroundColor Green
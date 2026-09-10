# WinTweaker dist verification (local only)
# Usage: powershell -ExecutionPolicy Bypass -File .\scripts\verify-dist.ps1

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$proj = Join-Path $root "WinTweaker\WinTweaker.csproj"
$dist = Join-Path $root "dist"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " WinTweaker dist verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n[1/5] Debug build..." -ForegroundColor Yellow
dotnet build $proj -c Debug --nologo
if ($LASTEXITCODE -ne 0) { throw "Debug build failed" }
Write-Host "  OK" -ForegroundColor Green

Write-Host "`n[2/5] Check author and HelpPage..." -ForegroundColor Yellow
$csproj = Get-Content $proj -Raw -Encoding UTF8
if ($csproj -notmatch "zxzvsdcj") { throw "csproj author not updated" }
$help = Join-Path $root "WinTweaker\Views\HelpPage.xaml"
if (-not (Test-Path $help)) { throw "HelpPage.xaml missing" }
$helpText = Get-Content $help -Raw -Encoding UTF8
if ($helpText -notmatch "功能定制") { throw "HelpPage missing customization note" }
if ($helpText -notmatch "zxzvsdcj") { throw "HelpPage missing author wechat" }
if ($helpText -notmatch "内存完整性") { throw "HelpPage missing HVCI docs" }
$guide = Join-Path $root "WinTweaker\使用教程.md"
$guideText = Get-Content $guide -Raw -Encoding UTF8
if ($guideText -notmatch "内存完整性") { throw "使用教程.md missing HVCI docs" }
$main = Get-Content (Join-Path $root "WinTweaker\Views\MainWindow.xaml") -Raw -Encoding UTF8
if ($main -notmatch "HelpPage") { throw "MainWindow missing HelpPage nav" }
Write-Host "  OK" -ForegroundColor Green

Write-Host "`n[3/5] Package dist..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "package-dist.ps1")
if ($LASTEXITCODE -ne 0) { throw "package-dist failed" }

Write-Host "`n[4/5] Validate artifacts..." -ForegroundColor Yellow
$checks = @(
    "轻量版\WinTweaker.exe",
    "轻量版\使用教程.md",
    "轻量版\请先阅读.txt",
    "完整版\WinTweaker.exe",
    "完整版\使用教程.md",
    "完整版\请先阅读.txt",
    "WinTweaker-轻量版.zip",
    "WinTweaker-完整版.zip"
)
foreach ($rel in $checks) {
    $p = Join-Path $dist $rel
    if (-not (Test-Path $p)) { throw "Missing: $rel" }
    Write-Host "  + $rel" -ForegroundColor Green
}

$lightMb = [math]::Round((Get-Item (Join-Path $dist "轻量版\WinTweaker.exe")).Length / 1MB, 2)
$fullMb = [math]::Round((Get-Item (Join-Path $dist "完整版\WinTweaker.exe")).Length / 1MB, 2)
Write-Host "  light exe: ${lightMb} MB" -ForegroundColor White
Write-Host "  full exe:  ${fullMb} MB" -ForegroundColor White
if ($fullMb -le $lightMb) { throw "full package should be larger than light" }

Write-Host "`n[5/5] Launch Debug (admin)..." -ForegroundColor Yellow
$exe = Join-Path $root "WinTweaker\bin\Debug\net9.0-windows\win-x64\WinTweaker.exe"
if (-not (Test-Path $exe)) {
    $alt = Get-ChildItem (Join-Path $root "WinTweaker\bin\Debug") -Recurse -Filter WinTweaker.exe | Select-Object -First 1
    if ($null -eq $alt) { throw "Debug exe not found" }
    $exe = $alt.FullName
}
Write-Host "  Start: $exe"
Start-Process -FilePath $exe -Verb RunAs
Write-Host "  Started. Please open Help page in UI." -ForegroundColor Yellow

Write-Host "`nALL PASS" -ForegroundColor Green
Write-Host "dist: $dist"
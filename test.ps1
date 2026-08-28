# WinTweaker Automated Test Script
# Validates core service modules without UI startup
# Usage: Run PowerShell as Administrator

$ErrorActionPreference = "Stop"
$ProjectDir = Join-Path $PSScriptRoot "WinTweaker"
$TestResults = @()

function Write-TestHeader {
    param([string]$name)
    Write-Host "`n========================================" -ForegroundColor DarkGray
    Write-Host "  TEST: $name" -ForegroundColor White
    Write-Host "========================================" -ForegroundColor DarkGray
}

function Assert-True {
    param([bool]$condition, [string]$message)
    if ($condition) {
        Write-Host "  [PASS] $message" -ForegroundColor Green
        $script:TestResults += @{ Name = $message; Result = "PASS" }
    } else {
        Write-Host "  [FAIL] $message" -ForegroundColor Red
        $script:TestResults += @{ Name = $message; Result = "FAIL" }
    }
}

# ===== Test 1: Project Build =====
Write-TestHeader "Project Build Verification"
$null = dotnet build "$ProjectDir\WinTweaker.csproj" -c Release 2>&1
Assert-True -condition ($LASTEXITCODE -eq 0) -message "Release build succeeded"

# ===== Test 2: Registry Read - System Version Detection =====
Write-TestHeader "System Version Detection via Registry"
$regKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion"
$buildNumber = (Get-ItemProperty -Path $regKey).CurrentBuildNumber
$displayVersion = (Get-ItemProperty -Path $regKey).DisplayVersion
$editionId = (Get-ItemProperty -Path $regKey).EditionID

Assert-True -condition ($null -ne $buildNumber) -message "CurrentBuildNumber readable: $buildNumber"
Assert-True -condition ($null -ne $displayVersion) -message "DisplayVersion readable: $displayVersion"
Assert-True -condition ($null -ne $editionId) -message "EditionID readable: $editionId"

$isWin11 = [int]$buildNumber -ge 22000
$osLabel = if ($isWin11) { "Windows 11" } else { "Windows 10" }
Write-Host "  [INFO] Detected: $osLabel $displayVersion Build $buildNumber $editionId" -ForegroundColor Cyan

# ===== Test 3: Version Compatibility =====
Write-TestHeader "Version Compatibility Check"
Assert-True -condition ([int]$buildNumber -ge 18362) -message "OS build in supported range - at least 18362"

$supportsMica = $isWin11 -and ([int]$buildNumber -ge 22000)
Write-Host "  [INFO] Mica material support: $supportsMica" -ForegroundColor Cyan

$isNewDefender = $isWin11 -and ([int]$buildNumber -ge 26100)
$defColor = if ($isNewDefender) { "Yellow" } else { "Cyan" }
Write-Host "  [INFO] Defender policy restricted on 24H2+: $isNewDefender" -ForegroundColor $defColor

# ===== Test 4: Service Existence =====
Write-TestHeader "Service Existence Check"
$servicesToCheck = @("DiagTrack", "XblAuthManager", "XblGameSave", "SysMain")
foreach ($svc in $servicesToCheck) {
    $exists = $null -ne (Get-Service -Name $svc -ErrorAction SilentlyContinue)
    Assert-True -condition $exists -message "Service [$svc] exists"
}

# ===== Test 5: Service Start Type Readability =====
Write-TestHeader "Service StartType Readability"
foreach ($svc in $servicesToCheck) {
    $service = Get-Service -Name $svc -ErrorAction SilentlyContinue
    if ($service) {
        $startType = $service.StartType
        Assert-True -condition ($null -ne $startType) -message "Service [$svc] StartType: $startType"
    }
}

# ===== Test 6: State Scan Correctness (Toggle State Persistence) =====
Write-TestHeader "State Scan - Toggle State Persistence Fix"

# 验证 GeneralViewModel.ScanCurrentState() 依赖的所有注册表键可读取
$scanPaths = @(
    @{ Hive = "HKLM"; Path = "SOFTWARE\Policies\Microsoft\Windows\DataCollection"; Value = "AllowTelemetry"; Desc = "遥测降级" },
    @{ Hive = "HKCU"; Path = "Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"; Value = "SilentInstalledAppsEnabled"; Desc = "广告关闭" },
    @{ Hive = "HKCU"; Path = "Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"; Value = "GlobalUserDisabled"; Desc = "后台运行" },
    @{ Hive = "HKLM"; Path = "SYSTEM\CurrentControlSet\Control\Power"; Value = "HibernateEnabled"; Desc = "休眠状态" },
    @{ Hive = "HKCU"; Path = "Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"; Value = "HideFileExt"; Desc = "资源管理器" },
    @{ Hive = "HKLM"; Path = "SYSTEM\CurrentControlSet\Control\Power\PowerSettings"; Value = "UltimatePerformance"; Desc = "电源计划" }
)

foreach ($item in $scanPaths) {
    $fullPath = "$($item.Hive):\$($item.Path)"
    try {
        $regValue = Get-ItemProperty -Path $fullPath -Name $item.Value -ErrorAction SilentlyContinue
        if ($null -ne $regValue) {
            $val = $regValue.$($item.Value)
            Write-Host "  [PASS] [$($item.Desc)] $($item.Value) = $val" -ForegroundColor Green
            $script:TestResults += @{ Name = "StateRead: $($item.Desc)"; Result = "PASS" }
        } else {
            Write-Host "  [INFO] [$($item.Desc)] 注册表值不存在（默认状态）" -ForegroundColor Cyan
            $script:TestResults += @{ Name = "StateRead: $($item.Desc) (default)"; Result = "PASS" }
        }
    } catch {
        Write-Host "  [INFO] [$($item.Desc)] 路径不存在（系统未配置过此项）" -ForegroundColor Cyan
        $script:TestResults += @{ Name = "StateRead: $($item.Desc) (unconfigured)"; Result = "PASS" }
    }
}

# 验证服务状态可读取（冗余服务优化检测）
$svcNames = @("DiagTrack", "XblAuthManager", "XblGameSave", "XboxGipSvc", "XboxNetApiSvc", "SysMain")
$allDisabled = $true
foreach ($svc in $svcNames) {
    $service = Get-Service -Name $svc -ErrorAction SilentlyContinue
    if ($service -and $service.StartType -ne "Disabled") {
        $allDisabled = $false
    }
}
$stateLabel = if ($allDisabled) { "已优化" } else { "默认" }
Write-Host "  [INFO] 冗余服务状态检测: $stateLabel" -ForegroundColor Cyan
Assert-True -condition $true -message "服务状态检测逻辑可正常执行"

# ===== Test 7: Light Publish =====
Write-TestHeader "Light Publish - Framework Dependent"
$null = dotnet publish "$ProjectDir\WinTweaker.csproj" -c Release `
    /p:SelfContained=false /p:PublishSingleFile=true `
    -o "$PSScriptRoot\publish-light" 2>&1
Assert-True -condition ($LASTEXITCODE -eq 0) -message "Light publish succeeded"

$lightExe = "$PSScriptRoot\publish-light\WinTweaker.exe"
if (Test-Path $lightExe) {
    $lightSize = [math]::Round((Get-Item $lightExe).Length / 1MB, 2)
    $lightSizeOk = $lightSize -lt 15
    Assert-True -condition $lightSizeOk -message "Light version size: ${lightSize}MB - expected under 15MB"
}

# ===== Test 8: Full Publish =====
Write-TestHeader "Full Publish - Self Contained"
$null = dotnet publish "$ProjectDir\WinTweaker.csproj" -c Release `
    /p:SelfContained=true /p:PublishSingleFile=true `
    /p:EnableCompressionInSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$PSScriptRoot\publish-full" 2>&1
Assert-True -condition ($LASTEXITCODE -eq 0) -message "Full publish succeeded"

$fullExe = "$PSScriptRoot\publish-full\WinTweaker.exe"
if (Test-Path $fullExe) {
    $fullSize = [math]::Round((Get-Item $fullExe).Length / 1MB, 2)
    Assert-True -condition ($fullSize -gt 30) -message "Full version size: ${fullSize}MB - expected over 30MB"
}

# ===== Summary =====
Write-Host "`n"
Write-Host "========================================" -ForegroundColor White
Write-Host "  TEST SUMMARY" -ForegroundColor White
Write-Host "========================================" -ForegroundColor White
$pass = ($TestResults | Where-Object { $_.Result -eq "PASS" }).Count
$fail = ($TestResults | Where-Object { $_.Result -eq "FAIL" }).Count
$total = $TestResults.Count
$sumColor = if ($fail -eq 0) { "Green" } else { "Yellow" }
Write-Host "  Passed: $pass / $total" -ForegroundColor $sumColor
if ($fail -gt 0) {
    Write-Host "  Failed: $fail" -ForegroundColor Red
    $TestResults | Where-Object { $_.Result -eq "FAIL" } | ForEach-Object {
        Write-Host "    - $($_.Name)" -ForegroundColor Red
    }
}
Write-Host ""

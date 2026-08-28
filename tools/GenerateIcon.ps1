# 生成 WinTweaker 图标 v6
# 改进：
# 1. window.ico 使用 BMP 格式（非 PNG），兼容性最佳
# 2. 同时生成 taskbar.png (48x48) 作为备选
# 3. app.ico 使用 PNG 格式（Explorer 兼容好）
# 4. 满填充、无透明、高饱和蓝色

Add-Type -AssemblyName System.Drawing

$appIcoPath = Join-Path $PSScriptRoot "WinTweaker\Assets\app.ico"
$winIcoPath = Join-Path $PSScriptRoot "WinTweaker\Assets\window.ico"

function Render-Icon {
    param([int]$size)
    
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 完全不透明蓝色背景 — 用高饱和亮蓝色，暗色任务栏上更醒目
    $bgColor = [System.Drawing.Color]::FromArgb(255, 0, 120, 215)
    $g.Clear($bgColor)

    # 白色 "W" 粗体字母，占 70% 高度
    $white = [System.Drawing.Color]::White
    $whiteBrush = New-Object System.Drawing.SolidBrush($white)
    
    $fontSize = $size * 0.70
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    
    $textRect = New-Object System.Drawing.RectangleF(0, ($size * 0.01), $size, $size)
    $g.DrawString("W", $font, $whiteBrush, $textRect, $sf)

    $font.Dispose()
    $sf.Dispose()
    $whiteBrush.Dispose()
    $g.Dispose()
    
    return $bmp
}

function Write-IcoFile-BMP {
    param([string]$path, [System.Drawing.Bitmap[]]$images)
    
    # 使用 BMP 格式（非 PNG）写入 .ico — 最大兼容性
    $ms = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($ms)

    $writer.Write([int16]0)
    $writer.Write([int16]1)
    $writer.Write([int16]$images.Length)

    $dataOffset = 6 + $images.Length * 16
    $imageDataList = @()

    foreach ($img in $images) {
        $w = $img.Width
        $h = $img.Height
        
        # 构建 DIB (Device Independent Bitmap) 数据
        # BITMAPINFOHEADER (40 bytes) + pixel data (BGRA) + AND mask
        $dibMs = New-Object System.IO.MemoryStream
        $dibWriter = New-Object System.IO.BinaryWriter($dibMs)
        
        # BITMAPINFOHEADER
        $dibWriter.Write([int32]40)           # biSize
        $dibWriter.Write([int32]$w)           # biWidth
        $dibWriter.Write([int32]($h * 2))     # biHeight (double for AND mask)
        $dibWriter.Write([int16]1)            # biPlanes
        $dibWriter.Write([int16]32)           # biBitCount
        $dibWriter.Write([int32]0)            # biCompression (BI_RGB)
        $dibWriter.Write([int32]0)            # biSizeImage
        $dibWriter.Write([int32]0)            # biXPelsPerMeter
        $dibWriter.Write([int32]0)            # biYPelsPerMeter
        $dibWriter.Write([int32]0)            # biClrUsed
        $dibWriter.Write([int32]0)            # biClrImportant
        
        # XOR mask (BGRA pixels, bottom-up)
        for ($y = $h - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $w; $x++) {
                $pixel = $img.GetPixel($x, $y)
                $dibWriter.Write([byte]$pixel.B)
                $dibWriter.Write([byte]$pixel.G)
                $dibWriter.Write([byte]$pixel.R)
                $dibWriter.Write([byte]$pixel.A)
            }
        }
        
        # AND mask (all zeros = fully opaque)
        $andRowBytes = [Math]::Ceiling($w / 8.0)
        $andRowPadded = [Math]::Ceiling($andRowBytes / 4.0) * 4
        $andMask = New-Object byte[] ($andRowPadded * $h)
        $dibWriter.Write($andMask)
        
        $imageDataList += ,$dibMs.ToArray()
        $dibWriter.Dispose()
        $dibMs.Dispose()
    }

    # Directory entries
    for ($i = 0; $i -lt $images.Length; $i++) {
        $img = $images[$i]
        $data = $imageDataList[$i]
        $w = if ($img.Width -ge 256) { 0 } else { $img.Width }
        $h = if ($img.Height -ge 256) { 0 } else { $img.Height }
        $writer.Write([byte]$w)
        $writer.Write([byte]$h)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([int16]1)
        $writer.Write([int16]32)
        $writer.Write([int32]$data.Length)
        $writer.Write([int32]$dataOffset)
        $dataOffset += $data.Length
    }

    foreach ($data in $imageDataList) { $writer.Write($data) }

    $dir = Split-Path $path -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
    
    $writer.Dispose()
    $ms.Dispose()
}

function Write-IcoFile-PNG {
    param([string]$path, [System.Drawing.Bitmap[]]$images)
    
    $ms = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($ms)

    $writer.Write([int16]0)
    $writer.Write([int16]1)
    $writer.Write([int16]$images.Length)

    $dataOffset = 6 + $images.Length * 16
    $imageDataList = @()

    foreach ($img in $images) {
        $pngMs = New-Object System.IO.MemoryStream
        $img.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
        $imageDataList += ,$pngMs.ToArray()
        $pngMs.Dispose()
    }

    for ($i = 0; $i -lt $images.Length; $i++) {
        $img = $images[$i]
        $data = $imageDataList[$i]
        $w = if ($img.Width -ge 256) { 0 } else { $img.Width }
        $h = if ($img.Height -ge 256) { 0 } else { $img.Height }
        $writer.Write([byte]$w)
        $writer.Write([byte]$h)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([int16]1)
        $writer.Write([int16]32)
        $writer.Write([int32]$data.Length)
        $writer.Write([int32]$dataOffset)
        $dataOffset += $data.Length
    }

    foreach ($data in $imageDataList) { $writer.Write($data) }

    $dir = Split-Path $path -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
    
    $writer.Dispose()
    $ms.Dispose()
}

Write-Host "=== Generating icons v6 (BMP format for taskbar) ===" -ForegroundColor Cyan

# 1. window.ico — BMP 格式，任务栏最佳兼容
Write-Host "`n[window.ico] Taskbar (BMP format, full-bleed)" -ForegroundColor Yellow
$winSizes = @(16, 20, 24, 32, 40, 48)
$winBitmaps = @()
foreach ($s in $winSizes) {
    $winBitmaps += ,(Render-Icon -size $s)
    Write-Host "  ${s}x${s}" -ForegroundColor Gray
}
Write-IcoFile-BMP -path $winIcoPath -images $winBitmaps
foreach ($bmp in $winBitmaps) { $bmp.Dispose() }
Write-Host "  -> $winIcoPath ($([math]::Round((Get-Item $winIcoPath).Length / 1KB, 1)) KB)" -ForegroundColor Green

# 2. app.ico — PNG 格式（Explorer 大图标用）
Write-Host "`n[app.ico] Explorer (PNG format, all sizes)" -ForegroundColor Yellow
$appSizes = @(16, 24, 32, 48, 64, 128, 256)
$appBitmaps = @()
foreach ($s in $appSizes) {
    $appBitmaps += ,(Render-Icon -size $s)
    Write-Host "  ${s}x${s}" -ForegroundColor Gray
}
Write-IcoFile-PNG -path $appIcoPath -images $appBitmaps
foreach ($bmp in $appBitmaps) { $bmp.Dispose() }
Write-Host "  -> $appIcoPath ($([math]::Round((Get-Item $appIcoPath).Length / 1KB, 1)) KB)" -ForegroundColor Green

Write-Host "`nDone!" -ForegroundColor Green

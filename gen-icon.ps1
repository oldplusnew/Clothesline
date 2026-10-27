Add-Type -AssemblyName System.Drawing
$sizes = @(16,32,48,256)
$imgs = @()
foreach ($s in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap $s, $s
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)
  $k = $s / 32.0
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([float](2.6*$k))
  $g.DrawLine($pen, [float](1*$k), [float](10*$k), [float](31*$k), [float](10*$k))
  $rect = New-Object System.Drawing.Rectangle ([int](11*$k)), ([int](7*$k)), ([int](10*$k)), ([int](18*$k))
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(232,150,84)), ([System.Drawing.Color]::FromArgb(176,102,48)), ([float]90)
  $g.FillRectangle($brush, $rect)
  $g.FillEllipse([System.Drawing.Brushes]::White, [float](9.5*$k), [float](3*$k), [float](13*$k), [float](8*$k))
  $g.Dispose()
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $imgs += ,$ms.ToArray()
}
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16*$sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $png = $imgs[$i]
  $bw.Write([Byte]($s -band 0xFF)); $bw.Write([Byte]($s -band 0xFF)); $bw.Write([Byte]0); $bw.Write([Byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32)
  $bw.Write([UInt32]$png.Length); $bw.Write([UInt32]$offset)
  $offset += $png.Length
}
foreach ($png in $imgs) { $bw.Write($png) }
$bw.Flush()
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
[System.IO.File]::WriteAllBytes((Join-Path $dir 'Clothesline.ico'), $out.ToArray())
Write-Host "Clothesline.ico generated"

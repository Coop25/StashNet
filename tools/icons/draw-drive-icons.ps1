# Draws the five StashNet drive icons (1K-256K) (160x160 transparent PNGs) in a worn 7 Days to Die style: the front
# art is drawn flat, then mapped onto the front of a box seen from the front-right and slightly above, with
# a shaded top and right side. Regenerate the mod icons with:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/icons/draw-drive-icons.ps1 -OutDir Mod/UIAtlases/ItemIconAtlas
# The file names must match the item names in Mod/Config/items.xml.
param([string]$OutDir = '.')
Add-Type -AssemblyName System.Drawing
$S = 2  # supersample factor

function C([int]$a,[int]$r,[int]$g,[int]$b) { [System.Drawing.Color]::FromArgb($a,$r,$g,$b) }
function Brush($c) { New-Object System.Drawing.SolidBrush $c }
function Pen($c, [float]$w) { $p = New-Object System.Drawing.Pen $c, ($w*$S); $p.LineJoin = 'Round'; $p }
function RR([float]$x,[float]$y,[float]$w,[float]$h,[float]$r) {
  $x*=$S; $y*=$S; $w*=$S; $h*=$S; $r*=$S; $d=$r*2
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddArc($x,$y,$d,$d,180,90); $p.AddArc($x+$w-$d,$y,$d,$d,270,90); $p.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90); $p.AddArc($x,$y+$h-$d,$d,$d,90,90); $p.CloseFigure(); $p }
function Ell($g, $brush, [float]$x,[float]$y,[float]$w,[float]$h) { $g.FillEllipse($brush, $x*$S, $y*$S, $w*$S, $h*$S) }
function Poly($g, $brush, $pts) { $arr = @(); foreach ($p in $pts) { $arr += New-Object System.Drawing.PointF ($p[0]*$S), ($p[1]*$S) }; $g.FillPolygon($brush, [System.Drawing.PointF[]]$arr) }
function Blotch($g, [float]$cx,[float]$cy,[float]$rx,[float]$ry, $color) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $p.AddEllipse(($cx-$rx)*$S, ($cy-$ry)*$S, 2*$rx*$S, 2*$ry*$S)
  $b = New-Object System.Drawing.Drawing2D.PathGradientBrush $p; $b.CenterColor = $color; $b.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $color.R, $color.G, $color.B))
  $g.FillPath($b, $p) }
function Speckle($g, $clip, [int]$count, [int]$seed, $dark, $light) {
  $rnd = New-Object System.Random $seed; $old = $g.Clip; $g.SetClip($clip)
  for ($i=0; $i -lt $count; $i++) {
    $x = $rnd.Next(20,140); $y = $rnd.Next(8,152); $s = 0.6 + $rnd.NextDouble()*1.6
    $c = if ($rnd.NextDouble() -lt 0.7) { $dark } else { $light }
    $g.FillEllipse((Brush ([System.Drawing.Color]::FromArgb([int]($c.A * (0.4 + $rnd.NextDouble()*0.6)), $c.R, $c.G, $c.B))), $x*$S, $y*$S, $s*$S, $s*$S) }
  for ($i=0; $i -lt 14; $i++) {
    $x = $rnd.Next(26,130); $y = $rnd.Next(14,146); $len = 4 + $rnd.Next(10); $ang = $rnd.NextDouble()*3.14
    $g.DrawLine((Pen (C 70 210 205 190) 0.6), $x*$S, $y*$S, ($x + [math]::Cos($ang)*$len)*$S, ($y + [math]::Sin($ang)*$len)*$S) }
  $g.Clip = $old }
function Rivet($g, [float]$x, [float]$y, [float]$r) {
  Ell $g (Brush (C 255 40 36 32)) ($x-$r) ($y-$r) (2*$r) (2*$r)
  Ell $g (Brush (C 255 120 112 100)) ($x-$r*0.75) ($y-$r*0.8) (1.5*$r) (1.5*$r)
  Ell $g (Brush (C 200 190 180 160)) ($x-$r*0.45) ($y-$r*0.55) (0.6*$r) (0.6*$r) }

# Face art in canvas coordinates (face = 22,10 .. 128,146), cropped to a 106x136 bitmap (at 2x).
function FaceArt([string]$tierName, [string]$label, $tape, [int]$seed) {
  $bmp = New-Object System.Drawing.Bitmap (160*$S), (160*$S); $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.Clear([System.Drawing.Color]::Transparent)
  $face = RR 22 10 106 136 3
  $rect = New-Object System.Drawing.RectangleF (22*$S), (10*$S), (106*$S), (136*$S)
  $g.FillPath((New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (C 255 116 116 102), (C 255 64 65 58), 75), $face)
  $old = $g.Clip; $g.SetClip($face)
  Blotch $g 75 146 76 26 (C 160 52 42 30)
  $rnd = New-Object System.Random ($seed + 7)
  for ($i=0; $i -lt 5; $i++) { Blotch $g ($rnd.Next(30,120)) ($rnd.Next(18,140)) (6 + $rnd.Next(10)) (5 + $rnd.Next(8)) (C 150 128 62 26) }
  Blotch $g 26 142 16 12 (C 180 118 54 22); Blotch $g 126 14 14 10 (C 170 118 54 22)
  $g.Clip = $old
  Speckle $g $face 260 $seed (C 120 30 28 24) (C 90 190 182 160)
  $g.DrawPath((Pen (C 255 30 30 27) 2), $face)
  $g.DrawPath((Pen (C 80 205 198 176) 1.1), (RR 25 13 100 130 2))

  Ell $g (Brush (C 255 26 26 24)) 35 20 82 82
  $pp = New-Object System.Drawing.Drawing2D.GraphicsPath; $pp.AddEllipse(38*$S, 23*$S, 76*$S, 76*$S)
  $pb = New-Object System.Drawing.Drawing2D.PathGradientBrush $pp; $pb.CenterColor = (C 255 200 198 186); $pb.SurroundColors = @((C 255 104 104 96)); $pb.CenterPoint = New-Object System.Drawing.PointF (64*$S), (46*$S)
  $g.FillPath($pb, $pp)
  foreach ($r in 30,23,16) { $g.DrawEllipse((Pen (C 55 50 48 40) 0.7), (76-$r)*$S, (61-$r)*$S, 2*$r*$S, 2*$r*$S) }
  $rnd2 = New-Object System.Random ($seed + 3); $old = $g.Clip; $g.SetClip($pp)
  for ($i=0; $i -lt 10; $i++) { $rr = 8 + $rnd2.Next(28); $g.DrawArc((Pen (C 80 240 236 220) 0.5), (76-$rr)*$S, (61-$rr)*$S, 2*$rr*$S, 2*$rr*$S, $rnd2.Next(0,360), 20 + $rnd2.Next(50)) }
  Blotch $g 92 82 16 12 (C 90 70 60 40); $g.Clip = $old
  Ell $g (Brush (C 255 52 50 46)) 68 53 16 16; Ell $g (Brush (C 255 150 146 134)) 72 57 6 6

  $arm = Pen (C 255 168 162 146) 6.5; $arm.StartCap = 'Round'; $arm.EndCap = 'Round'; $g.DrawLine($arm, 116*$S, 96*$S, 91*$S, 70*$S)
  $g.DrawLine((Pen (C 255 78 74 66) 1.6), 116*$S, 96*$S, 91*$S, 70*$S)
  Ell $g (Brush (C 255 34 32 30)) 107 87 18 18; Ell $g (Brush (C 255 128 122 108)) 112 92 8 8
  foreach ($pt in @(@(31,19),@(119,19),@(31,137),@(119,137))) { Rivet $g $pt[0] $pt[1] 3.6 }

  if ($tierName -eq 'basic') {
    $g.TranslateTransform(40*$S, 26*$S); $g.RotateTransform(-38)
    $dt = RR -26 -8 52 16 2; $g.FillPath((Brush (C 235 150 150 146)), $dt)
    for ($i=-24; $i -lt 26; $i+=3) { $g.DrawLine((Pen (C 40 90 90 86) 0.5), $i*$S, -8*$S, $i*$S, 8*$S) }
    $g.DrawPath((Pen (C 120 80 80 76) 0.8), $dt); $g.ResetTransform()
    $g.DrawLine((Pen (C 200 24 22 20) 1), 104*$S, 120*$S, 112*$S, 132*$S)
  } elseif ($tierName -eq 'standard') {
    foreach ($c in @(@(22,10,1,1),@(128,10,-1,1),@(22,146,1,-1),@(128,146,-1,-1))) {
      $x=$c[0]; $y=$c[1]; $sx=$c[2]; $sy=$c[3]
      Poly $g (Brush (C 255 74 70 64)) @(@($x,$y),@(($x+$sx*24),$y),@(($x+$sx*24),($y+$sy*8)),@(($x+$sx*8),($y+$sy*8)),@(($x+$sx*8),($y+$sy*24)),@($x,($y+$sy*24)))
      $g.DrawLine((Pen (C 120 190 180 160) 0.8), $x*$S, $y*$S, ($x+$sx*24)*$S, $y*$S)
      Rivet $g ($x+$sx*4) ($y+$sy*16) 2.4; Rivet $g ($x+$sx*16) ($y+$sy*4) 2.4 }
  } else {
    # 16K and up: hazard band on top + steel corner brackets. 64K adds side straps; 256K adds a gold rim and gold brackets.
    $hz = RR 45 11 60 8 1.5; $g.FillPath((Brush (C 255 200 160 40)), $hz)
    $old = $g.Clip; $g.SetClip($hz)
    for ($i=0; $i -lt 14; $i++) { Poly $g (Brush (C 255 30 28 26)) @(@((40+$i*7),19),@((45+$i*7),11),@((48.5+$i*7),11),@((43.5+$i*7),19)) }
    $g.Clip = $old; $g.DrawPath((Pen (C 255 30 28 26) 1.2), $hz)
    if ($tierName -eq '64k' -or $tierName -eq '256k') {
      foreach ($sx in 23, 121) {
        $strap = RR $sx 24 6 96 1; $g.FillPath((Brush (C 255 96 100 102)), $strap); $g.DrawPath((Pen (C 255 40 40 38) 0.8), $strap)
        $g.DrawLine((Pen (C 110 210 206 190) 0.6), ($sx+1)*$S, 25*$S, ($sx+1)*$S, 119*$S)
        foreach ($ry in 34, 58, 82, 106) { Rivet $g ($sx+3) $ry 2.2 } }
    }
    $bracket = if ($tierName -eq '256k') { C 255 196 158 62 } else { C 255 120 124 126 }
    if ($tierName -eq '256k') { $g.DrawPath((Pen (C 255 176 140 52) 2.2), (RR 23.5 11.5 103 133 2.5)); $g.DrawPath((Pen (C 120 250 220 140) 0.7), (RR 25.5 13.5 99 129 2)) }
    foreach ($c in @(@(22,10,1,1),@(128,10,-1,1),@(22,146,1,-1),@(128,146,-1,-1))) {
      $x=$c[0]; $y=$c[1]; $sx=$c[2]; $sy=$c[3]
      Poly $g (Brush $bracket) @(@($x,$y),@(($x+$sx*20),$y),@(($x+$sx*20),($y+$sy*6)),@(($x+$sx*6),($y+$sy*6)),@(($x+$sx*6),($y+$sy*20)),@($x,($y+$sy*20)))
      Rivet $g ($x+$sx*3.5) ($y+$sy*3.5) 2.6 }
  }

  # Tier tape: horizontal across the lower front, runs off the right edge (it wraps onto the side).
  $g.FillRectangle((Brush $tape), 22*$S, 122*$S, 106*$S, 12*$S)
  $g.FillRectangle((Brush (C 60 255 255 255)), 22*$S, 122*$S, 106*$S, 2.5*$S)
  $g.DrawLine((Pen (C 140 20 20 20) 0.8), 22*$S, 122*$S, 128*$S, 122*$S); $g.DrawLine((Pen (C 140 20 20 20) 0.8), 22*$S, 134*$S, 128*$S, 134*$S)

  $g.TranslateTransform(70*$S, 116*$S); $g.RotateTransform(-5)
  Poly $g (Brush (C 240 214 198 156)) @(@(-24,-9),@(-20,-10),@(-14,-9),@(20,-10),@(25,-8),@(23,-4),@(26,0),@(24,5),@(25,9),@(-22,10),@(-25,7),@(-23,3),@(-26,-1),@(-23,-5))
  $fs = if ($label.Length -ge 4) { 14 } else { 17 }
  $f = New-Object System.Drawing.Font 'Ink Free', ($fs*$S), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
  $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
  $g.DrawString($label, $f, (Brush (C 255 24 22 22)), (New-Object System.Drawing.RectangleF (-26*$S), (-11*$S), (52*$S), (22*$S)), $sf)
  $g.ResetTransform()

  Ell $g (Brush ([System.Drawing.Color]::FromArgb(110, $tape.R, $tape.G, $tape.B))) 112 33 11 11
  Ell $g (Brush $tape) 114.5 35.5 6 6; Ell $g (Brush (C 200 255 255 240)) 115.5 36.2 2 2

  $crop = New-Object System.Drawing.Bitmap (106*$S), (136*$S); $gc = [System.Drawing.Graphics]::FromImage($crop)
  $gc.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, (106*$S), (136*$S)), (New-Object System.Drawing.Rectangle (22*$S), (10*$S), (106*$S), (136*$S)), 'Pixel')
  $gc.Dispose(); $g.Dispose(); $bmp.Dispose()
  return $crop
}

# Box geometry (face-art pixels): width 106, height 136, thickness 28. Camera front-right, slightly above.
$W = 106; $H = 136; $T = 36
$th = 38 * [math]::PI / 180; $ph = 22 * [math]::PI / 180
$ct = [math]::Cos($th); $st = [math]::Sin($th); $cp = [math]::Cos($ph); $sp = [math]::Sin($ph)
function P0([double]$X, [double]$Y, [double]$Z) { $x = $X*$ct + $Z*$st; $depth = -$X*$st + $Z*$ct; $y = -($Y*$cp + $depth*$sp); return ,@($x, $y) }
$corners = @(); foreach ($X in 0,$W) { foreach ($Y in 0,$H) { foreach ($Z in 0,$T) { $corners += ,(P0 $X $Y $Z) } } }
$minX = ($corners | ForEach-Object { $_[0] } | Measure-Object -Minimum).Minimum; $maxX = ($corners | ForEach-Object { $_[0] } | Measure-Object -Maximum).Maximum
$minY = ($corners | ForEach-Object { $_[1] } | Measure-Object -Minimum).Minimum; $maxY = ($corners | ForEach-Object { $_[1] } | Measure-Object -Maximum).Maximum
$scale = 146 / [math]::Max($maxX - $minX, $maxY - $minY)
$offX = (160 - ($maxX - $minX) * $scale) / 2 - $minX * $scale
$offY = (160 - ($maxY - $minY) * $scale) / 2 - $minY * $scale
function P([double]$X, [double]$Y, [double]$Z) { $p = P0 $X $Y $Z; New-Object System.Drawing.PointF ((($p[0]*$scale) + $offX) * $S), ((($p[1]*$scale) + $offY) * $S) }
function FaceMatrix([double]$w, [double]$h, $ul, $ur, $ll) { New-Object System.Drawing.Drawing2D.Matrix (New-Object System.Drawing.RectangleF 0, 0, $w, $h), ([System.Drawing.PointF[]]@($ul, $ur, $ll)) }

function Drive3D([string]$file, [string]$tierName, [string]$label, $tape, [int]$seed) {
  $face = FaceArt $tierName $label $tape $seed
  $bmp = New-Object System.Drawing.Bitmap (160*$S), (160*$S); $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([System.Drawing.Color]::Transparent)
  $rnd = New-Object System.Random ($seed + 50)

  # Right side (X = W): darker, lit from the front-left. Local rect: T wide (front->back), H tall (top->bottom).
  $g.Transform = FaceMatrix $T $H (P $W $H 0) (P $W $H $T) (P $W 0 0)
  $g.FillRectangle((New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF 0, 0, $T, $H), (C 255 70 70 62), (C 255 38 38 34), 90), 0, 0, $T, $H)
  for ($i=0; $i -lt 120; $i++) { $g.FillEllipse((Brush (C (40 + $rnd.Next(60)) 24 22 20)), [float]($rnd.NextDouble()*$T), [float]($rnd.NextDouble()*$H), 1.2, 1.2) }
  Blotch2 $g ($T*0.5) ($H-8) ($T*0.7) 14 (C 140 110 52 24)
  foreach ($yy in 22, 68, 112) { $g.FillEllipse((Brush (C 255 22 22 20)), [float]($T/2-3), [float]($yy-3), 6, 6); $g.FillEllipse((Brush (C 120 150 140 120)), [float]($T/2-2), [float]($yy-2.4), 2.5, 2.5) }
  $dark = [System.Drawing.Color]::FromArgb(255, [int]($tape.R*0.62), [int]($tape.G*0.62), [int]($tape.B*0.62))
  $g.FillRectangle((Brush $dark), 0, 112, $T, 12); $g.FillRectangle((Brush (C 40 255 255 255)), 0, 112, $T, 2)
  $g.DrawLine((New-Object System.Drawing.Pen (C 140 20 20 20), 0.8), 0, 112, $T, 112); $g.DrawLine((New-Object System.Drawing.Pen (C 140 20 20 20), 0.8), 0, 124, $T, 124)

  # Top (Y = H): lightest. Local rect: W wide (left->right), T deep (back->front).
  $g.Transform = FaceMatrix $W $T (P 0 $H $T) (P $W $H $T) (P 0 $H 0)
  $g.FillRectangle((New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF 0, 0, $W, $T), (C 255 132 130 114), (C 255 104 102 90), 0), 0, 0, $W, $T)
  for ($i=0; $i -lt 90; $i++) { $g.FillEllipse((Brush (C (40 + $rnd.Next(60)) 30 28 24)), [float]($rnd.NextDouble()*$W), [float]($rnd.NextDouble()*$T), 1.4, 1.4) }
  Blotch2 $g ($W*0.85) ($T*0.5) 12 ($T*0.6) (C 130 118 56 24)
  $g.DrawLine((New-Object System.Drawing.Pen (C 150 225 220 200), 1.6), 0, $T-0.8, $W, $T-0.8)

  # Front: the worn art, then a bright bevel along its top and right edges.
  $g.ResetTransform()
  $g.DrawImage($face, [System.Drawing.PointF[]]@((P 0 $H 0), (P $W $H 0), (P 0 0 0)))
  $bevel = New-Object System.Drawing.Pen (C 140 230 224 205), (1.2*$S)
  $g.DrawLine($bevel, (P 0 $H 0), (P $W $H 0)); $g.DrawLine($bevel, (P $W $H 0), (P $W 0 0))

  # Silhouette outline
  $sil = [System.Drawing.PointF[]]@((P 0 0 0), (P 0 $H 0), (P 0 $H $T), (P $W $H $T), (P $W 0 $T), (P $W 0 0))
  $g.DrawPolygon((New-Object System.Drawing.Pen (C 230 20 20 18), (1.6*$S)), $sil)

  $out = New-Object System.Drawing.Bitmap 160, 160; $g2 = [System.Drawing.Graphics]::FromImage($out)
  $g2.InterpolationMode = 'HighQualityBicubic'; $g2.SmoothingMode = 'AntiAlias'; $g2.PixelOffsetMode = 'HighQuality'
  $g2.Clear([System.Drawing.Color]::Transparent); $g2.DrawImage($bmp, 0, 0, 160, 160)
  $out.Save((Join-Path $OutDir "$file.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $g2.Dispose(); $out.Dispose(); $g.Dispose(); $bmp.Dispose(); $face.Dispose()
}

# Blotch in local (already transformed) units, no supersample scaling.
function Blotch2($g, [float]$cx,[float]$cy,[float]$rx,[float]$ry, $color) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $p.AddEllipse($cx-$rx, $cy-$ry, 2*$rx, 2*$ry)
  $b = New-Object System.Drawing.Drawing2D.PathGradientBrush $p; $b.CenterColor = $color; $b.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $color.R, $color.G, $color.B))
  $g.FillPath($b, $p) }

Drive3D 'stashnetDriveBasic'       'basic'    '48'   (C 255 70 112 168) 11
Drive3D 'stashnetDriveStandard'    'standard' '192'  (C 255 84 138 70)  23
Drive3D 'stashnetDriveHighDensity' 'hd'       '576'  (C 255 128 84 150) 37
Drive3D 'stashnetDrive64K'         '64k'      '1728' (C 255 196 112 40) 53
Drive3D 'stashnetDrive256K'        '256k'     '5184' (C 255 168 40 40)  71

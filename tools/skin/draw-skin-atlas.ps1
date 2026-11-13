# Draws the StashNet "wrecked but working military salvage" decal atlas: one 1024x2048 transparent PNG plus
# atlas.xml (sprite name -> pixel rect, origin top-left). The mod sticks these decals onto the vanilla models
# at runtime (Mod/Skins/layouts.xml says where). Deterministic: fixed seeds, fixed order, so re-running
# reproduces both files byte for byte. Regenerate with:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/skin/draw-skin-atlas.ps1 -OutDir Mod/Skins
# Style rule (owner): when in doubt, dirtier and more worn. Mockups: docs/superpowers/specs/2026-11-13-wrecked-salvage-look-mockups/
param([string]$OutDir = '.')
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$S = 2          # supersample factor (each sprite is drawn at 2x, then scaled into the atlas)
$AtlasW = 1024; $AtlasH = 2048; $Pad = 4

function C([int]$a,[int]$r,[int]$g,[int]$b) { [System.Drawing.Color]::FromArgb($a,$r,$g,$b) }
function Hex([int]$a, [string]$hex) { C $a ([Convert]::ToInt32($hex.Substring(0,2),16)) ([Convert]::ToInt32($hex.Substring(2,2),16)) ([Convert]::ToInt32($hex.Substring(4,2),16)) }
function Brush($c) { New-Object System.Drawing.SolidBrush $c }
function Pen($c, [float]$w) { $p = New-Object System.Drawing.Pen $c, $w; $p.LineJoin = 'Round'; $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p }
function PtF([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }
function Poly($g, $brush, $pts) { $arr = @(); foreach ($p in $pts) { $arr += PtF $p[0] $p[1] }; $g.FillPolygon($brush, [System.Drawing.PointF[]]$arr) }
function Blotch($g, [float]$cx,[float]$cy,[float]$rx,[float]$ry, $color) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $p.AddEllipse($cx-$rx, $cy-$ry, 2*$rx, 2*$ry)
  $b = New-Object System.Drawing.Drawing2D.PathGradientBrush $p; $b.CenterColor = $color; $b.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $color.R, $color.G, $color.B))
  $g.FillPath($b, $p); $b.Dispose(); $p.Dispose() }
# Punch soft transparent holes (wear) into what's already drawn.
function Wipe($g, [float]$x, [float]$y, [float]$w, [float]$h) {
  $old = $g.CompositingMode; $g.CompositingMode = 'SourceCopy'
  $g.FillEllipse((Brush ([System.Drawing.Color]::Transparent)), $x, $y, $w, $h); $g.CompositingMode = $old }
function WipeRect($g, [float]$x, [float]$y, [float]$w, [float]$h) {
  $old = $g.CompositingMode; $g.CompositingMode = 'SourceCopy'
  $g.FillRectangle((Brush ([System.Drawing.Color]::Transparent)), $x, $y, $w, $h); $g.CompositingMode = $old }
function Grime($g, [float]$w, [float]$h, [int]$count, [int]$seed, [int]$alpha) {
  $rnd = New-Object System.Random $seed
  for ($i=0; $i -lt $count; $i++) {
    $s = 0.6 + $rnd.NextDouble()*2.2
    $g.FillEllipse((Brush (C ([int]($alpha*(0.3+$rnd.NextDouble()*0.7))) 28 24 18)), [float]($rnd.NextDouble()*$w), [float]($rnd.NextDouble()*$h), [float]$s, [float]$s) } }
function Text($g, [string]$s, [string]$font, [float]$size, $style, $color, [float]$x, [float]$y, [float]$w, [float]$h, [string]$align = 'Center') {
  $f = New-Object System.Drawing.Font $font, $size, $style, ([System.Drawing.GraphicsUnit]::Pixel)
  $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = $align; $sf.LineAlignment = 'Center'
  $g.DrawString($s, $f, (Brush $color), (New-Object System.Drawing.RectangleF $x, $y, $w, $h), $sf); $f.Dispose() }

# ---------- Sprite drawers (all in sprite pixel units; the 2x supersample is applied by the caller) ----------

function Draw-Stencil($g, [float]$w, [float]$h, [string]$text, [int]$seed, [float]$size) {
  Text $g $text 'Courier New' $size ([System.Drawing.FontStyle]::Bold) (C 215 216 210 184) 0 0 $w $h
  # Stencil bridges: thin vertical gaps through the letters.
  $rnd = New-Object System.Random $seed
  for ($x = 6; $x -lt $w; $x += $size*0.62) { WipeRect $g ($x + $rnd.Next(0,3)) ($h*0.48) 1.6 ($h*0.08) }
  # Worn away: big chunks gone (owner: "mostly worn off"), plus pitting.
  for ($i=0; $i -lt 4; $i++) { Wipe $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h*0.6)) ([float](10+$rnd.Next(26))) ([float](8+$rnd.Next(16))) }
  for ($i=0; $i -lt 90; $i++) { Wipe $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h)) 2.2 2.2 }
  Grime $g $w $h 120 ($seed+1) 120 }

function Draw-BareMetal($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $pts = @(); $n = 11
  for ($i=0; $i -lt $n; $i++) {
    $a = $i / $n * 2 * [math]::PI; $r = 0.62 + $rnd.NextDouble()*0.38
    $pts += ,@(($w/2 + [math]::Cos($a)*$w/2*$r*0.95), ($h/2 + [math]::Sin($a)*$h/2*$r*0.95)) }
  Poly $g (Brush (C 235 118 120 114)) $pts
  # Paint-chip rim: a slightly lighter edge where the paint flaked.
  $arr = @(); foreach ($p in $pts) { $arr += PtF $p[0] $p[1] }
  $g.DrawPolygon((Pen (C 150 160 158 150) 2), [System.Drawing.PointF[]]$arr)
  for ($i=0; $i -lt 26; $i++) {
    $x = $rnd.NextDouble()*$w; $y = $rnd.NextDouble()*$h; $len = 6 + $rnd.Next(22); $ang = -0.3 + $rnd.NextDouble()*0.6
    $g.DrawLine((Pen (C 110 200 200 192) 0.8), [float]$x, [float]$y, [float]($x + [math]::Cos($ang)*$len), [float]($y + [math]::Sin($ang)*$len)) }
  for ($i=0; $i -lt 4; $i++) { Blotch $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h)) ([float](8+$rnd.Next(14))) ([float](6+$rnd.Next(10))) (C 170 122 66 30) }
  Grime $g $w $h 70 ($seed+2) 150 }

function Draw-RustStreak($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  for ($k=0; $k -lt 4; $k++) {
    $x = $w*0.25 + $rnd.NextDouble()*$w*0.5; $top = $rnd.NextDouble()*$h*0.15; $len = $h*(0.55 + $rnd.NextDouble()*0.45)
    $wd = 4 + $rnd.NextDouble()*($w*0.35)
    $rect = New-Object System.Drawing.RectangleF ([float]($x-$wd/2)), ([float]$top), ([float]$wd), ([float]$len)
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (C 150 104 58 26), (C 0 80 44 20), 90
    $g.FillEllipse($lg, $rect); $lg.Dispose() }
  Blotch $g ($w/2) ($h*0.06) ($w*0.5) ($h*0.07) (C 170 90 46 18)
  Grime $g $w $h 60 ($seed+3) 120 }

function Draw-RustBase($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $rect = New-Object System.Drawing.RectangleF 0, 0, $w, $h
  $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (C 0 110 58 22), (C 235 84 42 16), 90
  $g.FillRectangle($lg, $rect); $lg.Dispose()
  for ($i=0; $i -lt 18; $i++) { Blotch $g ([float]($rnd.NextDouble()*$w)) ([float]($h*0.4+$rnd.NextDouble()*$h*0.6)) ([float](6+$rnd.Next(18))) ([float](4+$rnd.Next(8))) (C 210 128 64 22) }
  # Rusted through: dark holes along the bottom edge.
  for ($i=0; $i -lt 7; $i++) { Blotch $g ([float]($rnd.NextDouble()*$w)) ([float]($h*0.85)) ([float](4+$rnd.Next(9))) ([float](3+$rnd.Next(5))) (C 240 18 12 8) }
  Grime $g $w $h 160 ($seed+4) 160 }

function Draw-BulletHole($g, [float]$w, [float]$h, [int]$seed, [bool]$through) {
  $rnd = New-Object System.Random $seed
  $cx = $w/2; $cy = $h/2
  Blotch $g $cx $cy ($w*0.48) ($h*0.48) (C 150 30 28 24)               # soot ring
  $petals = @()
  for ($i=0; $i -lt 9; $i++) { $a = $i/9*2*[math]::PI + $rnd.NextDouble()*0.3; $r = $w*(0.17 + $rnd.NextDouble()*0.09); $petals += ,@(($cx+[math]::Cos($a)*$r), ($cy+[math]::Sin($a)*$r)) }
  Poly $g (Brush (C 255 168 168 160)) $petals                            # torn bright metal lip
  if ($through) {
    Blotch $g $cx $cy ($w*0.15) ($h*0.15) (C 255 6 6 6); $g.FillEllipse((Brush (C 255 4 4 4)), $cx-$w*0.09, $cy-$h*0.09, $w*0.18, $h*0.18)
  } else {
    $g.FillEllipse((Brush (C 255 96 98 92)), $cx-$w*0.12, $cy-$h*0.12, $w*0.24, $h*0.24)
    $g.FillEllipse((Brush (C 255 200 200 192)), $cx-$w*0.06, $cy-$h*0.08, $w*0.08, $h*0.07) }
  for ($i=0; $i -lt 5; $i++) { $a = $rnd.NextDouble()*2*[math]::PI; $g.DrawLine((Pen (C 160 22 20 18) 0.9), [float]$cx, [float]$cy, [float]($cx+[math]::Cos($a)*$w*0.44), [float]($cy+[math]::Sin($a)*$h*0.44)) } }

function Draw-Dent($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  Blotch $g ($w*0.55) ($h*0.6) ($w*0.48) ($h*0.42) (C 190 20 22 16)      # shadowed lower half
  Blotch $g ($w*0.42) ($h*0.38) ($w*0.36) ($h*0.28) (C 150 176 178 166)  # highlight on the upper rim
  for ($i=0; $i -lt 8; $i++) { $x = $w*0.2 + $rnd.NextDouble()*$w*0.6; $y = $h*0.25 + $rnd.NextDouble()*$h*0.5; $g.DrawLine((Pen (C 140 200 200 190) 0.8), [float]$x, [float]$y, [float]($x+6+$rnd.Next(10)), [float]($y+$rnd.Next(-3,4))) }
  Grime $g $w $h 40 ($seed+5) 110 }

function Draw-Claw($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  for ($k=0; $k -lt 4; $k++) {
    $x0 = $w*0.12 + $k*$w*0.2 + $rnd.NextDouble()*6; $y0 = $h*0.04 + $rnd.NextDouble()*$h*0.08
    $x1 = $x0 + $w*0.12 + $rnd.NextDouble()*8;      $y1 = $h*(0.82 + $rnd.NextDouble()*0.14); if ($k -eq 3) { $y1 -= $h*0.15 }
    $mx = ($x0+$x1)/2; $my = ($y0+$y1)/2; $nx = ($y1-$y0); $ny = -($x1-$x0); $nl = [math]::Sqrt($nx*$nx+$ny*$ny); $nx/=$nl; $ny/=$nl
    $wd = 5 + $rnd.NextDouble()*3
    Poly $g (Brush (C 255 14 15 11)) @(@($x0,$y0), @(($mx+$nx*$wd),($my+$ny*$wd)), @($x1,$y1), @(($mx-$nx*$wd*0.6),($my-$ny*$wd*0.6)))
    $g.DrawLine((Pen (C 230 196 198 188) 1.4), [float]($x0+$nx*1.5), [float]$y0, [float]($mx+$nx*$wd+1), [float]($my+$ny*$wd))
    $g.DrawLine((Pen (C 230 196 198 188) 1.4), [float]($mx+$nx*$wd+1), [float]($my+$ny*$wd), [float]$x1, [float]$y1)
    Blotch $g ([float]$x1) ([float]$y1) 6 10 (C 160 110 52 20) }           # rust where the gouge ends
  Grime $g $w $h 50 ($seed+6) 120 }

function Draw-Pry($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  for ($y = 8; $y -lt $h - 8; $y += 14 + $rnd.Next(10)) {
    $x = $w*0.6 + $rnd.Next(-4,5)
    $g.DrawLines((Pen (C 255 30 30 26) 3.2), [System.Drawing.PointF[]]@((PtF $x $y), (PtF ($x-$w*0.35) ($y+5)), (PtF $x ($y+10))))
    $g.DrawLines((Pen (C 230 200 200 192) 1.1), [System.Drawing.PointF[]]@((PtF ($x+1) $y), (PtF ($x-$w*0.33) ($y+5)), (PtF ($x+1) ($y+10)))) }
  for ($i=0; $i -lt 3; $i++) { Blotch $g ($w*0.6) ([float]($rnd.NextDouble()*$h)) 8 14 (C 150 112 54 20) } }

function Draw-Scorch($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  Blotch $g ($w/2) ($h/2) ($w*0.48) ($h*0.46) (C 220 10 9 8)
  for ($i=0; $i -lt 9; $i++) { Blotch $g ([float]($w*0.2+$rnd.NextDouble()*$w*0.6)) ([float]($h*0.2+$rnd.NextDouble()*$h*0.6)) ([float]($w*(0.1+$rnd.NextDouble()*0.18))) ([float]($h*(0.1+$rnd.NextDouble()*0.18))) (C 200 6 6 5) }
  for ($i=0; $i -lt 6; $i++) { Blotch $g ([float]($w*0.15+$rnd.NextDouble()*$w*0.7)) ([float]($h*0.15+$rnd.NextDouble()*$h*0.7)) 8 6 (C 120 120 70 30) }
  # Streaks of soot upward (heat rises).
  for ($i=0; $i -lt 5; $i++) { $x = $w*0.3 + $rnd.NextDouble()*$w*0.4; Blotch $g ([float]$x) ($h*0.2) 6 ($h*0.2) (C 140 12 11 10) } }

function Draw-Stripe($g, [float]$w, [float]$h, [string]$hex, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $g.FillRectangle((Brush (Hex 235 $hex)), 0, 2, $w, $h-4)
  $g.FillRectangle((Brush (C 40 255 255 255)), 0, 2, $w, 2)
  # Chipped: chunks missing, scratches through it, grime on top.
  for ($i=0; $i -lt 7; $i++) { WipeRect $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.Next(0,[int]($h/2)))) ([float](8+$rnd.Next(26))) ([float]($h*0.4+$rnd.NextDouble()*$h*0.7)) }
  for ($i=0; $i -lt 30; $i++) { Wipe $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h)) 2.5 2.5 }
  for ($i=0; $i -lt 10; $i++) { $x = $rnd.NextDouble()*$w; $g.DrawLine((Pen (C 160 150 150 140) 0.8), [float]$x, 2, [float]($x+$rnd.Next(-12,12)), [float]($h-2)) }
  Grime $g $w $h 110 ($seed+7) 160 }

function Draw-Readout($g, [float]$w, [float]$h, [string]$text, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $g.FillRectangle((Brush (C 255 38 41 31)), 0, 0, $w, $h)                       # armored housing
  $g.DrawRectangle((Pen (C 255 12 13 10) 2), 1, 1, $w-2, $h-2)
  foreach ($p in @(@(6,6),@(($w-6),6),@(6,($h-6)),@(($w-6),($h-6)))) { $g.FillEllipse((Brush (C 255 20 20 16)), [float]($p[0]-3), [float]($p[1]-3), 6, 6); $g.FillEllipse((Brush (C 255 110 108 96)), [float]($p[0]-1.6), [float]($p[1]-2), 2.6, 2.6) }
  $sx = 12; $sy = 9; $sw = $w-24; $sh = $h-18
  $g.FillRectangle((Brush (C 255 4 20 10)), $sx, $sy, $sw, $sh)
  Blotch $g ($sx+$sw/2) ($sy+$sh/2) ($sw*0.55) ($sh*0.7) (C 60 63 220 102)        # phosphor glow
  Text $g $text 'Consolas' ($sh*0.56) ([System.Drawing.FontStyle]::Bold) (C 255 63 220 102) $sx $sy $sw $sh
  for ($y = $sy; $y -lt $sy+$sh; $y += 3) { $g.DrawLine((Pen (C 70 0 0 0) 1), [float]$sx, [float]$y, [float]($sx+$sw), [float]$y) }   # scan lines
  # Crack from one corner.
  $cx = $sx + $sw*(0.55 + $rnd.NextDouble()*0.3); $cy = $sy + 2
  $pts = @((PtF $cx $cy)); for ($i=0; $i -lt 5; $i++) { $cx += $rnd.Next(-9,6); $cy += $sh/5; $pts += PtF $cx $cy }
  $g.DrawLines((Pen (C 200 210 240 225) 1), [System.Drawing.PointF[]]$pts)
  $g.DrawLine((Pen (C 160 210 240 225) 0.8), $pts[2], (PtF ($pts[2].X+14) ($pts[2].Y-4)))
  # A strip of duct tape across the top-left corner.
  $g.TranslateTransform(14, 8); $g.RotateTransform(-24)
  $g.FillRectangle((Brush (C 245 150 150 142)), -16, -5, 40, 11)
  for ($x = -16; $x -lt 24; $x += 3) { $g.DrawLine((Pen (C 40 80 80 76) 0.5), [float]$x, -5, [float]$x, 6) }
  $g.ResetTransform()
  Grime $g $w $h 90 ($seed+8) 150 }

function Draw-DuctTape($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $pts = @(@(0,3),@(6,1),@(4,($h/2)),@(7,($h-2)),@(0,($h-4)))
  $poly = @(@(4,2),@(($w-6),1),@(($w-2),($h*0.3)),@(($w-5),($h*0.6)),@(($w-1),($h-2)),@(5,($h-1)),@(1,($h*0.6)),@(5,($h*0.35)))
  Poly $g (Brush (C 245 152 152 144)) $poly
  for ($x = 4; $x -lt $w-4; $x += 3) { $g.DrawLine((Pen (C 45 80 80 76) 0.5), [float]$x, 2, [float]$x, [float]($h-2)) }
  $g.DrawLine((Pen (C 60 255 255 255) 1), 6, 4, [float]($w-8), 3)
  Grime $g $w $h 60 ($seed+9) 170 }

function Draw-Conduit($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $gapTop = $h*0.55; $gapBot = $h*0.62                                              # broken section
  foreach ($seg in @(@(0, $gapTop), @($gapBot, $h))) {
    $rect = New-Object System.Drawing.RectangleF 2, ([float]$seg[0]), ([float]($w-4)), ([float]($seg[1]-$seg[0]))
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (C 255 30 30 28), (C 255 74 74 70), 0
    $g.FillRectangle($lg, $rect); $lg.Dispose()
    for ($y = $seg[0] + 10; $y -lt $seg[1]; $y += 40) { $g.FillRectangle((Brush (C 255 50 50 46)), 0, [float]$y, $w, 5) } }   # clamps
  Poly $g (Brush (C 255 44 44 40)) @(@(2,$gapTop),@(($w-2),$gapTop),@(($w-4),($gapTop+5)),@(($w*0.5),($gapTop+2)),@(4,($gapTop+6)))
  for ($i=0; $i -lt 4; $i++) { Blotch $g ($w/2) ([float]($rnd.NextDouble()*$h)) ($w*0.6) 12 (C 140 110 54 20) }
  Grime $g $w $h 70 ($seed+10) 140 }

function Draw-Cables($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $cols = @((C 255 150 34 30), (C 255 22 22 22), (C 255 180 150 40), (C 255 40 70 120), (C 255 22 22 22))
  for ($k=0; $k -lt $cols.Count; $k++) {
    $x0 = $w*0.3 + $k*$w*0.08; $pts = @((PtF $x0 0))
    $x = $x0; for ($y = $h*0.2; $y -le $h; $y += $h*0.2) { $x += $rnd.Next(-22, 26); $pts += PtF $x $y }
    $g.DrawCurve((Pen (C 255 10 10 10) 6.5), [System.Drawing.PointF[]]$pts)
    $g.DrawCurve((Pen $cols[$k] 4.5), [System.Drawing.PointF[]]$pts)
    $g.DrawCurve((Pen (C 60 255 255 255) 1), [System.Drawing.PointF[]]$pts) }
  Grime $g $w $h 50 ($seed+11) 120 }

function Draw-Spark($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $pts = [System.Drawing.PointF[]]@((PtF 0 ($h*0.2)), (PtF ($w*0.25) ($h*0.3)), (PtF ($w*0.45) ($h*0.55)), (PtF ($w*0.55) ($h*0.62)))
  $g.DrawCurve((Pen (C 255 12 10 10) 5.5), $pts); $g.DrawCurve((Pen (C 255 168 40 34) 3.6), $pts)
  $tx = $w*0.6; $ty = $h*0.64
  $g.DrawLine((Pen (C 255 200 120 60) 1.4), [float]($w*0.55), [float]($h*0.62), [float]$tx, [float]$ty)   # bare copper end
  Blotch $g $tx $ty ($w*0.3) ($h*0.32) (C 140 255 200 90)
  Blotch $g $tx $ty ($w*0.12) ($h*0.14) (C 255 255 244 200)
  for ($i=0; $i -lt 9; $i++) { $a = $rnd.NextDouble()*2*[math]::PI; $r = $w*(0.1+$rnd.NextDouble()*0.28)
    $g.DrawLine((Pen (C 255 255 214 120) 1.3), [float]$tx, [float]$ty, [float]($tx+[math]::Cos($a)*$r), [float]($ty+[math]::Sin($a)*$r)) } }

function Draw-Crt($g, [float]$w, [float]$h, [int]$seed) {
  $g.FillRectangle((Brush (C 255 4 20 10)), 0, 0, $w, $h)
  Blotch $g ($w/2) ($h/2) ($w*0.62) ($h*0.62) (C 70 63 220 102)
  $lines = @('STASHNET v1.0', '> CORE ......... OK', '> DRIVES ....... OK', '> LINK ........ OK', '> _')
  $fs = $h*0.088; $y = $h*0.12
  foreach ($l in $lines) {
    $size = if ($l -eq 'STASHNET v1.0') { $fs*1.25 } else { $fs }
    Text $g $l 'Consolas' $size ([System.Drawing.FontStyle]::Bold) (C 255 63 220 102) ($w*0.09) $y ($w*0.86) ($size*1.4) 'Near'
    $y += $size*1.55 }
  for ($sy = 0; $sy -lt $h; $sy += 3) { $g.DrawLine((Pen (C 80 0 0 0) 1.1), 0, [float]$sy, $w, [float]$sy) }   # scan lines
  # Tube curve: darkened rounded corners and edges.
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $p.AddRectangle((New-Object System.Drawing.RectangleF (-$w*0.1), (-$h*0.1), ($w*1.2), ($h*1.2)))
  $p2 = New-Object System.Drawing.Drawing2D.GraphicsPath; $r = $h*0.22
  $p2.AddArc(0,0,2*$r,2*$r,180,90); $p2.AddArc($w-2*$r,0,2*$r,2*$r,270,90); $p2.AddArc($w-2*$r,$h-2*$r,2*$r,2*$r,0,90); $p2.AddArc(0,$h-2*$r,2*$r,2*$r,90,90); $p2.CloseFigure()
  $region = New-Object System.Drawing.Region $p; $region.Exclude($p2); $g.FillRegion((Brush (C 255 8 8 7)), $region)
  $pb = New-Object System.Drawing.Drawing2D.PathGradientBrush $p2; $pb.CenterColor = (C 0 0 0 0); $pb.SurroundColors = @((C 170 0 0 0)); $pb.FocusScales = New-Object System.Drawing.PointF 0.72, 0.62
  $g.FillPath($pb, $p2) }

function Draw-Label($g, [float]$w, [float]$h, [string]$text, [int]$seed) {
  Text $g $text 'Courier New' ($h*0.62) ([System.Drawing.FontStyle]::Bold) (C 200 216 210 184) 0 0 $w $h
  $rnd = New-Object System.Random $seed
  for ($i=0; $i -lt 3; $i++) { Wipe $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h*0.5)) ([float](8+$rnd.Next(16))) ([float](6+$rnd.Next(10))) }
  for ($i=0; $i -lt 50; $i++) { Wipe $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h)) 2 2 }
  Grime $g $w $h 60 ($seed+12) 120 }

function Draw-Glyph($g, [float]$w, [float]$h, [string]$ch) {
  Text $g $ch 'Arial' ($h*0.95) ([System.Drawing.FontStyle]::Bold) (C 255 255 255 255) 0 0 $w $h }

function Draw-PaintPatch($g, [float]$w, [float]$h, [int]$seed) {
  # Rough brushed-on dark army green over old branding: opaque, ragged edges, faint brush strokes.
  $rnd = New-Object System.Random $seed
  $pts = @(); $n = 18
  for ($i=0; $i -lt $n; $i++) { $a = $i / $n * 2 * [math]::PI; $r = 0.88 + $rnd.NextDouble()*0.12
    $pts += ,@(($w/2 + [math]::Cos($a)*$w/2*$r), ($h/2 + [math]::Sin($a)*$h/2*$r)) }
  Poly $g (Brush (C 255 44 48 38)) $pts
  for ($i=0; $i -lt 26; $i++) { $y = $rnd.NextDouble()*$h; $x = $rnd.NextDouble()*$w*0.3
    $v = $rnd.Next(-6,7)
    $g.DrawLine((Pen (C 70 ([int](44+$v)) ([int](49+$v)) ([int](38+$v))) (2+$rnd.Next(4))), [float]$x, [float]$y, [float]($x+$w*0.4+$rnd.NextDouble()*$w*0.3), [float]($y+$rnd.Next(-3,4))) }
  Grime $g $w $h 90 ($seed+1) 90 }
function Draw-LedPanel($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $g.FillRectangle((Brush (C 255 22 24 20)), 0, 0, $w, $h)
  $g.DrawRectangle((Pen (C 255 70 72 64) 2), 1, 1, $w-2, $h-2)
  foreach ($p in @(@(5,5),@(($w-5),5),@(5,($h-5)),@(($w-5),($h-5)))) { $g.FillEllipse((Brush (C 255 90 90 82)), [float]($p[0]-2), [float]($p[1]-2), 4, 4) }
  Text $g 'DRIVE ARRAY' 'Consolas' ($h*0.16) ([System.Drawing.FontStyle]::Bold) (C 200 200 196 170) 4 2 ($w-8) ($h*0.24)
  $cols = 10; $rows = 3
  for ($r=0; $r -lt $rows; $r++) { for ($c=0; $c -lt $cols; $c++) {
    $x = 10 + $c*($w-20)/($cols-1); $y = $h*0.42 + $r*$h*0.18
    $roll = $rnd.NextDouble(); $col = if ($roll -lt 0.62) { C 255 70 230 100 } elseif ($roll -lt 0.8) { C 255 240 170 40 } elseif ($roll -lt 0.9) { C 255 230 50 40 } else { C 255 40 44 40 }
    if ($col.R -ne 40) { Blotch $g ([float]$x) ([float]$y) 6 5 ([System.Drawing.Color]::FromArgb(90, $col.R, $col.G, $col.B)) }
    $g.FillEllipse((Brush $col), [float]($x-2.5), [float]($y-2.5), 5, 5) } }
  Grime $g $w $h 60 ($seed+2) 130 }

function Draw-Vent($g, [float]$w, [float]$h, [int]$seed) {
  $g.FillRectangle((Brush (C 255 46 50 40)), 0, 0, $w, $h)
  $g.DrawRectangle((Pen (C 255 20 22 18) 3), 1.5, 1.5, $w-3, $h-3)
  for ($y = 8; $y -lt $h - 6; $y += 9) {
    $g.FillRectangle((Brush (C 255 10 11 9)), 8, [float]$y, $w-16, 5)
    $g.DrawLine((Pen (C 160 120 124 110) 1.2), 8, [float]($y+5.5), [float]($w-8), [float]($y+5.5)) }
  foreach ($p in @(@(4,4),@(($w-4),4),@(4,($h-4)),@(($w-4),($h-4)))) { $g.FillEllipse((Brush (C 255 110 110 100)), [float]($p[0]-2), [float]($p[1]-2), 4, 4) }
  Blotch $g ($w*0.7) ($h*0.8) ($w*0.25) ($h*0.3) (C 150 100 52 22)
  Grime $g $w $h 80 ($seed+3) 150 }

function Draw-Ports($g, [float]$w, [float]$h, [int]$seed) {
  # A dirty connector strip with thin, dull, sagging cables (not toy-colored).
  $rnd = New-Object System.Random $seed
  $g.FillRectangle((Brush (C 255 30 32 27)), 0, $h*0.12, $w, $h*0.42)
  $g.DrawRectangle((Pen (C 255 12 13 10) 2), 1, [float]($h*0.12), $w-2, [float]($h*0.42))
  $g.DrawLine((Pen (C 90 120 120 110) 1), 2, [float]($h*0.14), $w-2, [float]($h*0.14))
  $cols = @((C 255 92 34 28), (C 255 26 26 24), (C 255 120 100 44), (C 255 38 52 70), (C 255 32 32 30), (C 255 58 76 48), (C 255 26 26 24))
  for ($i=0; $i -lt 7; $i++) {
    $x = 11 + $i*($w-22)/6; $y = $h*0.34
    $g.FillRectangle((Brush (C 255 8 8 8)), [float]($x-5), [float]($y-4.5), 10, 9)
    $g.FillRectangle((Brush (C 255 112 110 100)), [float]($x-3.5), [float]($y-3), 7, 6)
    $g.FillRectangle((Brush (C 255 70 68 62)), [float]($x-2), [float]($y+3), 4, 4)
    $sag = $rnd.Next(-6, 7)
    $pts = [System.Drawing.PointF[]]@((PtF $x ($y+5)), (PtF ($x + $sag*0.5) ($h*0.66)), (PtF ($x + $sag) ($h*0.84)), (PtF ($x + $sag + $rnd.Next(-3,4)) $h))
    $g.DrawCurve((Pen (C 255 10 10 9) 3.6), $pts); $g.DrawCurve((Pen $cols[$i] 2.4), $pts)
    $g.DrawCurve((Pen (C 40 220 220 210) 0.6), $pts) }
  Blotch $g ($w*0.3) ($h*0.3) ($w*0.25) ($h*0.25) (C 90 90 48 20)
  Grime $g $w $h 120 ($seed+4) 170 }
function Draw-Warning($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $g.FillRectangle((Brush (C 255 220 180 30)), 0, 0, $w, $h)
  $g.FillRectangle((Brush (C 255 20 20 20)), 3, [float]($h*0.36), $w-6, [float]($h*0.6))
  for ($x = -$h; $x -lt $w; $x += 12) { Poly $g (Brush (C 255 20 20 20)) @(@($x,($h*0.32)),@(($x+6),($h*0.32)),@(($x+6+$h*0.3),0),@(($x+$h*0.3),0)) }
  Text $g 'DATA STORAGE' 'Arial' ($h*0.24) ([System.Drawing.FontStyle]::Bold) (C 255 220 180 30) 3 ([float]($h*0.38)) ($w-6) ([float]($h*0.3))
  Text $g 'HANDLE WITH CARE' 'Arial' ($h*0.16) ([System.Drawing.FontStyle]::Bold) (C 255 220 180 30) 3 ([float]($h*0.66)) ($w-6) ([float]($h*0.26))
  for ($i=0; $i -lt 3; $i++) { Wipe $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h)) ([float](6+$rnd.Next(10))) ([float](5+$rnd.Next(8))) }
  Grime $g $w $h 70 ($seed+5) 160 }

function Draw-LedStrip($g, [float]$w, [float]$h, [int]$seed) {
  # Status lights only, no text (for mirrored faces, where words would read backwards).
  $rnd = New-Object System.Random $seed
  $g.FillRectangle((Brush (C 255 22 24 20)), 0, 0, $w, $h)
  $g.DrawRectangle((Pen (C 255 70 72 64) 2), 1, 1, $w-2, $h-2)
  $cols = 9; $rows = 2
  for ($r=0; $r -lt $rows; $r++) { for ($c=0; $c -lt $cols; $c++) {
    $x = 9 + $c*($w-18)/($cols-1); $y = $h*0.33 + $r*$h*0.34
    $roll = $rnd.NextDouble(); $col = if ($roll -lt 0.62) { C 255 70 230 100 } elseif ($roll -lt 0.8) { C 255 240 170 40 } elseif ($roll -lt 0.9) { C 255 230 50 40 } else { C 255 40 44 40 }
    if ($col.R -ne 40) { Blotch $g ([float]$x) ([float]$y) 6 5 ([System.Drawing.Color]::FromArgb(90, $col.R, $col.G, $col.B)) }
    $g.FillEllipse((Brush $col), [float]($x-2.5), [float]($y-2.5), 5, 5) } }
  Grime $g $w $h 50 ($seed+2) 130 }

function Hex-Points([float]$cx, [float]$cy, [float]$r) {
  $pts = @(); for ($i=0; $i -lt 6; $i++) { $a = ($i*60 - 90) * [math]::PI / 180; $pts += ,@(($cx + [math]::Cos($a)*$r), ($cy + [math]::Sin($a)*$r)) }; return ,$pts }

function Draw-CoreHalf($g, [float]$w, [float]$h, [string]$hex, [int]$seed) {
  # The LEFT half of a Core emblem, centered on the sprite's right edge. On a mirrored face, placed against
  # the mirror seam, it shows up as one whole glowing hexagon.
  $rnd = New-Object System.Random $seed
  $tier = Hex 255 $hex; $cx = $w; $cy = $h/2; $R = $h*0.46
  Poly $g (Brush (C 255 26 28 23)) (Hex-Points $cx $cy $R)
  $outer = Hex-Points $cx $cy $R; $arr = @(); foreach ($p in $outer) { $arr += PtF $p[0] $p[1] }
  $g.DrawPolygon((Pen (C 255 120 122 112) 3), [System.Drawing.PointF[]]$arr)
  foreach ($p in $outer) { $g.FillEllipse((Brush (C 255 80 80 74)), [float]($p[0]-3), [float]($p[1]-3), 6, 6) }
  Blotch $g $cx $cy ($R*0.95) ($R*0.95) ([System.Drawing.Color]::FromArgb(120, $tier.R, $tier.G, $tier.B))
  $ring = Hex-Points $cx $cy ($R*0.66); $arr = @(); foreach ($p in $ring) { $arr += PtF $p[0] $p[1] }
  $g.DrawPolygon((Pen ([System.Drawing.Color]::FromArgb(255, [math]::Min(255,$tier.R+70), [math]::Min(255,$tier.G+70), [math]::Min(255,$tier.B+70))) 4), [System.Drawing.PointF[]]$arr)
  $g.DrawPolygon((Pen $tier 7), [System.Drawing.PointF[]]$arr)
  Blotch $g $cx $cy ($R*0.42) ($R*0.42) ([System.Drawing.Color]::FromArgb(220, $tier.R, $tier.G, $tier.B))
  Blotch $g $cx $cy ($R*0.2) ($R*0.2) (C 255 235 245 255)
  # Circuit traces running out to the left.
  for ($i=0; $i -lt 5; $i++) {
    $y = $cy + ($i-2)*$R*0.32; $x0 = $cx - $R*0.72; $x1 = $x0 - $R*(0.15+$rnd.NextDouble()*0.2)
    $g.DrawLine((Pen ([System.Drawing.Color]::FromArgb(200, $tier.R, $tier.G, $tier.B)) 1.6), [float]$x0, [float]$y, [float]$x1, [float]$y)
    $g.FillEllipse((Brush $tier), [float]($x1-2), [float]($y-2), 4, 4) }
  Grime $g $w $h 70 ($seed+1) 120 }

function Draw-HazardBand($g, [float]$w, [float]$h, [int]$seed) {
  $rnd = New-Object System.Random $seed
  $g.FillRectangle((Brush (C 245 200 160 30)), 0, 0, $w, $h)
  for ($x = -$h; $x -lt $w + $h; $x += 18) { Poly $g (Brush (C 250 22 22 20)) @(@($x,$h),@(($x+9),$h),@(($x+9+$h),0),@(($x+$h),0)) }
  for ($i=0; $i -lt 10; $i++) { Wipe $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h)) ([float](6+$rnd.Next(18))) ([float](4+$rnd.Next(10))) }
  for ($i=0; $i -lt 40; $i++) { Wipe $g ([float]($rnd.NextDouble()*$w)) ([float]($rnd.NextDouble()*$h)) 2 2 }
  Grime $g $w $h 160 ($seed+1) 170 }

function Draw-CableRun($g, [float]$w, [float]$h, [int]$seed) {
  # Thick armored cables running down into the ground, with clamps.
  $rnd = New-Object System.Random $seed
  for ($k=0; $k -lt 3; $k++) {
    $x = $w*(0.22 + $k*0.28); $pts = @(); for ($y = 0; $y -le $h; $y += $h/6) { $pts += PtF ($x + $rnd.Next(-2,3)) $y }
    $p = [System.Drawing.PointF[]]$pts
    $g.DrawCurve((Pen (C 255 12 13 11) 11), $p); $g.DrawCurve((Pen (C 255 46 48 42) 8), $p)
    for ($y = 4; $y -lt $h; $y += 6) { $g.DrawLine((Pen (C 120 20 20 18) 1), [float]($x-4), [float]$y, [float]($x+4), [float]$y) }
    $g.DrawCurve((Pen (C 60 200 200 190) 1.2), $p) }
  for ($y = $h*0.15; $y -lt $h; $y += $h*0.3) { $g.FillRectangle((Brush (C 255 70 70 64)), 2, [float]$y, $w-4, 7); $g.DrawRectangle((Pen (C 255 20 20 18) 1), 2, [float]$y, $w-4, 7) }
  Blotch $g ($w/2) ($h*0.95) ($w*0.6) ($h*0.08) (C 170 70 46 24)
  Grime $g $w $h 120 ($seed+2) 170 }

# ---------- Sprite list: name, width, height, drawer. Fixed order = fixed packing. ----------
$sprites = @(
  @('crtScreen', 256, 192, { param($g,$w,$h) Draw-Crt $g $w $h 1 }),
  @('stencilStashnet', 256, 48, { param($g,$w,$h) Draw-Stencil $g $w $h 'STASHNET' 2 38 }),
  @('rustBase', 256, 40, { param($g,$w,$h) Draw-RustBase $g $w $h 3 }),
  @('stripeBlue', 256, 24, { param($g,$w,$h) Draw-Stripe $g $w $h '3A6FB0' 4 }),
  @('stripeGreen', 256, 24, { param($g,$w,$h) Draw-Stripe $g $w $h '4CAF50' 5 }),
  @('stripePurple', 256, 24, { param($g,$w,$h) Draw-Stripe $g $w $h '9C5FD0' 6 }),
  @('labelUnitBay', 256, 32, { param($g,$w,$h) Draw-Label $g $w $h 'UNIT 01 - BAY' 7 }),
  @('labelUnitCore', 256, 32, { param($g,$w,$h) Draw-Label $g $w $h 'UNIT 00 - CORE' 8 }),
  @('labelUnitTerminal', 256, 32, { param($g,$w,$h) Draw-Label $g $w $h 'UNIT 02 - TERM' 9 }),
  @('labelUnitCraft', 256, 32, { param($g,$w,$h) Draw-Label $g $w $h 'UNIT 03 - FAB' 10 }),
  @('readoutDrv4', 160, 56, { param($g,$w,$h) Draw-Readout $g $w $h 'DRV 4/4 OK' 11 }),
  @('readoutDrv8', 160, 56, { param($g,$w,$h) Draw-Readout $g $w $h 'DRV 8/8 OK' 12 }),
  @('readoutDrv12', 160, 56, { param($g,$w,$h) Draw-Readout $g $w $h 'DRV 12/12' 13 }),
  @('readoutCore', 160, 56, { param($g,$w,$h) Draw-Readout $g $w $h 'CORE OK' 14 }),
  @('readoutCraft', 160, 56, { param($g,$w,$h) Draw-Readout $g $w $h 'CRAFT OK' 15 }),
  @('clawGouge', 128, 192, { param($g,$w,$h) Draw-Claw $g $w $h 16 }),
  @('scorch1', 160, 128, { param($g,$w,$h) Draw-Scorch $g $w $h 17 }),
  @('scorch2', 160, 128, { param($g,$w,$h) Draw-Scorch $g $w $h 18 }),
  @('cableBundle', 160, 96, { param($g,$w,$h) Draw-Cables $g $w $h 19 }),
  @('pryMarks', 64, 160, { param($g,$w,$h) Draw-Pry $g $w $h 20 }),
  @('rustStreak1', 40, 200, { param($g,$w,$h) Draw-RustStreak $g $w $h 21 }),
  @('rustStreak2', 40, 200, { param($g,$w,$h) Draw-RustStreak $g $w $h 22 }),
  @('conduit', 24, 256, { param($g,$w,$h) Draw-Conduit $g $w $h 23 }),
  @('bareMetal1', 96, 80, { param($g,$w,$h) Draw-BareMetal $g $w $h 24 }),
  @('bareMetal2', 96, 80, { param($g,$w,$h) Draw-BareMetal $g $w $h 25 }),
  @('bareMetal3', 96, 80, { param($g,$w,$h) Draw-BareMetal $g $w $h 26 }),
  @('dent1', 96, 64, { param($g,$w,$h) Draw-Dent $g $w $h 27 }),
  @('wireSpark', 96, 80, { param($g,$w,$h) Draw-Spark $g $w $h 28 }),
  @('ductTape', 96, 28, { param($g,$w,$h) Draw-DuctTape $g $w $h 29 }),
  @('bulletHole1', 48, 48, { param($g,$w,$h) Draw-BulletHole $g $w $h 30 $true }),
  @('bulletHole2', 48, 48, { param($g,$w,$h) Draw-BulletHole $g $w $h 31 $true }),
  @('bulletDent', 48, 48, { param($g,$w,$h) Draw-BulletHole $g $w $h 32 $false }),
  @('paintPatch', 128, 96, { param($g,$w,$h) Draw-PaintPatch $g $w $h 51 }),
  @('ledPanel', 160, 80, { param($g,$w,$h) Draw-LedPanel $g $w $h 52 }),
  @('ventGrille', 160, 80, { param($g,$w,$h) Draw-Vent $g $w $h 53 }),
  @('portPanel', 160, 72, { param($g,$w,$h) Draw-Ports $g $w $h 54 }),
  @('warningLabel', 176, 64, { param($g,$w,$h) Draw-Warning $g $w $h 55 }),
  @('ledStrip', 144, 40, { param($g,$w,$h) Draw-LedStrip $g $w $h 56 }),
  @('coreHalfBlue', 64, 128, { param($g,$w,$h) Draw-CoreHalf $g $w $h '3A6FB0' 61 }),
  @('coreHalfGreen', 64, 128, { param($g,$w,$h) Draw-CoreHalf $g $w $h '4CAF50' 62 }),
  @('coreHalfPurple', 64, 128, { param($g,$w,$h) Draw-CoreHalf $g $w $h '9C5FD0' 63 }),
  @('hazardBand', 256, 24, { param($g,$w,$h) Draw-HazardBand $g $w $h 64 }),
  @('cableRun', 40, 256, { param($g,$w,$h) Draw-CableRun $g $w $h 65 }),
  @('glyphA', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h 'A' }),
  @('glyphB', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h 'B' }),
  @('glyphC', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h 'C' }),
  @('glyphD', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h 'D' }),
  @('glyphE', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h 'E' }),
  @('glyphF', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h 'F' }),
  @('glyphG', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h 'G' }),
  @('glyphH', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h 'H' }),
  @('glyph1', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h '1' }),
  @('glyph2', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h '2' }),
  @('glyph3', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h '3' }),
  @('glyph4', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h '4' }),
  @('glyph5', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h '5' }),
  @('glyph6', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h '6' }),
  @('glyph7', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h '7' }),
  @('glyph8', 28, 36, { param($g,$w,$h) Draw-Glyph $g $w $h '8' })
)

# ---------- Shelf packing + drawing ----------
$atlas = New-Object System.Drawing.Bitmap $AtlasW, $AtlasH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$ga = [System.Drawing.Graphics]::FromImage($atlas)
$ga.Clear([System.Drawing.Color]::Transparent); $ga.InterpolationMode = 'HighQualityBicubic'; $ga.PixelOffsetMode = 'HighQuality'; $ga.CompositingMode = 'SourceCopy'
$x = $Pad; $y = $Pad; $shelfH = 0
$xml = New-Object System.Text.StringBuilder
[void]$xml.AppendLine("<atlas width=`"$AtlasW`" height=`"$AtlasH`">")
foreach ($sp in $sprites) {
  $name = $sp[0]; $w = $sp[1]; $h = $sp[2]
  if ($x + $w + $Pad -gt $AtlasW) { $x = $Pad; $y += $shelfH + 2*$Pad; $shelfH = 0 }
  if ($y + $h + $Pad -gt $AtlasH) { throw "atlas full at $name" }
  $bmp = New-Object System.Drawing.Bitmap ($w*$S), ($h*$S), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::Transparent); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
  $g.ScaleTransform($S, $S)
  & $sp[3] $g ([float]$w) ([float]$h)
  $g.Dispose()
  $ga.DrawImage($bmp, (New-Object System.Drawing.Rectangle $x, $y, $w, $h), 0, 0, ($w*$S), ($h*$S), ([System.Drawing.GraphicsUnit]::Pixel))
  $bmp.Dispose()
  [void]$xml.AppendLine("  <sprite name=`"$name`" x=`"$x`" y=`"$y`" w=`"$w`" h=`"$h`" />")
  $x += $w + 2*$Pad; if ($h -gt $shelfH) { $shelfH = $h }
}
[void]$xml.AppendLine('</atlas>')
$ga.Dispose()
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force $OutDir | Out-Null }
$atlas.Save((Join-Path (Resolve-Path $OutDir) 'stashnet-skin.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$atlas.Dispose()
[IO.File]::WriteAllText((Join-Path (Resolve-Path $OutDir) 'atlas.xml'), $xml.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Output "Wrote $($sprites.Count) sprites to $OutDir"

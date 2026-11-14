# Draws StashNet block icons (160x160 transparent PNGs) in the "wrecked but working" salvage look: each starts
# from the vanilla icon of the block's model (read from the game install, never committed itself), repainted
# army green, then dressed with the same decals the mod puts on the model in-game (from Mod/Skins). The file
# names match the block names, so the game uses them when a block has no CustomIcon. Deterministic. Run:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/icons/draw-block-icons.ps1 -GameDir "<7 Days To Die folder>" -OutDir Mod/UIAtlases/ItemIconAtlas [-Only <blockName>]
param([Parameter(Mandatory=$true)][string]$GameDir, [string]$OutDir = '.', [string]$Only = '')
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$skinDir = Join-Path $root 'Mod/Skins'

# Block -> vanilla icon, army-green tint, tier stripe sprite, readout sprite, label sprite, seed.
$blocks = @(
  # No readouts (owner removed the "DRV n/n" readouts in-game, 2026-11-13). Stripe = tier color, like in-game.
  @{ Name='stashnetDriveBay'; Icon='cntFileCabinetTallClosed'; Tint='4A503F'; Stripe='stripeBlue'; Seed=101 },
  @{ Name='stashnetDriveBayReinforced'; Icon='decoServerRackWhite'; Tint='4A503F'; Stripe='stripeGreen'; Seed=102 },
  @{ Name='stashnetDriveBayArmored'; Icon='cntGunSafeWhite'; Tint='3A3F31'; Stripe='stripePurple'; Seed=103 },
  @{ Name='stashnetScrapCore'; Icon='electricalBox02'; Tint='4A503F'; Stripe='stripeBlue'; Emblem='coreHalfBlue'; Seed=111 },
  @{ Name='stashnetStorageCore'; Icon='controlPanelBase05'; Tint='4A503F'; Stripe='stripeGreen'; Emblem='coreHalfGreen'; Seed=112 },
  @{ Name='stashnetAdvancedCore'; Icon='controlPanelBase01'; Tint='4A503F'; Stripe='stripePurple'; Emblem='coreHalfPurple'; Seed=113 },
  @{ Name='stashnetTerminal'; Icon='tvCRT_Player'; Tint='4A503F'; Stripe='stripeGreen'; Seed=121 },
  @{ Name='stashnetCraftingTerminal'; Icon='workbench'; Tint='4A503F'; Stripe='stripeGreen'; Seed=122 },
  # v0.19.0 StashNet stations: their vanilla icon, StashNet green tint and stripe.
  @{ Name='stashnetForge'; Icon='forge'; Tint='4A503F'; Stripe='stripeGreen'; Seed=123; LabelU=0.66; LabelV=0.55; LabelW=0.42 },
  @{ Name='stashnetChemistryStation'; Icon='chemistryStation'; Tint='4A503F'; Stripe='stripeGreen'; Seed=124; LabelU=0.6; LabelV=0.55; LabelW=0.4 },
  @{ Name='stashnetCampfire'; Icon='campfire'; Tint='4A503F'; Stripe='stripeGreen'; Seed=125; LabelV=0.45; LabelW=0.5 },
  @{ Name='stashnetCementMixer'; Icon='cementMixer'; Tint='4A503F'; Stripe='stripeGreen'; Seed=126; LabelU=0.4; LabelV=0.8; LabelW=0.48 }
)

# ---- atlas sprites ----
$atlas = [System.Drawing.Image]::FromFile((Join-Path $skinDir 'stashnet-skin.png'))
$rects = @{}
foreach ($s in ([xml](Get-Content (Join-Path $skinDir 'atlas.xml') -Raw)).atlas.sprite) { $rects[$s.name] = New-Object System.Drawing.Rectangle ([int]$s.x), ([int]$s.y), ([int]$s.w), ([int]$s.h) }

function Sprite($g, [string]$name, [float]$cx, [float]$cy, [float]$w, [float]$h, [float]$rot = 0) {
  $r = $rects[$name]; if ($null -eq $r) { throw "unknown sprite $name" }
  $state = $g.Save(); $g.TranslateTransform($cx, $cy); if ($rot -ne 0) { $g.RotateTransform(-$rot) }
  $g.DrawImage($atlas, (New-Object System.Drawing.RectangleF (-$w/2), (-$h/2), $w, $h), $r, ([System.Drawing.GraphicsUnit]::Pixel))
  $g.Restore($state) }

# Repaint: keep each pixel's brightness and alpha, swap its hue for the tint (lifted so the shape still reads).
function Repaint([System.Drawing.Bitmap]$bmp, [string]$hex) {
  $tr = [Convert]::ToInt32($hex.Substring(0,2),16); $tg = [Convert]::ToInt32($hex.Substring(2,2),16); $tb = [Convert]::ToInt32($hex.Substring(4,2),16)
  $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
  $data = $bmp.LockBits($rect, 'ReadWrite', ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
  $n = $bmp.Width * $bmp.Height * 4; $buf = New-Object byte[] $n
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $n)
  for ($i = 0; $i -lt $n; $i += 4) {
    if ($buf[$i+3] -eq 0) { continue }
    $l = (0.114*$buf[$i] + 0.587*$buf[$i+1] + 0.299*$buf[$i+2]) / 255.0
    $k = 0.3 + 1.25*$l
    $buf[$i]   = [byte][math]::Min(255, [int]($tb*$k))
    $buf[$i+1] = [byte][math]::Min(255, [int]($tg*$k))
    $buf[$i+2] = [byte][math]::Min(255, [int]($tr*$k)) }
  [System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $data.Scan0, $n)
  $bmp.UnlockBits($data) }

# Box around the icon's visible pixels, so decals land on the object whatever its shape.
function OpaqueBox([System.Drawing.Bitmap]$bmp) {
  $minX = $bmp.Width; $minY = $bmp.Height; $maxX = 0; $maxY = 0
  for ($y = 0; $y -lt $bmp.Height; $y += 2) { for ($x = 0; $x -lt $bmp.Width; $x += 2) {
    if ($bmp.GetPixel($x, $y).A -gt 40) { if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } } } }
  New-Object System.Drawing.RectangleF $minX, $minY, ($maxX - $minX), ($maxY - $minY) }

foreach ($b in $blocks) {
  if ($Only -and $b.Name -ne $Only) { continue }
  $src = [System.Drawing.Image]::FromFile((Join-Path $GameDir "Data\ItemIcons\$($b.Icon).png"))
  $bmp = New-Object System.Drawing.Bitmap 160, 160, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::Transparent); $g.DrawImage($src, 0, 0, 160, 160); $g.Dispose(); $src.Dispose()
  Repaint $bmp $b.Tint
  $box = OpaqueBox $bmp
  # Decals only where the object is: clip to its own silhouette.
  $mask = New-Object System.Drawing.Bitmap $bmp
  $g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
  $X = { param($u) $box.X + $u * $box.Width }; $Y = { param($v) $box.Y + (1 - $v) * $box.Height }
  $W = $box.Width; $H = $box.Height
  $rnd = New-Object System.Random $b.Seed
  Sprite $g 'scorch1' (& $X 0.62) (& $Y 0.80) ($W*0.55) ($H*0.22)
  Sprite $g 'scorch2' (& $X 0.30) (& $Y 0.15) ($W*0.50) ($H*0.18)
  Sprite $g 'rustStreak1' (& $X 0.22) (& $Y 0.58) ($W*0.10) ($H*0.40)
  Sprite $g 'rustBase' (& $X 0.5) (& $Y 0.04) ($W*0.95) ($H*0.08)
  Sprite $g 'clawGouge' (& $X 0.32) (& $Y 0.50) ($W*0.36) ($H*0.30) 15
  Sprite $g 'bareMetal1' (& $X 0.15) (& $Y 0.94) ($W*0.28) ($H*0.10)
  Sprite $g $b.Stripe (& $X 0.5) (& $Y 0.28) ($W*0.92) ($H*0.06)
  # Label spot: top center by default; LabelU/V/W move it onto the body for odd shapes (stations).
  $lu = if ($b.LabelU) { $b.LabelU } else { 0.5 }; $lv = if ($b.LabelV) { $b.LabelV } else { 0.84 }; $lw = if ($b.LabelW) { $b.LabelW } else { 0.62 }
  Sprite $g 'stencilStashnet' (& $X $lu) (& $Y $lv) ($W*$lw) ($H*0.08*$lw/0.62)
  if ($b.Emblem) {
    # Whole core emblem: the half sprite plus the same half turned 180 (the hexagon is symmetric), like in-game.
    $e = [math]::Min($W, $H) * 0.34
    Sprite $g $b.Emblem ((& $X 0.5) - $e/4) (& $Y 0.55) ($e/2) $e
    Sprite $g $b.Emblem ((& $X 0.5) + $e/4) (& $Y 0.55) ($e/2) $e 180 }
  foreach ($k in 0..2) { Sprite $g 'bulletHole1' (& $X (0.55 + $rnd.NextDouble()*0.3)) (& $Y (0.4 + $rnd.NextDouble()*0.25)) 9 9 ($rnd.Next(0,360)) }
  $g.Dispose()
  # Put the original silhouette back: anything drawn outside it becomes transparent again.
  for ($y = 0; $y -lt 160; $y++) { for ($x = 0; $x -lt 160; $x++) {
    $a = $mask.GetPixel($x, $y).A; $p = $bmp.GetPixel($x, $y)
    if ($p.A -ne $a) { $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([math]::Min($a, $p.A), $p.R, $p.G, $p.B)) } } }
  $mask.Dispose()
  if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force $OutDir | Out-Null }
  $bmp.Save((Join-Path (Resolve-Path $OutDir) "$($b.Name).png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Output "Wrote $($b.Name).png"
}
$atlas.Dispose()

<#
.SYNOPSIS
  Cuts a StashNet release on this PC (the build needs the game's own DLLs, so it can't run on GitHub).

.DESCRIPTION
  Always: checks the working tree is clean, sets the version in Mod/ModInfo.xml and
  src/StashNet/StashNetMod.cs, runs the tests, builds, and writes dist/StashNet-v<version>.zip.
  -Tag     also commits the version change (if any) and creates the git tag v<version>.
  -Publish also pushes the branch and tag to GitHub and creates a GitHub Release with the zip
           attached (implies -Tag). Releases cut from a branch other than master, or below 1.0.0,
           are marked as pre-releases.

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools/release.ps1 -Version 0.17.0
  powershell -NoProfile -ExecutionPolicy Bypass -File tools/release.ps1 -Version 1.0.0 -Publish
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$Tag,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
if ($Publish) { $Tag = $true }
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Fail([string]$msg) { Write-Host "release: $msg" -ForegroundColor Red; exit 1 }
function Step([string]$msg) { Write-Host "== $msg" -ForegroundColor Cyan }
function Run([string]$exe, [string[]]$argList) {
    & $exe @argList
    if ($LASTEXITCODE -ne 0) { Fail "'$exe $($argList -join ' ')' failed (exit $LASTEXITCODE)" }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') { Fail "version must look like 1.2.3 (got '$Version')" }
$tagName = "v$Version"

Step "Checking the working tree"
$dirty = git status --porcelain
if ($dirty) { Fail "uncommitted changes; commit or stash them first:`n$dirty" }
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($Tag -and (git tag --list $tagName)) { Fail "tag $tagName already exists" }

Step "Setting version $Version"
$utf8 = New-Object System.Text.UTF8Encoding($false)
$modInfo = Join-Path $root 'Mod\ModInfo.xml'
$modCs = Join-Path $root 'src\StashNet\StashNetMod.cs'
$infoText = [IO.File]::ReadAllText($modInfo)
$csText = [IO.File]::ReadAllText($modCs)
$newInfo = [regex]::Replace($infoText, '<Version value="[^"]*" />', "<Version value=""$Version"" />")
$newCs = [regex]::Replace($csText, 'public const string Version = "[^"]*";', "public const string Version = ""$Version"";")
if ($newInfo -eq $infoText -and $infoText -notmatch "<Version value=""$([regex]::Escape($Version))"" />") { Fail "couldn't find the version in ModInfo.xml" }
if ($newCs -eq $csText -and $csText -notmatch "Version = ""$([regex]::Escape($Version))"";") { Fail "couldn't find the version constant in StashNetMod.cs" }
[IO.File]::WriteAllText($modInfo, $newInfo, $utf8)
[IO.File]::WriteAllText($modCs, $newCs, $utf8)

Step "Running tests"
Run 'dotnet' @('test', 'tests/StashNet.Core.Tests', '--nologo', '-v', 'q')

Step "Building the release zip"
Run 'dotnet' @('build', 'src/StashNet/StashNet.csproj', '-c', 'Release', '-p:Package=true', '--nologo', '-v', 'q')
$zip = Join-Path $root "dist\StashNet-v$Version.zip"
if (-not (Test-Path $zip)) { Fail "expected $zip but it wasn't written" }
Write-Host "zip: $zip ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"

if (-not $Tag) {
    $changed = git status --porcelain
    if ($changed) { Write-Host "Version files changed but were NOT committed (run with -Tag or -Publish to commit and tag)." -ForegroundColor Yellow }
    Write-Host "Done (local only)." -ForegroundColor Green
    exit 0
}

Step "Committing and tagging $tagName"
if (git status --porcelain) {
    $msgFile = Join-Path ([IO.Path]::GetTempPath()) 'stashnet-release-msg.txt'
    [IO.File]::WriteAllText($msgFile, "Release $tagName`n", $utf8)
    Run 'git' @('add', 'Mod/ModInfo.xml', 'src/StashNet/StashNetMod.cs')
    Run 'git' @('commit', '-q', '-F', $msgFile)
}
Run 'git' @('tag', '-a', $tagName, '-m', "StashNet $tagName")

if (-not $Publish) {
    Write-Host "Done: committed and tagged $tagName locally (nothing pushed)." -ForegroundColor Green
    exit 0
}

Step "Pushing $branch and $tagName"
Run 'git' @('push', 'origin', $branch)
Run 'git' @('push', 'origin', $tagName)

Step "Creating the GitHub Release"
$ghArgs = @('release', 'create', $tagName, $zip, '--title', "StashNet $tagName", '--generate-notes', '--target', $branch)
$major = [int]($Version.Split('.')[0])
if ($branch -ne 'master' -or $major -lt 1) { $ghArgs += '--prerelease' }
Run 'gh' $ghArgs
Write-Host "Done: $tagName published with $([IO.Path]::GetFileName($zip))." -ForegroundColor Green

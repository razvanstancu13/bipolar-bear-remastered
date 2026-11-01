# Builds the game (GameBuild.Windows) and plays the test runs of GameTestRun (-bear-test, see that script):
# normal, walk, eskimo, roar, punch, relocate. Screenshots + logs go to Logs\test\. Passes when every run finishes with 0 errors, every
# model plays a clip and, in the standing runs, the bear's soles are on the ground.
#   powershell -File tools\test_run.ps1             (build + all runs)
#   powershell -File tools\test_run.ps1 -NoBuild    (runs only, on the last build)
#   powershell -File tools\test_run.ps1 -Modes walk
# Ends with OK: ... or FAIL: ...
param([switch]$NoBuild, [string[]]$Modes = @("normal", "walk", "eskimo", "roar", "punch", "relocate"), [int]$Width = 1600, [int]$Height = 900,
      [int]$TimeoutSec = 90)
. "$PSScriptRoot\common.ps1"
$exe = Join-Path $ProjectDir "Builds\Windows\BipolarBear.exe"
$out = Join-Path $ProjectDir "Logs\test"
$standing = @("normal", "roar")                   # modes where the bear stands still: feet= must be ~0

# (no file-date check on the build: an incremental build leaves unchanged files, the .exe keeps Unity's own date)
if (-not $NoBuild -and -not (Invoke-UnityMethod -Method GameBuild.Windows -Expect "BB build: Succeeded")) { Write-Output "FAIL: build"; exit 1 }
if (-not (Test-Path $exe)) { Write-Output "FAIL: no build at $exe"; exit 1 }
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\*" -ErrorAction SilentlyContinue

# one run; returns what went wrong (empty = passed)
function Test-Mode([string]$mode) {
  $log = Join-Path $out "$mode.log"
  $a = @("-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height", "-logFile", $log, "-bear-test", $out)
  if ($mode -ne "normal") { $a += $mode }
  $p = Start-Process $exe -ArgumentList $a -PassThru
  $null = $p.Handle
  if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); return @("$mode (timed out)") }
  $lines = @(Select-String -Path $log -Pattern "BEARTEST" | ForEach-Object { $_.Line })
  $lines | ForEach-Object { Write-Host "  [$mode] $_" }
  $bad = @()
  $done = $lines | Where-Object { $_ -match "BEARTEST done" } | Select-Object -Last 1
  if (-not $done) { $bad += "$mode (no done line)" }
  elseif ($done -notmatch "errors=0") { $bad += "$mode ($done)" }
  if ($lines -match "clip=NONE") { $bad += "$mode (a model played no clip)" }
  if ($standing -contains $mode) {
    # the bear's soles must touch the ground (gap within 0.1 units); the log may write a decimal comma
    foreach ($l in $lines) {
      if ($l -match "feet=(-?\d+[.,]\d+)" -and [math]::Abs([double]($matches[1] -replace ",", ".")) -gt 0.1) { $bad += "$mode (feet off the ground: $($matches[1]))"; break }
    }
  }
  return $bad
}

$bad = @()
foreach ($m in $Modes) { $bad += @(Test-Mode $m) }
$shots = @(Get-ChildItem "$out\*.png" -ErrorAction SilentlyContinue).Count
if ($bad.Count) { Write-Output ("FAIL: " + ($bad -join "; ")); exit 1 }
Write-Output "OK: test runs $($Modes -join ', ') passed, 0 errors, $shots screenshots in Logs\test"

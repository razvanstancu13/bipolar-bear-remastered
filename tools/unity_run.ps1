# Runs one static editor method of this project in Unity batch mode and checks its log.
#   powershell -File tools\unity_run.ps1 -Method SceneDump.Run
#   powershell -File tools\unity_run.ps1 -Method Make3D.Shots -Graphics      (rendering needs graphics)
# Ends with OK: ... or FAIL: ... (the method must log the -Expect text; default "OK:").
param(
  [Parameter(Mandatory = $true)][string]$Method,
  [string]$Expect = "OK:",
  [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe",
  [int]$TimeoutMin = 40,
  [switch]$Graphics
)
. "$PSScriptRoot\common.ps1"
if (Test-EditorOpen) { Write-Output "FAIL: the project is open in the Unity Editor - close it first"; exit 1 }
New-Item -ItemType Directory -Force "$ProjectDir\Logs" | Out-Null
$log = "$ProjectDir\Logs\unity_run_$($Method -replace '[^A-Za-z0-9]','_').log"
$start = Get-Date
$uargs = @("-batchmode") + $(if ($Graphics) { @() } else { @("-nographics") }) +
  @("-projectPath", "`"$ProjectDir`"", "-executeMethod", $Method, "-logFile", "`"$log`"")
$p = Start-Process -FilePath $Unity -PassThru -NoNewWindow -ArgumentList $uargs
$null = $p.Handle   # keeps the handle so ExitCode is readable after WaitForExit
if (-not $p.WaitForExit($TimeoutMin * 60000)) { $p.Kill(); Write-Output "FAIL: timed out after $TimeoutMin min (log $log)"; exit 1 }
if (-not (Test-Path $log) -or (Get-Item $log).LastWriteTime -lt $start) { Write-Output "FAIL: no new log at $log"; exit 1 }
Select-String -Path $log -Pattern "error CS\d+|Exception:|FAIL:" | Select-Object -First 15 | ForEach-Object { Write-Output $_.Line }
$ok = Select-String -Path $log -Pattern ([regex]::Escape($Expect)) | Select-Object -Last 1
$secs = [int]((Get-Date) - $start).TotalSeconds
if ($ok -and $p.ExitCode -eq 0) { Write-Output "OK: $Method in $secs s - $($ok.Line.Trim())" }
else { Write-Output "FAIL: $Method exit $($p.ExitCode) after $secs s, '$Expect' $(if ($ok) {'found'} else {'not found'}) (log $log)"; exit 1 }

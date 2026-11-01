# Shared by the tools' PowerShell scripts. Load with:  . "$PSScriptRoot\common.ps1"
$ProjectDir = Split-Path $PSScriptRoot -Parent

# true while the Unity Editor has the project open (its lock file cannot be opened): batch runs cannot start then
function Test-EditorOpen {
  $lock = Join-Path $ProjectDir "Temp\UnityLockfile"
  if (-not (Test-Path $lock)) { return $false }
  try { [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Close(); return $false }
  catch { return $true }
}

# git.exe (not always on PowerShell's PATH here)
function Get-Git {
  $git = (Get-Command git -ErrorAction SilentlyContinue).Source
  if ($git) { return $git }
  return "C:\Program Files\Git\cmd\git.exe"
}

# runs one editor method through unity_run.ps1, writes its lines; returns $true when it ended OK
function Invoke-UnityMethod {
  param([string]$Method, [string]$Expect = "OK:", [switch]$Graphics)
  $a = @("-ExecutionPolicy", "Bypass", "-File", "$PSScriptRoot\unity_run.ps1", "-Method", $Method, "-Expect", $Expect)
  if ($Graphics) { $a += "-Graphics" }
  & powershell @a | ForEach-Object { Write-Host $_ }
  return ($LASTEXITCODE -eq 0)
}

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'dist\windows'
$exe = Join-Path $output (-join @([char]0x91CC, [char]0x7A0B, [char]0x7891, '.exe'))
$report = Join-Path $output 'test-results.txt'
if (Test-Path $report) { Remove-Item $report }
$process = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $report + '"')) -Wait -PassThru
if (Test-Path $report) { Get-Content $report }
if ($process.ExitCode -ne 0 -or !(Test-Path $report)) { throw "Tests failed: $($process.ExitCode)" }

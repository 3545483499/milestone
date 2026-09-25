$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'dist\windows'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path $compiler)) { throw 'The .NET Framework C# compiler is required.' }
$exe = Join-Path $output (-join @([char]0x91CC, [char]0x7A0B, [char]0x7891, '.exe'))
$sources = @('Model.cs', 'App.cs', 'SelfTests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    "/win32icon:$PSScriptRoot\AppIcon.ico" `
    "/win32manifest:$PSScriptRoot\app.manifest" `
    "/resource:$PSScriptRoot\AppIcon.ico,AppIcon.ico" `
    "/resource:$PSScriptRoot\Logo.png,Logo.png" "/out:$exe" $sources
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Write-Output $exe

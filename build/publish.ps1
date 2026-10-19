# Builds a single, self-contained TokenBob.exe (no .NET install needed on the PC that runs it).
#   powershell -ExecutionPolicy Bypass -File build/publish.ps1 [-Output <folder>] [-Version 0.2.0]
# -Version stamps the exe (Installed apps, the Update window, the log); without it the
# version from Directory.Build.props is used.
param([string]$Output = (Join-Path $PSScriptRoot '..\publish'), [string]$Version = '')
$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\TokenBob.App\TokenBob.App.csproj'
$versionArgs = @()
if ($Version) { $versionArgs = @("-p:Version=$Version") }
dotnet publish $project -c Release -r win-x64 --self-contained @versionArgs `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:AllowedReferenceRelatedFileExtensions=none `
    -o $Output
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$exe = Get-Item (Join-Path $Output 'TokenBob.exe')
'Published {0} ({1:N1} MB)' -f $exe.FullName, ($exe.Length / 1MB)

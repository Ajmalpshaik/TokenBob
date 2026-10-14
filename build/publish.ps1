# Builds a single, self-contained TokenBob.exe (no .NET install needed on the PC that runs it).
#   powershell -ExecutionPolicy Bypass -File build/publish.ps1 [-Output <folder>]
param([string]$Output = (Join-Path $PSScriptRoot '..\publish'))
$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\TokenBob.App\TokenBob.App.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:AllowedReferenceRelatedFileExtensions=none `
    -o $Output
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$exe = Get-Item (Join-Path $Output 'TokenBob.exe')
'Published {0} ({1:N1} MB)' -f $exe.FullName, ($exe.Length / 1MB)

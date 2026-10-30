param([switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../Zegar.csproj'
$output = Join-Path $PSScriptRoot $(if ($FrameworkDependent) { '../publish-framework' } else { '../publish' })
$selfContained = (-not $FrameworkDependent).ToString().ToLowerInvariant()
dotnet publish $project -c Release -r win-x64 --self-contained $selfContained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Budowanie nie powiodło się.' }

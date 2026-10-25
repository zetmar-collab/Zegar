$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../Zegar.csproj'
$output = Join-Path $PSScriptRoot '../publish'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Budowanie nie powiodło się.' }

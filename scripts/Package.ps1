param([string]$Version = '', [switch]$RequireSignature)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'Zegar.csproj') -Raw
$projectVersion = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrEmpty($Version)) { $Version = $projectVersion }
if ($Version -notmatch '^\d+\.\d+\.\d+$' -or $Version -ne $projectVersion) { throw 'Wersja pakietu musi odpowiadać wersji projektu.' }
foreach ($sourceFolder in @('publish','publish-framework')) {
    $exe = Join-Path $root "$sourceFolder/Zegar.exe"
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Brak pliku: $exe. Zbuduj obie wersje." }
    $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
    if ($fileVersion.FileMajorPart -ne ([version]$Version).Major -or $fileVersion.FileMinorPart -ne ([version]$Version).Minor -or $fileVersion.FileBuildPart -ne ([version]$Version).Build) { throw "Nieaktualny EXE w $sourceFolder. Zbuduj wersję $Version." }
    if ($RequireSignature) {
        $signature = Get-AuthenticodeSignature -LiteralPath $exe
        if ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate -or $signature.SignerCertificate.Subject -eq $signature.SignerCertificate.Issuer) { throw "Brak poprawnego podpisu wydawcy: $sourceFolder." }
    }
}
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
foreach ($variant in @('standalone','framework')) {
    $suffix = if ($variant -eq 'framework') { '-framework' } else { '' }
    $source = if ($variant -eq 'framework') { 'publish-framework' } else { 'publish' }
    $name = "Zegar-$Version-win-x64$suffix"
    # Each staging directory is unique; stale files cannot enter a new archive.
    $stage = Join-Path $dist ($name + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $stage 'publish'),(Join-Path $stage 'scripts'),(Join-Path $stage 'docs/images'),(Join-Path $stage 'assets') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root "$source/Zegar.exe") -Destination (Join-Path $stage 'publish/Zegar.exe')
    foreach ($file in @('Install.ps1','AutostartPolicy.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $stage 'scripts') }
    foreach ($file in @('README.md','LICENSE','CHANGELOG.md','VERIFICATION.md')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage }
    Copy-Item -Path (Join-Path $root 'docs/images/*.png') -Destination (Join-Path $stage 'docs/images')
    Copy-Item -Path (Join-Path $root 'docs/*.md') -Destination (Join-Path $stage 'docs')
    Copy-Item -LiteralPath (Join-Path $root 'assets/Zegar.png') -Destination (Join-Path $stage 'assets')
    if ($variant -eq 'framework') { 'Requires .NET 10.0.15 Windows Desktop Runtime x64 or a newer 10.0.x patch. / Wymaga .NET 10.0.15 Windows Desktop Runtime x64 lub nowszej poprawki 10.0.x.' | Set-Content -LiteralPath (Join-Path $stage 'RUNTIME-REQUIRED.txt') -Encoding UTF8 }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $dist ($name + '.zip')) -Force
}
$assets = @((Join-Path $root 'publish/Zegar.exe'),(Join-Path $dist "Zegar-$Version-win-x64.zip"),(Join-Path $dist "Zegar-$Version-win-x64-framework.zip"))
$assets | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) } | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII

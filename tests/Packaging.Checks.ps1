$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$folder = Join-Path ([IO.Path]::GetTempPath()) ('ZegarPackageChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $folder 'scripts'),(Join-Path $folder 'publish'),(Join-Path $folder 'publish-framework') -Force | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $root 'scripts/Package.ps1') -Destination (Join-Path $folder 'scripts/Package.ps1')
    [xml]$project = Get-Content -LiteralPath (Join-Path $root 'Zegar.csproj') -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    $fixtureProject = Join-Path $folder 'Zegar.csproj'
    '<Project><PropertyGroup><Version>9.9.9</Version></PropertyGroup></Project>' | Set-Content -LiteralPath $fixtureProject
    foreach ($target in @('publish','publish-framework')) {
        $exe = Join-Path $folder "$target/Zegar.exe"
        Copy-Item -LiteralPath (Join-Path $root 'publish-framework/Zegar.exe') -Destination $exe
        # Invalidate any existing signature on the disposable fixture, keeping PE version metadata.
        $stream = [IO.File]::Open($exe,[IO.FileMode]::Append)
        try { $stream.WriteByte(0) } finally { $stream.Dispose() }
    }
    function Expect-Rejection([string]$Expected, [scriptblock]$Action) {
        $rejected = $false
        try { & $Action } catch { $rejected = $_.Exception.Message -like "*$Expected*" }
        if (-not $rejected) { throw "Packaging guard failed: $Expected" }
    }
    $package = Join-Path $folder 'scripts/Package.ps1'
    Expect-Rejection 'Wersja pakietu' { & $package -Version '0.0.0' }
    Expect-Rejection 'Nieaktualny EXE' { & $package }
    "<Project><PropertyGroup><Version>$version</Version></PropertyGroup></Project>" | Set-Content -LiteralPath $fixtureProject
    Expect-Rejection 'Brak poprawnego podpisu' { & $package -RequireSignature }
    if (Test-Path -LiteralPath (Join-Path $folder 'dist')) { throw 'Guard created output before validation.' }
    Write-Output 'PASS: 3 packaging guards (version mismatch, stale EXE, required publisher signature).'
}
finally {
    $resolved = [IO.Path]::GetFullPath($folder)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'ZegarPackageChecks-*') { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

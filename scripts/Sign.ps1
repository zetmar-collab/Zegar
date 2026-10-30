param(
    [Parameter(Mandatory=$true)][string]$CertificateThumbprint,
    [string]$FilePath = (Join-Path $PSScriptRoot '../publish/Zegar.exe'),
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$certificate = Get-ChildItem Cert:/CurrentUser/My,Cert:/LocalMachine/My |
    Where-Object { $_.Thumbprint -eq $CertificateThumbprint } | Select-Object -First 1
if (-not $certificate -or -not $certificate.HasPrivateKey) { throw 'Nie znaleziono certyfikatu z dostępnym kluczem prywatnym.' }
if ($certificate.NotAfter -lt (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) { throw 'Certyfikat nie jest obecnie ważny.' }
if ($certificate.EnhancedKeyUsageList.ObjectId -notcontains '1.3.6.1.5.5.7.3.3') { throw 'Certyfikat nie jest przeznaczony do podpisywania kodu.' }
if ($certificate.Subject -eq $certificate.Issuer) { throw 'Wymagany jest certyfikat wydawcy, nie certyfikat samopodpisany.' }
if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) { throw 'Nie znaleziono EXE do podpisania.' }
$signature = Set-AuthenticodeSignature -LiteralPath $FilePath -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $TimestampServer
if ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate) { throw ('Nie udało się potwierdzić podpisu i znacznika czasu: ' + $signature.StatusMessage) }
Write-Output ('Podpisano: ' + (Resolve-Path -LiteralPath $FilePath).Path)

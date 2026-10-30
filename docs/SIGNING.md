# Podpisywanie wydania Windows

Wersja 1.4.0 nie ma podpisu Authenticode. Właściciel projektu nie ma obecnie certyfikatu wydawcy. Ikona i metadane EXE nie zastępują podpisu.

Po uzyskaniu certyfikatu do podpisywania kodu, z dostępnym kluczem prywatnym w magazynie Windows `CurrentUser/My` lub `LocalMachine/My`:

```powershell
./scripts/Build.ps1
./scripts/Build.ps1 -FrameworkDependent
./scripts/Sign.ps1 -CertificateThumbprint 'ODCISK_CERTYFIKATU'
./scripts/Sign.ps1 -CertificateThumbprint 'ODCISK_CERTYFIKATU' -FilePath ./publish-framework/Zegar.exe
Get-AuthenticodeSignature ./publish/Zegar.exe
./scripts/Package.ps1
```

Skrypt sprawdza ważność, klucz prywatny i przeznaczenie Code Signing, odrzuca certyfikat samopodpisany, podpisuje SHA-256 i żąda znacznika czasu. Kończy się błędem, jeżeli podpis nie ma statusu `Valid` lub brak potwierdzonego znacznika czasu. Domyślny serwer znacznika to DigiCert; można wskazać inny przez `-TimestampServer`.

Podpisuj po ostatniej kompilacji, przed pakowaniem i obliczeniem sum kontrolnych. Kolejna kompilacja zastępuje podpisany plik. Nie zapisuj certyfikatu, klucza ani hasła w repozytorium. Certyfikaty na tokenach sprzętowych lub usługi zdalnego podpisywania mogą wymagać narzędzia dostawcy; ten skrypt używa dostawcy certyfikatu Windows. CI buduje wersję niepodpisaną i nie przechowuje kluczy wydawcy.

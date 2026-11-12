# Zegar HTC 1.5.0

Wersja lokalna z 12 listopada 2026. Przygotowanie plików nie publikuje wydania w GitHub i nie aktualizuje automatycznie zainstalowanej aplikacji.

## Nowe funkcje

1. **Tryb kompaktowy:** Ustawienia → Tryb kompaktowy. Mniejszy widżet 320 × 252 zamiast 420 × 382, przed skalowaniem. Godzina, temperatura, miejscowość i status danych pozostają widoczne. AM/PM przenosi się do nagłówka.
2. **Ulubione miejscowości:** dodaj bieżącą miejscowość lub wybrany wynik wyszukiwania. Wybieraj lokalizacje w ustawieniach lub przez menu PPM. Maksymalnie 30 pozycji. Zmiany w edytorze wymagają zapisania ustawień.
3. **Prognoza godzinowa:** Prognoza → Najbliższe 24 godziny. Temperatura, prawdopodobieństwo opadów, opis i wiatr w strefie miejscowości, także z cache. Offset strefy przy godzinie odróżnia powtórzone godziny przy zmianie czasu.
4. **Jednostki:** temperatura °C/°F, wiatr km/h, m/s lub mph. Dane są przechowywane w °C i km/h, a przeliczanie nie wymaga Internetu.
5. **Profile wyglądu:** zapisz nazwany zestaw kolorów, czcionki, skali, przezroczystości i trybu kompaktowego; zastosuj go w ustawieniach. Maksymalnie 20 profili. Zapis ustawień zatwierdza zmiany; Anuluj je odrzuca.
6. **Minutnik i alarm:** PPM lub zasobnik → Minutnik i alarm. Minutnik 1–1440 minut, alarm na najbliższe wystąpienie podanej godziny Windows, drzemka 5 minut, wyciszenie. Jeden aktywny termin; nowy zastępuje poprzedni. Termin przetrwa restart programu. Aplikacja musi działać, aby odtworzyć dźwięk i powiadomienie; nie wybudza komputera. Po wznowieniu lub ponownym uruchomieniu nadrabia przeterminowane przypomnienie.
7. **Diagnostyka:** status, ostatnia próba i powodzenie, termin następnej próby, czas obserwacji i kategoria błędu. Eksport TXT zawiera raport i lokalny log bez miasta, współrzędnych, surowych odpowiedzi API i treści wyjątków. Log jest ograniczony do około 256 KiB plus jednej poprzedniej kopii.

## Skróty klawiaturowe widżetu

| Skrót | Działanie |
|---|---|
| Ctrl+, | Ustawienia |
| Ctrl+F | Prognoza |
| Ctrl+T | Minutnik i alarm |
| Ctrl+D | Diagnostyka |
| Escape | Anulowanie edycji ustawień, poza trwającym zapisem |

## Poprawki po audycie

- .NET 10 LTS; skrypt wydania domyślnie pobiera runtime 10.0.15. Wariant samodzielny zawiera runtime, mniejszy wariant wymaga .NET 10.0.15 Windows Desktop Runtime x64 lub nowszej poprawki 10.0.x.
- Zapis edytora zachowuje aktualne położenie monitora i aktywne przypomnienie. Skala skorygowana przez zmianę ekranu jest zachowywana, jeśli nie została zmieniona w edytorze.
- Cache bez strefy jest odrzucany; widok toleruje brakującą lub nieznaną strefę, stosując UTC.
- Ikona na jasnym tle nie ma podwójnie naliczanej przezroczystości.
- Geokodowanie odrzuca puste nazwy, nieskończone i niepoprawne współrzędne.
- Wspólny parser bieżącej pogody. Walidacja tablic prognozy godzinowej i wartości liczbowych.
- Uporządkowane zapisy ustawień i cache poza wątkiem UI; zamknięcie czeka na końcowy zapis ustawień. Błąd zatwierdzenia wycofuje zmianę autostartu.
- Lokalne logowanie błędów. Nieoczekiwany błąd UI kończy aplikację po komunikacie zamiast kontynuować pracę w nieznanym stanie.
- Pakowanie sprawdza wersję projektu i obu EXE przed tworzeniem archiwów. `-RequireSignature` odrzuca pliki bez ważnego podpisu.

## Budowanie i dystrybucja

```powershell
./scripts/Build.ps1
./scripts/Build.ps1 -FrameworkDependent
./tests/Packaging.Checks.ps1
./scripts/Package.ps1
```

Pliki: `publish/Zegar.exe`, `dist/Zegar-1.5.0-win-x64.zip`, `dist/Zegar-1.5.0-win-x64-framework.zip`, `dist/SHA256SUMS.txt`.

Brak zaufanego certyfikatu wydawcy: EXE pozostaje niepodpisany. Dostępne certyfikaty samopodpisane nie zostały użyte. Po pozyskaniu certyfikatu wykonaj procedurę z `SIGNING.md`, a następnie `./scripts/Package.ps1 -RequireSignature`.

## Weryfikacja i ograniczenia

Testy logiki, WPF, instalatora i zabezpieczeń pakowania są opisane w `../VERIFICATION.md`. Testy WPF pracują na katalogach tymczasowych, atrapach autostartu i HTTP. Tryb `--visual` pokazuje rzeczywiste okna z danymi demonstracyjnymi i izolowanymi ustawieniami.

Fizyczne odłączanie ekranów, zmiana sprzętowego DPI, dostarczenie alarmu po rzeczywistym uśpieniu i autostart po logowaniu nadal wymagają sprawdzenia na docelowym komputerze. Logika tych scenariuszy ma testy automatyczne, które nie zastępują testów sprzętowych.

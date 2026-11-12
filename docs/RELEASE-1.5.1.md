# Zegar HTC 1.5.1 — nowy interfejs, prognoza godzinowa i alarmy

Natywny widżet zegara i pogody dla Windows x64. Wydanie obejmuje nowe funkcje 1.5.0 i poprawiony interfejs 1.5.1.

## Nowości

- Ustawienia w zakładkach Zegar, Pogoda i Wygląd; wspólny styl okien, większe opisy i wektorowe ikony pogody.
- Tryb kompaktowy, ulubione miejscowości i zapisane profile wyglądu.
- Prognoza dzienna i godzinowa dostępna także z cache; jednostki °C/°F oraz km/h, m/s i mph.
- Minutnik i alarm z pięciominutową drzemką oraz zachowaniem terminu między uruchomieniami.
- Diagnostyka i eksport lokalnego raportu bez lokalizacji i treści odpowiedzi API.
- Poprawki zapisu ustawień, obsługi cache, geokodowania i spóźnionych odpowiedzi sieciowych.
- Aktualizacja do .NET 10 LTS; pakiet samodzielny zawiera runtime 10.0.15.

## Pliki do pobrania

- **Zegar.exe** — samodzielny program, bez konieczności instalowania .NET.
- **Zegar-1.5.1-win-x64.zip** — samodzielny program, instrukcja i skrypt dodający skróty oraz autostart.
- **Zegar-1.5.1-win-x64-framework.zip** — mniejszy pakiet wymagający .NET 10.0.15 Windows Desktop Runtime x64 lub nowszej poprawki 10.0.x.
- **SHA256SUMS.txt** — sumy SHA-256 powyższych plików.

Pogoda wymaga Internetu; ostatnie dane pozostają dostępne offline. Alarm wymaga uruchomionej aplikacji i nie wybudza komputera. Po wznowieniu nadrabia przekroczony termin.

EXE nie ma podpisu Authenticode: brak zaufanego certyfikatu wydawcy. Nie użyto certyfikatu samopodpisanego.

## Sprawdzenia

Kompilacja Release: 0 błędów i 0 ostrzeżeń. 104 sprawdzenia: 57 logiki, 34 WPF, 7 instalatora, 3 zabezpieczeń pakowania i 3 integracji z rzeczywistym Open-Meteo.

Fizyczne zmiany monitorów/DPI, alarm po rzeczywistym uśpieniu i autostart po logowaniu pozostają do sprawdzenia na docelowym sprzęcie.

## Skróty

- Ctrl+, — ustawienia
- Ctrl+F — prognoza
- Ctrl+T — minutnik i alarm
- Ctrl+D — diagnostyka

[Instrukcja i kod źródłowy](https://github.com/zetmar-collab/Zegar#readme) · [Szczegóły weryfikacji](https://github.com/zetmar-collab/Zegar/blob/main/VERIFICATION.md) · [Opis interfejsu](https://github.com/zetmar-collab/Zegar/blob/main/docs/UI-REVIEW.md)

# Weryfikacja — 25.10.2026

## Aktualizacja 1.3.0 — 26.10.2026

- Kompilacja i publikacja samodzielnego EXE powiodły się; 21/21 testów lokalnych przechodzi.
- Nowe testy obejmują północ, południe i ostatnią minutę dnia w 12H, format 24H, daty PL/EN, tłumaczenia oraz migrację i zapis preferencji języka i formatu.
- W zainstalowanej aplikacji przełączono ustawienia na English i 12H. Potwierdzono przetłumaczone etykiety, angielski podgląd 03:34 PM oraz zapis `Language: en`, `Use24HourClock: false`.
- Działający widżet pokazał angielską datę, CLOCK / WEATHER, Overcast, Feels like, CELSIUS oraz AM obok daty; czas danych pogodowych miał oznaczenie AM.
- Po sprawdzeniu przywrócono PL i 24H, zachowując pozostałe preferencje użytkownika.

## Aktualizacja 1.2.0

- Kompilacja i publikacja samodzielnego EXE powiodły się; przechodzi 12/12 dotychczasowych testów lokalnych.
- W zainstalowanej aplikacji sprawdzono listę czcionek, wybór Arial, paletę koloru czcionki, biały kolor i podgląd.
- Po zapisaniu potwierdzono `FontColor: #FFFFFF` i `FontFamily: Arial` w ustawieniach, biały tekst widżetu oraz automatycznie przyciemnione karty cyfr.
- Ustawienia otwierają się modalnie, a widżet w tle jest przyciemniany. Escape zamyka okno; zapisane preferencje nie zmieniły się po anulowaniu.
- Dodano skrót Ctrl+, do otwierania ustawień. Okno można powiększać i przewijać.
- Po sprawdzeniu przywrócono domyślną czcionkę i automatyczny kolor. Zachowano bieżące ustawienia użytkownika: Gliwice, niebieskie tło, skalę, przezroczystość, blokadę i położenie. Zaktualizowana aplikacja jest uruchomiona w zwykłym trybie.

## Aktualizacja 1.1.0

- Kompilacja i publikacja po dodaniu wyboru koloru zakończone powodzeniem; 12/12 dotychczasowych testów lokalnych przechodzi.
- W zainstalowanej aplikacji otwarto systemową paletę, wybrano jasny zielony kolor, zatwierdzono i zapisano ustawienia. Tło zmieniło kolor, napisy stały się ciemne, a `BackgroundColor` zapisano w lokalnym JSON.
- Po sprawdzeniu przywrócono poprzedni kolor domyślny i uruchomiono zaktualizowany widżet w zwykłym trybie.

## Wersja 1.0.0

- `dotnet build Zegar.csproj -c Release`: 0 błędów, 0 ostrzeżeń.
- `scripts/Build.ps1`: utworzono samodzielny `publish/Zegar.exe` dla Windows x64.
- Zestaw testowy: 12/12 testów lokalnych, 2/2 integracji z publicznym API Open-Meteo.
- Te same 14 testów przeszło również jako samodzielna aplikacja EXE.
- W działającym oknie Windows potwierdzono wyświetlanie czasu 24 h, polskiej daty i pogody, otwieranie menu PPM, wyszukiwanie miejscowości i zapis współrzędnych.
- Włączenie blokady jest zapamiętywane; próba przeciągnięcia zablokowanego okna nie zmieniła położenia.
- Przełącznik zawsze na wierzchu zapisuje preferencję; po teście przywrócono wartość wyłączoną.
- W zainstalowanym EXE wystąpił przejściowy błąd HTTPS; użycie „Odśwież widżet” przywróciło temperaturę i opis pogody. Zegar działał podczas błędu.
- Skontrolowano poprawiony widok ustawień, w tym dolne opisy i zaznaczony autostart.
- Instalator utworzył skróty na Pulpicie i w menu Start oraz wpis autostartu wskazujący `%LOCALAPPDATA%\Programs\ZegarHTC\Zegar.exe`.
- Uruchomienie `--restart` zastąpiło proces zainstalowanej aplikacji nowym procesem. Końcowo widżet działa w zwykłym trybie, bez przycisku na pasku zadań.

Nie wykonywano wylogowania/restartu Windows. Start podczas następnego logowania oraz zachowanie przy innych skalach DPI i odłączaniu monitorów pozostają do sprawdzenia w tych warunkach.

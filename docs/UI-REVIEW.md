# Przegląd wyglądu — 1.5.1

## Rozpoznane problemy

- Ustawienia tworzyły jeden długi formularz: zegar, pogoda, kolory i profile mieszały się w tej samej przestrzeni.
- Okna pomocnicze miały różne odstępy i przypadkową hierarchię: prognoza była blokiem tekstu, a minutnik listą przycisków.
- Symbole pogodowe zależały od kroju pisma. Małe podpisy oraz niespójne kontrolki utrudniały czytanie.
- Potrzebne były wyraźne stany klawiatury, trwale dostępny zapis i poprawna dostępność zakładek.

## Wprowadzone zmiany

- Trzy zakładki ustawień: **Zegar**, **Pogoda**, **Wygląd**. Zapis i anulowanie są poza przewijaną treścią. Profile znajdują się w rozwijanej sekcji wyglądu.
- Wspólny styl `UiTheme.xaml`: Segoe UI, tekst podstawowy 14, nagłówki 17/28–30, odstępy 8/16/24, jasne karty i niebieski akcent. Bez dodatkowych czcionek i bibliotek.
- Spójne zaokrąglenia, przyciski główne i pomocnicze, reakcje na najechanie i naciśnięcie, widoczny fokus oraz stany wyłączenia. Bez animacji, więc nie ma ruchu do ograniczania przy preferencji reduced motion.
- Podgląd kolorów na początku zakładki Wygląd; czytelne etykiety i zachowanie polskich oraz angielskich tłumaczeń.
- Prognoza dzienna w kartach z wyraźnym zakresem temperatur. Godziny, temperatury, opisy i opady mają wyrównane kolumny.
- Minutnik ma osobną kartę aktywnego terminu, duże odliczanie, oddzielne formularze minutnika i alarmu oraz błędy przy odpowiednim polu. Nieaktywne akcje i puste błędy nie zajmują miejsca.
- Diagnostyka ma nagłówek, czytelniejszy raport i wyróżniony eksport.
- Pogoda używa rysunków wektorowych WPF zamiast glifów symbolicznych. Widżet zachowuje karty klapkowe, własne kolory użytkownika i tryb kompaktowy.
- Okna pomocnicze przełączają zasoby na kolory systemowe w trybie wysokiego kontrastu. Dekoracyjne ikony nie są treścią dla czytnika ekranu; znacząca ikona w trybie kompaktowym ma opis.

## Weryfikacja

- Obejrzano rzeczywisty widżet, ustawienia PL/EN, przełączanie zakładek i okno minutnika na izolowanych ustawieniach.
- Sprawdzono drzewo dostępności zakładek. Szablon zachowuje `PART_SelectedContentHost`, a kolor i pogrubienie aktywnego nagłówka nie przechodzą na formularz.
- Testy sprawdzają dostępność zapisu w małym obszarze 620 × 540, tłumaczenia kategorii oraz poprawne udostępnienie zawartości zakładek.
- Tryb `dotnet run --project tests/UiChecks/Zegar.UiChecks.csproj -c Release -- --capture` zapisuje rendery WPF do `test-results/ui`, bez otwierania okien i bez zmian preferencji użytkownika. `--visual` pozostaje interaktywnym podglądem.
- Pozostają testy na sprzęcie z różnymi DPI i pełna próba z czytnikiem ekranu. Trybu wysokiego kontrastu nie włączano w ustawieniach systemu użytkownika.

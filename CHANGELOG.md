# Historia zmian

## 1.5.1 — 2026-11-12
- Ustawienia podzielone na zakładki Zegar, Pogoda i Wygląd; stały pasek zapisu i rozwijane profile.
- Wspólne style okien pomocniczych, karty, czytelniejsze nagłówki, przyciski i fokus klawiatury.
- Prognoza z wyrównanymi kolumnami i wyróżnionymi temperaturami; nowy układ minutnika i diagnostyki.
- Wektorowe ilustracje pogody, większe opisy na widżecie i zasoby wysokiego kontrastu w oknach pomocniczych.
- Testy dostępności zakładek, małego okna i tłumaczeń oraz generowanie podglądów bez otwierania okien.

## 1.5.0 — 2026-11-12
- Aktualizacja do .NET 10 LTS; wydanie zawiera runtime 10.0.15.
- Naprawy po audycie: zachowanie geometrii podczas zapisu edytora, walidacja cache i geokodowania, jednokrotna przezroczystość ikony.
- Tryb kompaktowy, ulubione miejscowości, profile wyglądu i wybór jednostek.
- Prognoza na najbliższe 24 godziny z cache i godzinami w strefie miejscowości.
- Minutnik 1–1440 minut, alarm, drzemka, wyciszenie i trwały termin.
- Panel diagnostyczny, eksport raportu i ograniczony lokalny log bez treści wyjątków.
- Kolejka zapisów poza wątkiem UI, wspólny parser i ochrona przed spóźnionymi odpowiedziami API.
- Weryfikacja wersji EXE i opcjonalnie podpisu przed pakowaniem; rozbudowane CI.
- Rozszerzone testy oraz izolowany interaktywny podgląd `--visual`.
- EXE pozostaje niepodpisany — brak zaufanego certyfikatu wydawcy.

## 1.4.0 — 2026-10-30
- Naprawa harmonogramu automatycznego odświeżania: aktualizacja statusu zegara nie odsuwa już pobierania pogody.
- Zachowanie danych i komunikatu offline przy zmianach wyglądu, blokady i zawsze na wierzchu; ponowienie po 2 minutach.
- Trwały cache pogody osobno dla współrzędnych, dostępny również po ponownym uruchomieniu.
- Prognoza na 3 dni: temperatura minimalna i maksymalna, szansa opadów, wschód i zachód słońca w strefie miejscowości.
- Transakcyjny zapis ustawień i autostartu, kopia `.bak`, odzyskiwanie i zachowanie uszkodzonego pliku; błąd zapisu pozostawia edytor otwarty.
- Instalator zachowuje wybór autostartu podczas aktualizacji; opcje jawnego włączenia i wyłączenia.
- Osobna widoczność tła i tekstu, większe podpisy, podgląd gradientu i ostrzeżenie o niskim kontraście.
- Obsługa zmian monitorów i DPI, pozycje w pikselach fizycznych, przywołanie działającego okna przy drugim uruchomieniu.
- Poprawione nocne i nieznane symbole pogody, numer wersji w ustawieniach.
- Własna ikona programu w 8 rozmiarach, osadzona w EXE, oknach i zasobniku.
- Testy regresji, workflow Windows CI, mniejszy wariant wymagający .NET i skrypty pakowania oraz podpisywania.
- Podpis wydawcy nie został dodany: przygotowano obsługę przyszłego certyfikatu.

## 1.3.0 — 2026-10-26
- Język polski i angielski: ustawienia, menu, zasobnik, daty, pogoda i komunikaty.
- Zegar 12H z AM/PM lub 24H; format dotyczy również czasu danych pogodowych.
- Podgląd języka i formatu w ustawieniach, trwały zapis oraz zgodność ze starszymi ustawieniami.
- Testy konwersji północy, południa, dat i zapisywania nowych preferencji.

## 1.2.0 — 2026-10-25
- Wybór zainstalowanej czcionki oraz koloru napisów i cyfr, z podglądem w ustawieniach.
- Tryb automatycznego koloru i przywracanie domyślnej czcionki HTC.
- Modalne, skalowalne okno ustawień; nieaktywny widżet jest przyciemniony podczas edycji.
- Dopasowanie cyfr do kart oraz kontrast kart przy wyborze jasnej czcionki.

## 1.1.0 — 2026-10-25
- Wybór dowolnego koloru tła w ustawieniach, próbka koloru i powrót do domyślnego.
- Automatyczny dobór jasnych lub ciemnych napisów i zapamiętywanie koloru.

## 1.0.0 — 2026-10-25
- Widżet Windows w stylu klapkowego zegara HTC, czas 24 h i polska data.
- Aktualna pogoda Open-Meteo i wyszukiwanie miejscowości.
- Menu kontekstowe: ustawienia, blokada położenia, zawsze na wierzchu, odświeżanie i restart.
- Autostart, regulacja rozmiaru i przezroczystości, zapamiętywanie ustawień.
- Skróty instalacyjne, ikona zasobnika i odzyskiwanie przez osobny skrót restartu.

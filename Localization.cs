using System;
using System.Collections.Generic;
using System.Globalization;

namespace Zegar;

public static class UiText
{
    public static CultureInfo Culture(string language) => CultureInfo.GetCultureInfo(language == "en" ? "en-GB" : "pl-PL");
    public static string Get(string polish, string language) => language == "en" && English.TryGetValue(polish, out var text) ? text : polish;
    private static readonly Dictionary<string, string> English = new()
    {
        ["Prognoza…"] = "Forecast…",
        ["Prognoza na 3 dni"] = "3-day forecast",
        ["Opady"] = "Rain chance",
        ["Wschód"] = "Sunrise",
        ["Zachód"] = "Sunset",
        ["Czasy w strefie miejscowości"] = "Times in the location's time zone",
        ["Prognoza niedostępna. Spróbuj odświeżyć pogodę."] = "Forecast unavailable. Try refreshing the weather.",
        ["Zapisane dane · "] = "Cached data · ",
        ["Przezroczystość tła"] = "Background opacity",
        ["Widoczność tekstu"] = "Text opacity",
        ["Niski kontrast tekstu — wybierz jaśniejszy lub ciemniejszy kolor."] = "Low text contrast — choose a lighter or darker colour.",
        ["Przywrócono ustawienia z kopii zapasowej."] = "Settings were restored from a backup.",
        ["Nie udało się odczytać ustawień. Uszkodzony plik zostanie zachowany."] = "Settings could not be read. The damaged file will be preserved.",
        ["Nie udało się zapisać pamięci pogody."] = "Could not save the weather cache.",
        ["Zegar już działa, ale nie odpowiada. Użyj skrótu Restart zegara HTC."] = "The clock is running but not responding. Use the Restart zegara HTC shortcut.",
        ["Zegar HTC"] = "HTC Clock",
        ["Ustawienia zegara"] = "Clock settings",
        ["Twój zegar"] = "Your clock",
        ["Miejsce, wygląd i uruchamianie"] = "Location, appearance and startup",
        ["Język / Language"] = "Language / Język",
        ["Format zegara"] = "Clock format",
        ["POGODA DLA MIEJSCOWOŚCI"] = "WEATHER LOCATION",
        ["Szukaj"] = "Search",
        ["Nazwa miejscowości"] = "City name",
        ["Wyniki wyszukiwania miejscowości"] = "City search results",
        ["Wpisz nazwę i wybierz miejscowość z listy."] = "Enter a name and select a location from the list.",
        ["Kolor widżetu"] = "Widget colour",
        ["Wybierz kolor…"] = "Choose colour…",
        ["Domyślny"] = "Default",
        ["Czcionka widżetu"] = "Widget font",
        ["Krój czcionki widżetu"] = "Widget font family",
        ["Domyślna (HTC)"] = "Default (HTC)",
        ["Kolor czcionki (cyfry, data i pogoda)"] = "Font colour (digits, date and weather)",
        ["Kolor czcionki…"] = "Font colour…",
        ["Automatyczny"] = "Automatic",
        ["Rozmiar widżetu"] = "Widget size",
        ["Rozmiar widżetu w procentach"] = "Widget size in percent",
        ["Przezroczystość"] = "Opacity",
        ["Widoczność widżetu w procentach"] = "Widget opacity in percent",
        ["widoczności"] = "opacity",
        ["Odświeżaj pogodę co"] = "Refresh weather every",
        ["Częstotliwość odświeżania"] = "Refresh interval",
        ["Uruchamiaj razem z systemem Windows"] = "Start with Windows",
        ["Zawsze na wierzchu"] = "Always on top",
        ["Zablokuj położenie widżetu"] = "Lock widget position",
        ["Czas i data: zgodnie z zegarem systemowym.\nPogoda: Open-Meteo, °C. Nie wymaga klucza API."] = "Time and date follow your system clock.\nWeather: Open-Meteo, °C. No API key required.",
        ["Anuluj"] = "Cancel",
        ["Zapisz ustawienia"] = "Save settings",
        ["Wpisz przynajmniej 2 znaki."] = "Enter at least 2 characters.",
        ["Wyszukiwanie w Open-Meteo…"] = "Searching Open-Meteo…",
        ["Brak wyników. Spróbuj innej nazwy."] = "No results. Try another name.",
        ["Wybierz właściwą miejscowość i zapisz ustawienia."] = "Select the correct location and save settings.",
        ["Nie udało się wyszukać. Sprawdź połączenie i spróbuj ponownie."] = "Search failed. Check your connection and try again.",
        ["Kliknij Szukaj, aby wybrać miejscowość."] = "Click Search to select a location.",
        ["Wyszukaj miejscowość i wybierz wynik z listy."] = "Search for a location and select a result from the list.",
        ["Nie udało się zmienić autostartu: "] = "Could not change startup settings: ",
        ["ZEGAR / POGODA"] = "CLOCK / WEATHER",
        ["CELSJUSZA"] = "CELSIUS",
        ["POŁOŻENIE ZABLOKOWANE"] = "POSITION LOCKED",
        ["Ustawienia…"] = "Settings…",
        ["Zablokuj położenie"] = "Lock position",
        ["Odśwież widżet"] = "Refresh widget",
        ["Uruchom ponownie"] = "Restart",
        ["Zamknij"] = "Exit",
        ["Pokaż zegar"] = "Show clock",
        ["Odśwież"] = "Refresh",
        ["Zegar HTC • prawy przycisk: menu"] = "HTC Clock • right-click for menu",
        ["Aktualizowanie…"] = "Updating…",
        ["Pobieranie pogody…"] = "Loading weather…",
        ["Łączenie…"] = "Connecting…",
        ["Odcz."] = "Feels like",
        ["wiatr"] = "wind",
        ["Czas danych z Open-Meteo: "] = "Open-Meteo observation time: ",
        ["Dane z "] = "As of ",
        ["Starsze dane · "] = "Older data · ",
        ["Brak połączenia · spróbuję ponownie"] = "Offline · will retry",
        ["Brak połączenia · ostatnie dane"] = "Offline · last known weather",
        ["Nie udało się pobrać pogody. "] = "Could not load weather. ",
        ["Pogoda niedostępna"] = "Weather unavailable",
        ["PPM → Odśwież widżet"] = "Right-click → Refresh widget",
        ["Nie udało się zapisać ustawień"] = "Could not save settings",
        ["Nie udało się wykonać operacji. "] = "The operation could not be completed. ",
        ["Bezchmurnie"] = "Clear sky",
        ["Przeważnie słonecznie"] = "Mainly clear",
        ["Częściowe zachmurzenie"] = "Partly cloudy",
        ["Pochmurno"] = "Overcast",
        ["Mgła"] = "Fog",
        ["Mżawka"] = "Drizzle",
        ["Marznąca mżawka"] = "Freezing drizzle",
        ["Deszcz"] = "Rain",
        ["Marznący deszcz"] = "Freezing rain",
        ["Śnieg"] = "Snow",
        ["Przelotny deszcz"] = "Rain showers",
        ["Przelotny śnieg"] = "Snow showers",
        ["Burza"] = "Thunderstorm",
        ["Brak opisu"] = "Unknown conditions"
    };
}

public static class ClockFormat
{
    public static string Hour(DateTime time, bool use24HourClock) => time.ToString(use24HourClock ? "HH" : "hh", CultureInfo.InvariantCulture);
    public static string Period(DateTime time, bool use24HourClock) => use24HourClock ? "" : time.ToString("tt", CultureInfo.InvariantCulture);
    public static string Time(DateTime time, bool use24HourClock) => time.ToString(use24HourClock ? "HH:mm" : "hh:mm tt", CultureInfo.InvariantCulture);
    public static string Date(DateTime time, string language) => time.ToString("dddd, d MMMM yyyy", UiText.Culture(language));
}

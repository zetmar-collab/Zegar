using System.Net;
using Zegar;

int checks = 0;
void Check(bool condition, string name)
{ if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); checks++; }

var folder = Path.Combine(Path.GetTempPath(), "ZegarChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    var path = Path.Combine(folder, "settings.json");
    var store = new SettingsStore(path);
    Check(store.Load().City == "Warszawa", "Brak pliku ustawień: wartości domyślne");
    File.WriteAllText(path, "{uszkodzony json");
    Check(store.Load().Scale == 1, "Uszkodzone ustawienia: bezpieczny start");
    var settings = new WidgetSettings { City = "Łódź", Scale = 1.25, Locked = true, Topmost = true, Left = -1200, RefreshMinutes = 30 };
    store.Save(settings);
    var loaded = store.Load();
    Check(loaded.City == "Łódź" && loaded.Scale == 1.25 && loaded.Locked && loaded.Topmost && loaded.Left == -1200 && loaded.RefreshMinutes == 30, "Trwałość ustawień i polskich znaków");
    loaded.Scale = double.NaN; loaded.Opacity = -5; loaded.Latitude = 999; loaded.RefreshMinutes = 0; loaded.Normalize();
    Check(loaded.Scale == 1 && loaded.Opacity == .4 && loaded.Latitude == 52.2297 && loaded.RefreshMinutes == 5, "Walidacja zakresów ustawień");

    var handler = new StubHandler();
    using var client = new HttpClient(handler);
    var service = new WeatherService(client);
    handler.Body = """{"current":{"temperature_2m":-4.5,"apparent_temperature":-8.1,"weather_code":73,"is_day":0,"wind_speed_10m":12.3,"time":1792886400}}""";
    var oldCulture = System.Globalization.CultureInfo.CurrentCulture;
    System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");
    var weather = await service.GetAsync(52.2297, 21.0122, default);
    System.Globalization.CultureInfo.CurrentCulture = oldCulture;
    Check(weather.Temperature == -4.5 && !weather.IsDay && weather.Code == 73, "Parsowanie temperatur ujemnych i nocy");
    Check(handler.LastUri!.Contains("latitude=52.2297") && handler.LastUri.Contains("timeformat=unixtime"), "API: współrzędne niezależne od języka systemu i czas Unix");
    handler.Body = """{"results":[{"name":"Łódź","latitude":51.75,"longitude":19.46,"country":"Polska"}]}""";
    var places = await service.SearchAsync("Łódź & centrum", default);
    Check(places.Count == 1 && places[0].Region == "" && handler.LastUri!.Contains("%26"), "Geokodowanie: opcjonalny region i kodowanie zapytania");
    handler.Body = "{}";
    Check((await service.SearchAsync("nieistniejące", default)).Count == 0, "Puste wyniki wyszukiwania");
    handler.Status = HttpStatusCode.ServiceUnavailable;
    bool failed = false;
    try { await service.GetAsync(0, 0, default); } catch (HttpRequestException) { failed = true; }
    Check(failed, "Błąd HTTP trafia do obsługi braku połączenia");
    handler.Status = HttpStatusCode.OK; handler.Body = "nie json";
    failed = false;
    try { await service.GetAsync(0, 0, default); } catch (System.Text.Json.JsonException) { failed = true; }
    Check(failed, "Nieprawidłowa odpowiedź jest odrzucana");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    failed = false;
    try { await service.GetAsync(0, 0, cancel.Token); } catch (OperationCanceledException) { failed = true; }
    Check(failed, "Anulowanie poprzedniego odświeżania");
    Check(WeatherService.Description(66) == "Marznący deszcz" && WeatherService.Description(99) == "Burza" && WeatherService.Symbol(0, false) == "☾", "Kody WMO i ikona nocna");

    if (args.Contains("--live"))
    {
        using var liveHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var live = new WeatherService(liveHttp);
        var cities = await live.SearchAsync("Warszawa", default);
        Check(cities.Count > 0 && cities[0].Name.Contains("Warszawa"), "LIVE: wyszukiwanie Open-Meteo");
        var current = await live.GetAsync(cities[0].Latitude, cities[0].Longitude, default);
        Check(double.IsFinite(current.Temperature) && (DateTimeOffset.UtcNow - current.ObservedAt).Duration() < TimeSpan.FromHours(3), "LIVE: aktualne dane pogodowe Open-Meteo");
        Console.WriteLine($"LIVE: {cities[0].Name}: {current.Temperature}°C, {WeatherService.Description(current.Code)}, {current.ObservedAt:O}");
    }
    Console.WriteLine($"Wynik: {checks} testów OK.");
}
finally { Directory.Delete(folder, true); }

sealed class StubHandler : HttpMessageHandler
{
    public string Body { get; set; } = "{}";
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string? LastUri { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastUri = request.RequestUri!.AbsoluteUri;
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
    }
}

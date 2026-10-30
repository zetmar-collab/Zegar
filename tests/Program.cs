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

    var midnight = new DateTime(2026, 10, 26, 0, 0, 0);
    Check(ClockFormat.Time(midnight, false) == "12:00 AM" && ClockFormat.Hour(midnight, false) == "12", "12H: północ to 12 AM");
    Check(ClockFormat.Time(midnight.AddHours(12), false) == "12:00 PM", "12H: południe to 12 PM");
    Check(ClockFormat.Time(midnight.AddHours(23).AddMinutes(59), false) == "11:59 PM", "12H: ostatnia minuta dnia");
    Check(ClockFormat.Time(midnight, true) == "00:00" && ClockFormat.Period(midnight, true) == "" && ClockFormat.Hour(midnight.AddHours(23), true) == "23", "24H: północ i brak AM/PM");
    Check(ClockFormat.Date(midnight, "en") == "Monday, 26 October 2026" && ClockFormat.Date(midnight, "pl") == "poniedziałek, 26 października 2026", "Daty PL i EN niezależne od języka Windows");
    Check(UiText.Get("Deszcz", "en") == "Rain" && UiText.Get("Ustawienia…", "en") == "Settings…" && UiText.Get("Deszcz", "pl") == "Deszcz", "Tłumaczenia pogody i menu");
    File.WriteAllText(path, """{"City":"Gliwice","Scale":0.85}""");
    Check(store.Load().Language == "pl" && store.Load().Use24HourClock && store.Load().City == "Gliwice", "Migracja starszych ustawień: domyślnie PL i 24H");
    loaded.Language = "en"; loaded.Use24HourClock = false; store.Save(loaded);
    Check(store.Load().Language == "en" && !store.Load().Use24HourClock, "Zapamiętywanie EN i 12H");
    loaded.Language = "invalid"; loaded.Normalize();
    Check(loaded.Language == "pl", "Nieznany język wraca do PL");

    Check(WeatherService.Symbol(2,false) != WeatherService.Symbol(2,true) && WeatherService.Symbol(-1,true) == "?", "Nocne zachmurzenie i neutralna ikona nieznanej pogody");
    store.Save(new WidgetSettings { City = "Poprzednie" }); store.Save(new WidgetSettings { City = "Nowsze" });
    File.WriteAllText(path,"{bad");
    Check(store.Load().City == "Poprzednie" && store.RecoveredFromBackup, "Odzyskanie poprawnej kopii ustawień");
    store.Save(new WidgetSettings { City = "Odzyskane" });
    Check(Directory.GetFiles(folder,"settings.json.corrupt-*").Length > 0 && File.ReadAllText(path+".bak").Contains("Poprzednie"), "Uszkodzony plik zachowany bez nadpisania dobrej kopii");
    var transactionFile=Path.Combine(folder,"blocked"); Directory.CreateDirectory(transactionFile);
    bool external=false, rolledBack=false;
    failed=false;
    try { new SettingsStore(transactionFile).Commit(new(),()=>external=true,()=>{external=false;rolledBack=true;}); } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {failed=true;}
    Check(failed && rolledBack && !external,"Nieudany zapis wycofuje zmianę zewnętrzną");
    external=false;
    failed=false;
    try { store.Commit(new WidgetSettings { City="Nie zapisuj" },()=>throw new IOException("registry simulation"),()=>external=true); } catch(IOException) {failed=true;}
    Check(failed && store.Load().City=="Odzyskane" && !external,"Nieudana zmiana autostartu nie zmienia pliku ustawień");

    var snapshot=new WeatherSnapshot(50,19,new Weather(8,5,3,false,10,DateTimeOffset.UtcNow),new(),"Europe/Warsaw",DateTimeOffset.UtcNow);
    var state=new WeatherState();state.Complete(snapshot);state.Fail("offline");
    Check(state.Status("en",false,DateTimeOffset.UtcNow).StartsWith("Offline") && state.Snapshot==snapshot && state.RefreshDelay(15)==TimeSpan.FromMinutes(2),"Offline zachowuje pogodę i ponawia po 2 minutach");
    state.SelectLocation(50,19);
    Check(state.Offline && state.Snapshot==snapshot,"Ta sama lokalizacja nie usuwa pogody ani stanu offline");
    state.SelectLocation(51,19);
    Check(state.Snapshot==null,"Zmiana miasta usuwa dane poprzedniej lokalizacji");
    state.LoadCache(snapshot);
    Check(state.Status("en",true,DateTimeOffset.UtcNow).StartsWith("Cached"),"Oznaczenie danych z pamięci dyskowej");
    state.Complete(snapshot with {Current=snapshot.Current with {ObservedAt=DateTimeOffset.UtcNow.AddHours(-3)}});
    Check(state.Status("pl",true,DateTimeOffset.UtcNow).StartsWith("Starsze dane"),"Wiek danych nie zależy od powodzenia połączenia");
    var cache=new WeatherCacheStore(Path.Combine(folder,"cache"));cache.Save(snapshot);
    Check(cache.Load(50,19)?.Current.Temperature==8 && cache.Load(51,19)==null,"Cache dyskowy jest przypisany do współrzędnych");
    foreach(var file in Directory.GetFiles(Path.Combine(folder,"cache"))) File.WriteAllText(file,"bad");
    Check(cache.Load(50,19)==null,"Uszkodzony cache nie blokuje uruchomienia");
    handler.Status=HttpStatusCode.OK;
    handler.Body="""{"timezone":"Europe/Warsaw","current":{"temperature_2m":8,"apparent_temperature":5,"weather_code":3,"is_day":0,"wind_speed_10m":10,"time":1792886400},"daily":{"time":[1792886400],"temperature_2m_min":[3],"temperature_2m_max":[12],"precipitation_probability_max":[null],"weather_code":[2],"sunrise":[null],"sunset":[1792926400]}}""";
    var forecast=await service.GetSnapshotAsync(50,19,default);
    Check(forecast.Forecast.Count==1 && forecast.Forecast[0].Minimum==3 && forecast.Forecast[0].PrecipitationProbability==null && forecast.Forecast[0].Sunrise==null,"Prognoza: zakres temperatur i brakujące opady/wschód");
    handler.Body=handler.Body.Replace("\"temperature_2m_min\":[3]","\"temperature_2m_min\":[]");failed=false;
    try {await service.GetSnapshotAsync(50,19,default);}catch(System.Text.Json.JsonException){failed=true;}
    Check(failed,"Odrzucenie niespójnych tablic prognozy");

    if (args.Contains("--live"))
    {
        using var liveHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var live = new WeatherService(liveHttp);
        var cities = await live.SearchAsync("Warszawa", default);
        Check(cities.Count > 0 && cities[0].Name.Contains("Warszawa"), "LIVE: wyszukiwanie Open-Meteo");
        var current = await live.GetAsync(cities[0].Latitude, cities[0].Longitude, default);
        Check(double.IsFinite(current.Temperature) && (DateTimeOffset.UtcNow - current.ObservedAt).Duration() < TimeSpan.FromHours(3), "LIVE: aktualne dane pogodowe Open-Meteo");
        Console.WriteLine($"LIVE: {cities[0].Name}: {current.Temperature}°C, {WeatherService.Description(current.Code)}, {current.ObservedAt:O}");
        var liveForecast=await live.GetSnapshotAsync(cities[0].Latitude,cities[0].Longitude,default);
        Check(liveForecast.Forecast.Count==3 && liveForecast.Forecast.All(day=>day.Minimum<=day.Maximum),"LIVE: 3-dniowa prognoza Open-Meteo");
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

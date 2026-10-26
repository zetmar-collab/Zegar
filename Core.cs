using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Zegar;

public sealed class WidgetSettings
{
    public string City { get; set; } = "Warszawa";
    public double Latitude { get; set; } = 52.2297;
    public double Longitude { get; set; } = 21.0122;
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
    public double Scale { get; set; } = 1;
    public double Opacity { get; set; } = 1;
    public string BackgroundColor { get; set; } = "#171E2B";
    public string FontColor { get; set; } = "";
    public string FontFamily { get; set; } = "";
    public string Language { get; set; } = "pl";
    public bool Use24HourClock { get; set; } = true;
    public bool Locked { get; set; }
    public bool Topmost { get; set; }
    public int RefreshMinutes { get; set; } = 15;
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(City)) City = "Warszawa";
        if (!double.IsFinite(Latitude) || Latitude < -90 || Latitude > 90) Latitude = 52.2297;
        if (!double.IsFinite(Longitude) || Longitude < -180 || Longitude > 180) Longitude = 21.0122;
        if (!double.IsFinite(Left)) Left = 80;
        if (!double.IsFinite(Top)) Top = 80;
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, .65, 1.6) : 1;
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, .4, 1) : 1;
        RefreshMinutes = Math.Clamp(RefreshMinutes, 5, 120);
        if (!IsRgb(BackgroundColor)) BackgroundColor = "#171E2B";
        if (!IsRgb(FontColor)) FontColor = "";
        if (string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 200) FontFamily = "";
        if (Language != "en") Language = "pl";
    }
    private static bool IsRgb(string? value) => value != null && value.Length == 7 && value[0] == '#' &&
        int.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
}

public sealed class SettingsStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZegarHTC");
    private readonly string path;
    public SettingsStore(string? path = null) => this.path = path ?? Path.Combine(Folder, "settings.json");
    public WidgetSettings Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(path)) ?? new();
            value.Normalize(); return value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(WidgetSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}

public sealed record Place(string Name, string Country, string Region, double Latitude, double Longitude)
{
    public override string ToString() => string.Join(" · ", new[] { Name, Region, Country });
}
public sealed record Weather(double Temperature, double FeelsLike, int Code, bool IsDay, double Wind, DateTimeOffset ObservedAt);

public sealed class WeatherService
{
    private readonly HttpClient http;
    public WeatherService(HttpClient http) => this.http = http;
    public async Task<List<Place>> SearchAsync(string name, CancellationToken cancellation, string language = "pl")
    {
        using var response = await http.GetAsync("https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(name) + "&count=8&language=" + (language == "en" ? "en" : "pl") + "&format=json", cancellation);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        var places = new List<Place>();
        if (json.RootElement.TryGetProperty("results", out var results))
            foreach (var item in results.EnumerateArray())
                places.Add(new Place(item.GetProperty("name").GetString()!, Text(item, "country"), Text(item, "admin1"), item.GetProperty("latitude").GetDouble(), item.GetProperty("longitude").GetDouble()));
        return places;
    }
    private static string Text(JsonElement item, string key) => item.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
    public async Task<Weather> GetAsync(double latitude, double longitude, CancellationToken cancellation)
    {
        string url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,apparent_temperature,weather_code,is_day,wind_speed_10m&temperature_unit=celsius&wind_speed_unit=kmh&timeformat=unixtime&timezone=auto&forecast_days=1");
        using var response = await http.GetAsync(url, cancellation);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        var current = json.RootElement.GetProperty("current");
        return new Weather(current.GetProperty("temperature_2m").GetDouble(), current.GetProperty("apparent_temperature").GetDouble(), current.GetProperty("weather_code").GetInt32(), current.GetProperty("is_day").GetInt32() == 1, current.GetProperty("wind_speed_10m").GetDouble(), DateTimeOffset.FromUnixTimeSeconds(current.GetProperty("time").GetInt64()));
    }
    public static string Description(int code) => code switch
    {
        0 => "Bezchmurnie", 1 => "Przeważnie słonecznie", 2 => "Częściowe zachmurzenie", 3 => "Pochmurno",
        45 or 48 => "Mgła", 51 or 53 or 55 => "Mżawka", 56 or 57 => "Marznąca mżawka",
        61 or 63 or 65 => "Deszcz", 66 or 67 => "Marznący deszcz", 71 or 73 or 75 or 77 => "Śnieg",
        80 or 81 or 82 => "Przelotny deszcz", 85 or 86 => "Przelotny śnieg", 95 or 96 or 99 => "Burza", _ => "Brak opisu"
    };
    public static string Symbol(int code, bool day) => code switch
    {
        0 or 1 => day ? "☀" : "☾", 2 => "⛅", 3 => "☁", 45 or 48 => "≋",
        >= 71 and <= 77 or 85 or 86 => "❄", >= 95 => "ϟ", _ => "☂"
    };
}

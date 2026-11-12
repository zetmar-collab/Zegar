using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Zegar;

public interface IStartupSettings
{
    string? Command { get; }
    bool Enabled { get; }
    void Set(bool enabled);
    void Restore(string? command);
}

public sealed class WidgetSettings
{
    public string City { get; set; } = "Warszawa";
    public double Latitude { get; set; } = 52.2297;
    public double Longitude { get; set; } = 21.0122;
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
    public double Scale { get; set; } = 1;
    public double Opacity { get; set; } = 1;
    public double TextOpacity { get; set; } = 1;
    public double? PhysicalLeft { get; set; }
    public double? PhysicalTop { get; set; }
    public string BackgroundColor { get; set; } = "#171E2B";
    public string FontColor { get; set; } = "";
    public string FontFamily { get; set; } = "";
    public string Language { get; set; } = "pl";
    public bool Use24HourClock { get; set; } = true;
    public bool Locked { get; set; }
    public bool Topmost { get; set; }
    public int RefreshMinutes { get; set; } = 15;
    public bool Compact { get; set; }
    public string TemperatureUnit { get; set; } = "C";
    public string WindUnit { get; set; } = "kmh";
    public List<Place> Favorites { get; set; } = new();
    public List<AppearanceProfile> Profiles { get; set; } = new();
    public ReminderState Reminder { get; set; } = new();
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(City)) City = "Warszawa";
        if (!double.IsFinite(Latitude) || Latitude < -90 || Latitude > 90) Latitude = 52.2297;
        if (!double.IsFinite(Longitude) || Longitude < -180 || Longitude > 180) Longitude = 21.0122;
        if (!double.IsFinite(Left)) Left = 80;
        if (!double.IsFinite(Top)) Top = 80;
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, .65, 1.6) : 1;
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, .4, 1) : 1;
        TextOpacity = double.IsFinite(TextOpacity) ? Math.Clamp(TextOpacity, .4, 1) : 1;
        if (PhysicalLeft is not double x || PhysicalTop is not double y || !double.IsFinite(x) || !double.IsFinite(y))
        { PhysicalLeft = PhysicalTop = null; }
        RefreshMinutes = Math.Clamp(RefreshMinutes, 5, 120);
        if (!IsRgb(BackgroundColor)) BackgroundColor = "#171E2B";
        if (!IsRgb(FontColor)) FontColor = "";
        if (string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 200) FontFamily = "";
        if (Language != "en") Language = "pl";
        TemperatureUnit = TemperatureUnit == "F" ? "F" : "C";
        WindUnit = WindUnit is "ms" or "mph" ? WindUnit : "kmh";
        Favorites = (Favorites ?? new()).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name) && WeatherService.ValidCoordinates(p.Latitude,p.Longitude)).DistinctBy(p => (Math.Round(p.Latitude,5),Math.Round(p.Longitude,5))).Take(30).ToList();
        Profiles = (Profiles ?? new()).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name)).DistinctBy(p => p.Name.Trim(),StringComparer.OrdinalIgnoreCase).Take(20).Select(p => p.Normalized()).ToList();
        Reminder ??= new(); Reminder.Normalize();
    }
    private static bool IsRgb(string? value) => value != null && value.Length == 7 && value[0] == '#' &&
        int.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
    public WidgetSettings Clone()
    {
        var result = (WidgetSettings)MemberwiseClone();
        result.Favorites = new(Favorites ?? new()); result.Profiles = new(Profiles ?? new()); result.Reminder = (Reminder ?? new()).Clone();
        return result;
    }
}

public sealed class SettingsStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZegarHTC");
    private readonly string path;
    private readonly SemaphoreSlim writes = new(1,1);
    public bool RecoveredFromBackup { get; private set; }
    public bool LoadFailed { get; private set; }
    public SettingsStore(string? path = null) => this.path = path ?? Path.Combine(Folder, "settings.json");
    public WidgetSettings Load()
    {
        RecoveredFromBackup = LoadFailed = false;
        if (TryLoad(path, out var settings)) return settings!;
        LoadFailed = File.Exists(path);
        if (TryLoad(path + ".bak", out settings)) { RecoveredFromBackup = true; return settings!; }
        return new();
    }
    private static bool TryLoad(string file, out WidgetSettings? settings)
    {
        settings = null;
        try { settings = JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(file)); settings?.Normalize(); return settings != null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }
    public void Save(WidgetSettings settings) => Commit(settings, () => { }, () => { });
    public void Commit(WidgetSettings settings, Action applyExternal, Action rollbackExternal)
    {
        writes.Wait();
        try { CommitCore(settings,applyExternal,rollbackExternal); }
        finally { writes.Release(); }
    }
    public async Task CommitAsync(WidgetSettings settings, Action applyExternal, Action rollbackExternal)
    {
        var captured=settings.Clone();
        await writes.WaitAsync().ConfigureAwait(false);
        try { await Task.Run(()=>CommitCore(captured,applyExternal,rollbackExternal)).ConfigureAwait(false); }
        finally { writes.Release(); }
    }
    public Task SaveAsync(WidgetSettings settings) => CommitAsync(settings,()=>{},()=>{});
    private void CommitCore(WidgetSettings settings, Action applyExternal, Action rollbackExternal)
    {
        var normalized = settings.Clone(); normalized.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool externalApplied = false;
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true }));
            bool exists = File.Exists(path);
            bool valid = exists && TryLoad(path, out _);
            // Preserve damaged input and never replace a good backup with damaged JSON.
            if (exists && !valid) File.Copy(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
            applyExternal(); externalApplied = true;
            if (valid) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path, true);
            LoadFailed = RecoveredFromBackup = false;
        }
        catch
        {
            if (externalApplied) rollbackExternal();
            throw;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
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
            {
                string placeName = Text(item, "name");
                double latitude = item.GetProperty("latitude").GetDouble(), longitude = item.GetProperty("longitude").GetDouble();
                if (string.IsNullOrWhiteSpace(placeName) || !ValidCoordinates(latitude, longitude)) continue;
                places.Add(new Place(placeName, Text(item, "country"), Text(item, "admin1"), latitude, longitude));
            }
        return places;
    }
    private static string Text(JsonElement item, string key) => item.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
    public static bool ValidCoordinates(double latitude, double longitude) => double.IsFinite(latitude) && double.IsFinite(longitude) && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    private static Weather ParseCurrent(JsonElement current)
    {
        var result = new Weather(current.GetProperty("temperature_2m").GetDouble(), current.GetProperty("apparent_temperature").GetDouble(), current.GetProperty("weather_code").GetInt32(), current.GetProperty("is_day").GetInt32() == 1, current.GetProperty("wind_speed_10m").GetDouble(), DateTimeOffset.FromUnixTimeSeconds(current.GetProperty("time").GetInt64()));
        if (!double.IsFinite(result.Temperature) || !double.IsFinite(result.FeelsLike) || !double.IsFinite(result.Wind) || result.Wind < 0) throw new JsonException("Invalid weather values.");
        return result;
    }
    public async Task<Weather> GetAsync(double latitude, double longitude, CancellationToken cancellation)
    {
        string url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,apparent_temperature,weather_code,is_day,wind_speed_10m&temperature_unit=celsius&wind_speed_unit=kmh&timeformat=unixtime&timezone=auto&forecast_days=1");
        using var response = await http.GetAsync(url, cancellation);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        var current = json.RootElement.GetProperty("current");
        return ParseCurrent(current);
    }
    public async Task<WeatherSnapshot> GetSnapshotAsync(double latitude, double longitude, CancellationToken cancellation)
    {
        if (!ValidCoordinates(latitude,longitude)) throw new ArgumentOutOfRangeException(nameof(latitude));
        string url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,apparent_temperature,weather_code,is_day,wind_speed_10m&daily=temperature_2m_min,temperature_2m_max,precipitation_probability_max,weather_code,sunrise,sunset&hourly=temperature_2m,precipitation_probability,weather_code,wind_speed_10m&temperature_unit=celsius&wind_speed_unit=kmh&timeformat=unixtime&timezone=auto&forecast_days=3");
        using var response = await http.GetAsync(url, cancellation);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        var root = json.RootElement;
        var current = root.GetProperty("current");
        var weather = ParseCurrent(current);
        var days = new List<ForecastDay>();
        if (root.TryGetProperty("daily", out var daily))
        {
            var times = daily.GetProperty("time");
            string[] fields = { "temperature_2m_min", "temperature_2m_max", "precipitation_probability_max", "weather_code", "sunrise", "sunset" };
            foreach (string field in fields)
                if (daily.GetProperty(field).GetArrayLength() != times.GetArrayLength()) throw new JsonException("Inconsistent forecast arrays.");
            DateTimeOffset? Sun(string field, int index) => daily.GetProperty(field)[index].ValueKind == JsonValueKind.Null ? null : DateTimeOffset.FromUnixTimeSeconds(daily.GetProperty(field)[index].GetInt64());
            for (int i = 0; i < Math.Min(3, times.GetArrayLength()); i++)
            {
                double min = daily.GetProperty(fields[0])[i].GetDouble(), max = daily.GetProperty(fields[1])[i].GetDouble();
                if (!double.IsFinite(min) || !double.IsFinite(max) || min > max) throw new JsonException("Invalid temperature range.");
                int? probability = daily.GetProperty(fields[2])[i].ValueKind == JsonValueKind.Null ? null : daily.GetProperty(fields[2])[i].GetInt32();
                if (probability < 0 || probability > 100) throw new JsonException("Invalid precipitation probability.");
                days.Add(new ForecastDay(DateTimeOffset.FromUnixTimeSeconds(times[i].GetInt64()), min, max, probability, daily.GetProperty(fields[3])[i].GetInt32(), Sun("sunrise", i), Sun("sunset", i)));
            }
        }
        var hours = new List<ForecastHour>();
        if (root.TryGetProperty("hourly",out var hourly))
        {
            var times=hourly.GetProperty("time");
            string[] fields={"temperature_2m","precipitation_probability","weather_code","wind_speed_10m"};
            foreach(string field in fields)
                if (hourly.GetProperty(field).GetArrayLength()!=times.GetArrayLength()) throw new JsonException("Inconsistent hourly arrays.");
            for(int i=0;i<Math.Min(96,times.GetArrayLength());i++)
            {
                double temperature=hourly.GetProperty(fields[0])[i].GetDouble(),wind=hourly.GetProperty(fields[3])[i].GetDouble();
                int? probability=hourly.GetProperty(fields[1])[i].ValueKind==JsonValueKind.Null?null:hourly.GetProperty(fields[1])[i].GetInt32();
                if(!double.IsFinite(temperature)||!double.IsFinite(wind)||wind<0||probability is <0 or >100) throw new JsonException("Invalid hourly values.");
                hours.Add(new ForecastHour(DateTimeOffset.FromUnixTimeSeconds(times[i].GetInt64()),temperature,probability,hourly.GetProperty(fields[2])[i].GetInt32(),wind));
            }
        }
        string zone=Text(root,"timezone");
        return new WeatherSnapshot(latitude, longitude, weather, days, string.IsNullOrWhiteSpace(zone)?"UTC":zone, DateTimeOffset.UtcNow) {Hourly=hours};
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
        0 or 1 => day ? "☀" : "☾", 2 => day ? "⛅" : "☾☁", 3 => "☁", 45 or 48 => "≋",
        >= 71 and <= 77 or 85 or 86 => "❄", 95 or 96 or 99 => "ϟ",
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "☂", _ => "?"
    };
}

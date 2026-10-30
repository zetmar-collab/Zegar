using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace Zegar;

public sealed record ForecastDay(DateTimeOffset Date, double Minimum, double Maximum, int? PrecipitationProbability, int Code, DateTimeOffset? Sunrise, DateTimeOffset? Sunset);
public sealed record WeatherSnapshot(double Latitude, double Longitude, Weather Current, List<ForecastDay> Forecast, string TimeZone, DateTimeOffset FetchedAt);

public sealed class WeatherState
{
    public WeatherSnapshot? Snapshot { get; private set; }
    public bool IsLoading { get; private set; }
    public bool Offline { get; private set; }
    public bool Cached { get; private set; }
    public string? Error { get; private set; }
    public static bool SameLocation(double lat1, double lon1, double lat2, double lon2) => Math.Abs(lat1 - lat2) < .00001 && Math.Abs(lon1 - lon2) < .00001;
    public void SelectLocation(double latitude, double longitude)
    {
        if (Snapshot != null && !SameLocation(latitude, longitude, Snapshot.Latitude, Snapshot.Longitude))
        { Snapshot = null; Cached = Offline = IsLoading = false; Error = null; }
    }
    public void LoadCache(WeatherSnapshot snapshot) { Snapshot = snapshot; Cached = true; Offline = IsLoading = false; }
    public void Begin() => IsLoading = true;
    public void Complete(WeatherSnapshot snapshot) { Snapshot = snapshot; IsLoading = Offline = Cached = false; Error = null; }
    public void Fail(string error) { IsLoading = false; Offline = true; Error = error; }
    public void Cancel() => IsLoading = false;
    public string Status(string language, bool use24, DateTimeOffset now)
    {
        string T(string key) => UiText.Get(key, language);
        if (IsLoading) return T("Aktualizowanie…");
        if (Offline) return T(Snapshot == null ? "Brak połączenia · spróbuję ponownie" : "Brak połączenia · ostatnie dane");
        if (Snapshot == null) return T("Łączenie…");
        bool stale = now - Snapshot.Current.ObservedAt > TimeSpan.FromMinutes(45);
        return T(stale ? "Starsze dane · " : Cached ? "Zapisane dane · " : "Dane z ") + ClockFormat.Time(Snapshot.Current.ObservedAt.LocalDateTime, use24);
    }
    public TimeSpan RefreshDelay(int minutes) => TimeSpan.FromMinutes(Offline ? Math.Min(Math.Clamp(minutes, 5, 120), 2) : Math.Clamp(minutes, 5, 120));
}

public sealed class WeatherCacheStore
{
    private readonly string folder;
    public WeatherCacheStore(string? folder = null) => this.folder = folder ?? Path.Combine(SettingsStore.Folder, "weather-cache");
    private string PathFor(double latitude, double longitude) => Path.Combine(folder, FormattableString.Invariant($"{latitude:F5}_{longitude:F5}.json"));
    public WeatherSnapshot? Load(double latitude, double longitude)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<WeatherSnapshot>(File.ReadAllText(PathFor(latitude, longitude)));
            if (snapshot?.Current == null || snapshot.Forecast == null || !WeatherState.SameLocation(latitude, longitude, snapshot.Latitude, snapshot.Longitude) ||
                !double.IsFinite(snapshot.Current.Temperature) || !double.IsFinite(snapshot.Current.FeelsLike) || !double.IsFinite(snapshot.Current.Wind) || snapshot.Current.ObservedAt > DateTimeOffset.UtcNow.AddHours(1) ||
                snapshot.Forecast.Any(day => day == null || !double.IsFinite(day.Minimum) || !double.IsFinite(day.Maximum) || day.Minimum > day.Maximum || day.PrecipitationProbability is < 0 or > 100)) return null;
            return snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    public void Save(WeatherSnapshot snapshot)
    {
        Directory.CreateDirectory(folder);
        string path = PathFor(snapshot.Latitude, snapshot.Longitude), temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot));
        File.Move(temp, path, true);
    }
}

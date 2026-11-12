using System;
using System.Globalization;

namespace Zegar;

public sealed record AppearanceProfile(string Name, string BackgroundColor, string FontColor, string FontFamily, double Scale, double Opacity, double TextOpacity, bool Compact)
{
    public static AppearanceProfile Capture(string name, WidgetSettings settings) => new(name.Trim(),settings.BackgroundColor,settings.FontColor,settings.FontFamily,settings.Scale,settings.Opacity,settings.TextOpacity,settings.Compact);
    public AppearanceProfile Normalized()
    {
        var value = new WidgetSettings { BackgroundColor=BackgroundColor,FontColor=FontColor,FontFamily=FontFamily,Scale=Scale,Opacity=Opacity,TextOpacity=TextOpacity };
        value.Normalize(); return Capture(Name.Length > 60 ? Name[..60] : Name,value) with { Compact=Compact };
    }
    public override string ToString() => Name;
}

public static class WeatherUnits
{
    // Cache and requests always use Celsius and km/h; changing units never invalidates data.
    public static string Temperature(double celsius, WidgetSettings settings) => (settings.TemperatureUnit=="F" ? celsius*9/5+32 : celsius).ToString("0",UiText.Culture(settings.Language))+"°"+settings.TemperatureUnit;
    public static string Wind(double kmh, WidgetSettings settings) => (settings.WindUnit switch { "ms" => kmh/3.6,"mph" => kmh/1.609344,_=>kmh }).ToString("0.#",UiText.Culture(settings.Language))+" "+(settings.WindUnit switch {"ms"=>"m/s","mph"=>"mph",_=>"km/h"});
}

public sealed class ReminderState
{
    public DateTimeOffset? DueAt { get; set; }
    public bool Ringing { get; set; }
    public string Kind { get; set; } = "timer";
    public ReminderState Clone() => (ReminderState)MemberwiseClone();
    public void Normalize() { if (Kind != "alarm") Kind="timer"; if (DueAt==null) Ringing=false; }
    public void StartTimer(int minutes, DateTimeOffset now)
    { if (minutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(minutes)); DueAt=now.AddMinutes(minutes);Kind="timer";Ringing=false; }
    public void StartAlarm(TimeOnly time, DateTime now, TimeZoneInfo zone)
    {
        DateTime local=DateTime.SpecifyKind(now.Date.Add(time.ToTimeSpan()),DateTimeKind.Unspecified);
        if (local<=now) local=local.AddDays(1);
        if (zone.IsInvalidTime(local)) throw new ArgumentException("Invalid local alarm time.");
        DueAt=new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local,zone));Kind="alarm";Ringing=false;
    }
    public bool Tick(DateTimeOffset now)
    { if (DueAt==null || Ringing || now<DueAt) return false;Ringing=true;return true; }
    public void Snooze(DateTimeOffset now) { DueAt=now.AddMinutes(5);Ringing=false; }
    public void Stop() { DueAt=null;Ringing=false; }
    public string Remaining(DateTimeOffset now)
    {
        if(DueAt==null)return "—";
        var remaining=TimeSpan.FromSeconds(Math.Max(0,Math.Ceiling((DueAt.Value-now).TotalSeconds)));
        return $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }
}

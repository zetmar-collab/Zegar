using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zegar;

internal sealed class ForecastWindow : Window
{
    private readonly WidgetSettings settings;
    private readonly StackPanel days = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,14) };
    private WeatherSnapshot? displayed;
    private string? displayedStatus;
    private string T(string value) => UiText.Get(value,settings.Language);
    public ForecastWindow(WidgetSettings settings, Func<Task> refresh)
    {
        this.settings = settings.Clone();
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Zegar;component/assets/Zegar.ico"));
        Title = T("Prognoza na 3 dni") + " · " + settings.City;
        Width = 570; Height = Math.Min(640,SystemParameters.WorkArea.Height-24);
        MinWidth = 430; MinHeight = 350; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(246,248,252)); Foreground = new SolidColorBrush(Color.FromRgb(23,35,55));
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        var panel = new DockPanel { Margin = new Thickness(24) };
        var heading = new TextBlock { Text = settings.City, FontSize = 26, FontWeight = FontWeights.SemiBold };
        DockPanel.SetDock(heading,Dock.Top); panel.Children.Add(heading);
        DockPanel.SetDock(status,Dock.Top); panel.Children.Add(status);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,14,0,0) };
        var button = new Button { Content = T("Odśwież"), Padding = new Thickness(14,8,14,8) };
        button.Click += async (_,_) => { button.IsEnabled = false; try { await refresh(); } finally { button.IsEnabled = true; } };
        footer.Children.Add(button);
        footer.Children.Add(new TextBlock { Text = "Open-Meteo · " + T("Czasy w strefie miejscowości"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0), TextWrapping = TextWrapping.Wrap });
        DockPanel.SetDock(footer,Dock.Bottom); panel.Children.Add(footer);
        panel.Children.Add(new ScrollViewer { Content = days, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = panel;
    }
    public void Update(WeatherSnapshot? snapshot, string message)
    {
        if (ReferenceEquals(displayed,snapshot) && displayedStatus == message) return;
        status.Text = message; displayedStatus = message;
        if (ReferenceEquals(displayed,snapshot) && days.Children.Count > 0) return;
        displayed = snapshot; days.Children.Clear();
        if (snapshot == null || snapshot.Forecast.Count == 0)
        { days.Children.Add(new TextBlock { Text = T("Prognoza niedostępna. Spróbuj odświeżyć pogodę."), TextWrapping = TextWrapping.Wrap }); return; }
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(snapshot.TimeZone); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        var culture = UiText.Culture(settings.Language);
        string Sun(DateTimeOffset? value) => value.HasValue ? ClockFormat.Time(TimeZoneInfo.ConvertTime(value.Value,zone).DateTime,settings.Use24HourClock) : "—";
        foreach (var day in snapshot.Forecast)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = TimeZoneInfo.ConvertTime(day.Date,zone).ToString("dddd, d MMMM",culture), FontSize = 17, FontWeight = FontWeights.SemiBold });
            stack.Children.Add(new TextBlock { Text = WeatherService.Symbol(day.Code,true) + "  " + T(WeatherService.Description(day.Code)), Margin = new Thickness(0,7,0,4) });
            stack.Children.Add(new TextBlock { Text = $"Min {day.Minimum.ToString("0",culture)}°C   Max {day.Maximum.ToString("0",culture)}°C   ·   {T("Opady")}: {day.PrecipitationProbability?.ToString(culture) ?? "—"}%", TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(new TextBlock { Text = $"{T("Wschód")}: {Sun(day.Sunrise)}   ·   {T("Zachód")}: {Sun(day.Sunset)}", Margin = new Thickness(0,5,0,0), TextWrapping = TextWrapping.Wrap });
            days.Children.Add(new Border { Child = stack, Padding = new Thickness(14), Margin = new Thickness(0,0,0,12), CornerRadius = new CornerRadius(10), Background = Brushes.White });
        }
    }
}

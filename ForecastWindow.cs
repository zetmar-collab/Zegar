using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zegar;

internal sealed class ForecastWindow : Window
{
    private readonly WidgetSettings settings;
    private readonly StackPanel days = new();
    private readonly StackPanel hours = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,14) };
    private WeatherSnapshot? displayed;
    private string? displayedStatus;
    private string T(string value) => UiText.Get(value,settings.Language);
    public ForecastWindow(WidgetSettings settings, Func<Task> refresh)
    {
        this.settings = settings.Clone();
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Zegar;component/assets/Zegar.ico"));
        Title = T("Prognoza na 3 dni") + " · " + settings.City;
        Width = 640; Height = Math.Min(740,SystemParameters.WorkArea.Height-24);
        MinWidth = 520; MinHeight = 430; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        UiTheme.Apply(this);
        var panel = new DockPanel { Margin = new Thickness(24) };
        var heading = UiTheme.Text(settings.City,30,strong:true);
        DockPanel.SetDock(heading,Dock.Top); panel.Children.Add(heading);
        status.SetResourceReference(TextBlock.ForegroundProperty,"UiMuted");
        DockPanel.SetDock(status,Dock.Top); panel.Children.Add(status);
        var footer = new DockPanel { Margin = new Thickness(0,12,0,0) };
        var button = UiTheme.Button(T("Odśwież"));button.Margin=new Thickness(12,0,0,0);DockPanel.SetDock(button,Dock.Right);
        button.Click += async (_,_) => { button.IsEnabled = false; try { await refresh(); } finally { button.IsEnabled = true; } };
        footer.Children.Add(button);
        var attribution=UiTheme.Text("Open-Meteo\n"+T("Czasy w strefie miejscowości"),12,"UiMuted");attribution.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(attribution);
        DockPanel.SetDock(footer,Dock.Bottom); panel.Children.Add(footer);
        var tabs=new TabControl();
        tabs.Items.Add(new TabItem { Header=T("Prognoza na 3 dni"),Content=new ScrollViewer {Content=days,VerticalScrollBarVisibility=ScrollBarVisibility.Auto} });
        tabs.Items.Add(new TabItem { Header=T("Najbliższe 24 godziny"),Content=new ScrollViewer {Content=hours,VerticalScrollBarVisibility=ScrollBarVisibility.Auto} });
        panel.Children.Add(tabs);
        Content = panel;
    }
    public void Update(WeatherSnapshot? snapshot, string message)
    {
        if (ReferenceEquals(displayed,snapshot) && displayedStatus == message) return;
        status.Text = message; displayedStatus = message;
        if (ReferenceEquals(displayed,snapshot) && days.Children.Count > 0) return;
        displayed = snapshot; days.Children.Clear();hours.Children.Clear();
        if (snapshot == null)
        { days.Children.Add(new TextBlock { Text = T("Prognoza niedostępna. Spróbuj odświeżyć pogodę."), TextWrapping = TextWrapping.Wrap });hours.Children.Add(new TextBlock {Text=T("Prognoza niedostępna. Spróbuj odświeżyć pogodę."),TextWrapping=TextWrapping.Wrap}); return; }
        if (snapshot.Forecast.Count==0) days.Children.Add(new TextBlock { Text=T("Prognoza niedostępna. Spróbuj odświeżyć pogodę."),TextWrapping=TextWrapping.Wrap });
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(snapshot.TimeZone ?? ""); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { zone = TimeZoneInfo.Utc; }
        var culture = UiText.Culture(settings.Language);
        string Sun(DateTimeOffset? value) => value.HasValue ? ClockFormat.Time(TimeZoneInfo.ConvertTime(value.Value,zone).DateTime,settings.Use24HourClock) : "—";
        foreach (var day in snapshot.Forecast)
        {
            var stack = new StackPanel();
            stack.Children.Add(UiTheme.Text(TimeZoneInfo.ConvertTime(day.Date,zone).ToString("dddd, d MMMM",culture),17,strong:true));
            var summary=new DockPanel {Margin=new Thickness(0,10,0,10)};
            var art=new WeatherImage {Decorative=true,Source=WeatherArtwork.Create(day.Code,true),Width=52,Height=48,Margin=new Thickness(0,0,14,0)};DockPanel.SetDock(art,Dock.Left);summary.Children.Add(art);
            var details=new StackPanel();details.Children.Add(UiTheme.Text(T(WeatherService.Description(day.Code)),14,"UiMuted"));
            details.Children.Add(UiTheme.Text($"{WeatherUnits.Temperature(day.Minimum,settings)}  /  {WeatherUnits.Temperature(day.Maximum,settings)}",24,strong:true));summary.Children.Add(details);stack.Children.Add(summary);
            stack.Children.Add(UiTheme.Text($"{T("Minimum / maksimum")} · {T("Opady")}: {day.PrecipitationProbability?.ToString(culture) ?? "—"}%",12,"UiMuted"));
            stack.Children.Add(UiTheme.Text($"{T("Wschód")}: {Sun(day.Sunrise)}   ·   {T("Zachód")}: {Sun(day.Sunset)}",12,"UiMuted"));
            days.Children.Add(UiTheme.Card(stack));
        }
        foreach(var hour in snapshot.Hourly.Where(h=>h.Time>=DateTimeOffset.UtcNow.AddMinutes(-30)).Take(24))
        {
            var local=TimeZoneInfo.ConvertTime(hour.Time,zone);
            var row=new Grid {ToolTip=$"{local:yyyy-MM-dd HH:mm zzz}"};
            foreach(var width in new[]{new GridLength(settings.Use24HourClock?80:100),new GridLength(82),new GridLength(1,GridUnitType.Star),new GridLength(66)})row.ColumnDefinitions.Add(new ColumnDefinition {Width=width});
            void Cell(UIElement element,int column){Grid.SetColumn(element,column);row.Children.Add(element);}
            var time=new StackPanel();time.Children.Add(UiTheme.Text(ClockFormat.Time(local.DateTime,settings.Use24HourClock),16,strong:true));time.Children.Add(UiTheme.Text(local.ToString("ddd d MMM",culture),12,"UiMuted"));Cell(time,0);
            Cell(UiTheme.Text(WeatherUnits.Temperature(hour.Temperature,settings),20,strong:true),1);
            var condition=new StackPanel {Margin=new Thickness(0,0,10,0)};condition.Children.Add(UiTheme.Text(T(WeatherService.Description(hour.Code))));condition.Children.Add(UiTheme.Text(WeatherUnits.Wind(hour.Wind,settings),12,"UiMuted"));Cell(condition,2);
            var rain=new StackPanel();rain.Children.Add(UiTheme.Text(T("Opady"),12,"UiMuted"));rain.Children.Add(UiTheme.Text((hour.PrecipitationProbability?.ToString(culture)??"—")+"%",16,strong:true));Cell(rain,3);
            var card=UiTheme.Card(row);card.Padding=new Thickness(14);card.Margin=new Thickness(0,0,0,8);hours.Children.Add(card);
        }
        if(hours.Children.Count==0) hours.Children.Add(new TextBlock {Text=T("Prognoza godzinowa niedostępna. Odśwież pogodę."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(10)});
    }
}

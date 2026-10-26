using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Zegar;

public partial class ClockWindow : Window
{
    private readonly SettingsStore store = new();
    private readonly WidgetSettings settings;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(18) };
    private readonly WeatherService weather;
    private readonly DispatcherTimer clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer weatherTimer = new();
    private CancellationTokenSource? fetch;
    private Weather? lastWeather;
    private bool lightBackground;
    private SettingsWindow? editor;
    private Forms.NotifyIcon? tray;
    private bool weatherOffline;
    private CultureInfo Culture => UiText.Culture(settings.Language);
    private string T(string text) => UiText.Get(text, settings.Language);

    public ClockWindow()
    {
        InitializeComponent();
        settings = store.Load();
        weather = new WeatherService(http);
        ContextMenu = CreateMenu();
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
            { args.Handled = true; ShowSettings(); }
        };
        ApplySettings();
        clockTimer.Tick += (_, _) => { UpdateClock(); UpdateStatus(); };
        weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();
        Loaded += async (_, _) =>
        {
            EnsureVisible(); UpdateClock(); clockTimer.Start(); weatherTimer.Start();
            SetupTray(); await RefreshWeatherAsync();
        };
        Closed += (_, _) =>
        {
            clockTimer.Stop(); weatherTimer.Stop(); fetch?.Cancel();
            editor?.Close(); tray?.Dispose(); http.Dispose(); TrySave();
        };
        SystemEventsHook();
    }
    private void SystemEventsHook()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerChanged;
    }
    private void OnPowerChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume)
            Dispatcher.InvokeAsync(async () => { UpdateClock(); EnsureVisible(); await RefreshWeatherAsync(); });
    }
    private void UpdateClock()
    {
        var now = DateTime.Now;
        Hours.Text = ClockFormat.Hour(now, settings.Use24HourClock);
        Minutes.Text = now.ToString("mm", CultureInfo.InvariantCulture);
        DateLabel.Text = ClockFormat.Date(now, settings.Language);
        PeriodLabel.Text = ClockFormat.Period(now, settings.Use24HourClock);
        PeriodLabel.Visibility = settings.Use24HourClock ? Visibility.Collapsed : Visibility.Visible;
    }
    private ContextMenu CreateMenu()
    {
        var menu = new ContextMenu();
        var configure = new MenuItem { Header = T("Ustawienia…"), InputGestureText = "Ctrl+," };
        configure.Click += (_, _) => ShowSettings(); menu.Items.Add(configure);
        menu.Items.Add(new Separator());
        var locked = new MenuItem { Header = T("Zablokuj położenie"), IsCheckable = true };
        locked.Click += (_, _) => { settings.Locked = locked.IsChecked; ApplySettings(); TrySave(); };
        menu.Items.Add(locked);
        var onTop = new MenuItem { Header = T("Zawsze na wierzchu"), IsCheckable = true };
        onTop.Click += (_, _) => { settings.Topmost = onTop.IsChecked; ApplySettings(); TrySave(); };
        menu.Items.Add(onTop);
        menu.Items.Add(new Separator());
        var refresh = new MenuItem { Header = T("Odśwież widżet") };
        refresh.Click += async (_, _) => { UpdateClock(); clockTimer.Stop(); clockTimer.Start(); weatherTimer.Stop(); weatherTimer.Start(); await RefreshWeatherAsync(); };
        menu.Items.Add(refresh);
        var restart = new MenuItem { Header = T("Uruchom ponownie") };
        restart.Click += (_, _) => Restart(); menu.Items.Add(restart);
        menu.Items.Add(new Separator());
        var exit = new MenuItem { Header = T("Zamknij") };
        exit.Click += (_, _) => Close(); menu.Items.Add(exit);
        menu.Opened += (_, _) => { locked.IsChecked = settings.Locked; onTop.IsChecked = settings.Topmost; };
        return menu;
    }
    private void SetupTray()
    {
        if (tray == null)
        {
            tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Visible = true };
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); Activate(); });
        }
        tray.Text = T("Zegar HTC • prawy przycisk: menu");
        tray.ContextMenuStrip?.Dispose();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(T("Pokaż zegar"), null, (_, _) => Dispatcher.Invoke(() => { Show(); EnsureVisible(); Activate(); }));
        menu.Items.Add(T("Ustawienia…"), null, (_, _) => Dispatcher.Invoke(ShowSettings));
        menu.Items.Add(T("Odśwież"), null, (_, _) => Dispatcher.InvokeAsync(async () => await RefreshWeatherAsync()));
        menu.Items.Add(T("Zamknij"), null, (_, _) => Dispatcher.Invoke(Close));
        tray.ContextMenuStrip = menu;
    }
    private void ShowSettings()
    {
        if (editor != null) { editor.Activate(); return; }
        var dialog = new SettingsWindow(settings, weather) { Owner = this, Topmost = Topmost, ShowInTaskbar = ShowInTaskbar };
        editor = dialog;
        dialog.SettingsApplied += async (_, _) => { lastWeather = null; ApplySettings(); TrySave(); await RefreshWeatherAsync(); };
        Root.Opacity = .45;
        try { dialog.ShowDialog(); }
        finally { editor = null; Root.Opacity = 1; }
    }
    private void ApplySettings()
    {
        settings.Normalize();
        ApplyColor();
        var font = new FontFamily(string.IsNullOrEmpty(settings.FontFamily) ? "Segoe UI" : settings.FontFamily);
        System.Windows.Documents.TextElement.SetFontFamily(Root, font);
        Hours.FontFamily = Minutes.FontFamily = string.IsNullOrEmpty(settings.FontFamily) ? new FontFamily("Arial") : font;
        Root.LayoutTransform = new ScaleTransform(settings.Scale, settings.Scale);
        Width = 420 * settings.Scale; Height = 382 * settings.Scale;
        Opacity = settings.Opacity; Topmost = settings.Topmost;
        Left = settings.Left; Top = settings.Top;
        weatherTimer.Interval = TimeSpan.FromMinutes(settings.RefreshMinutes);
        CityLabel.Text = settings.City; CityLabel.ToolTip = settings.City;
        Title = T("Zegar HTC");
        HeaderLabel.Text = T("ZEGAR / POGODA"); CelsiusLabel.Text = T("CELSJUSZA");
        LockMark.Text = settings.Locked ? T("POŁOŻENIE ZABLOKOWANE") : settings.Use24HourClock ? "24 H" : "12 H";
        ContextMenu = CreateMenu();
        if (tray != null) SetupTray();
        UpdateClock();
        if (lastWeather != null) RenderWeather(lastWeather);
        else { ConditionLabel.Text = T("Pobieranie pogody…"); StatusLabel.Text = T("Łączenie…"); }
        if (IsLoaded) EnsureVisible();
    }
    private void ApplyColor()
    {
        var background = (Color)ColorConverter.ConvertFromString(settings.BackgroundColor);
        static Color Blend(Color a, Color b, double amount) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * amount), (byte)(a.G + (b.G - a.G) * amount), (byte)(a.B + (b.B - a.B) * amount));
        bool light = Appearance.IsLight(background);
        lightBackground = light;
        var primary = Appearance.TextColor(settings.BackgroundColor, settings.FontColor);
        bool customText = !string.IsNullOrEmpty(settings.FontColor);
        Resources["ThemePrimary"] = new SolidColorBrush(primary);
        Resources["ThemeSecondary"] = new SolidColorBrush(customText ? primary : Blend(primary, background, .12));
        Resources["ThemeMuted"] = new SolidColorBrush(customText ? primary : Blend(primary, background, .20));
        Resources["ThemeDigits"] = new SolidColorBrush(customText ? primary : Color.FromRgb(34, 38, 45));
        Resources["ThemeLine"] = new SolidColorBrush(Blend(light ? Colors.Black : Colors.White, background, .75));
        bool darkCards = customText && Appearance.IsLight(primary);
        Resources["Card"] = new LinearGradientBrush(new GradientStopCollection
        {
            new(darkCards ? Color.FromRgb(56, 62, 74) : Colors.White, 0),
            new(darkCards ? Color.FromRgb(43, 48, 60) : Color.FromRgb(242, 241, 238), .49),
            new(darkCards ? Color.FromRgb(25, 29, 39) : Color.FromRgb(212, 213, 215), .5),
            new(darkCards ? Color.FromRgb(44, 50, 62) : Color.FromRgb(250, 250, 247), 1)
        }, new Point(0, 0), new Point(0, 1));
        Shell.BorderBrush = (System.Windows.Media.Brush)Resources["ThemeLine"];
        Shell.Background = new LinearGradientBrush(new GradientStopCollection
        {
            new(Blend(background, Colors.White, .13), 0), new(background, .55), new(Blend(background, Colors.Black, .3), 1)
        }, new Point(0, 0), new Point(.7, 1));
        UpdateWeatherIconColor();
    }
    private void UpdateWeatherIconColor()
    {
        WeatherIcon.Foreground = lightBackground ? (System.Windows.Media.Brush)Resources["ThemePrimary"] :
            new SolidColorBrush(lastWeather?.Code is 0 or 1 or 2 ? Color.FromRgb(255, 215, 124) : Color.FromRgb(168, 202, 230));
    }
    private void EnsureVisible()
    {
        var area = SystemParameters.WorkArea;
        bool visible = false;
        // Convert each monitor's physical work area to this window's device-independent units.
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var a = transform.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
            var b = transform.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
            if (Left >= a.X && Top >= a.Y && Left + Width <= b.X && Top + Height <= b.Y) { visible = true; break; }
        }
        if (!visible)
        {
            Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
            Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        }
        settings.Left = Left; settings.Top = Top;
    }
    private void DragWidget(object sender, MouseButtonEventArgs e)
    {
        if (settings.Locked || e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is System.Windows.Documents.Run || e.OriginalSource is System.Windows.Documents.Hyperlink) return;
        DragMove(); settings.Left = Left; settings.Top = Top; TrySave();
    }
    private async Task RefreshWeatherAsync()
    {
        fetch?.Cancel();
        var request = new CancellationTokenSource(); fetch = request;
        StatusLabel.Text = T("Aktualizowanie…");
        try
        {
            var result = await weather.GetAsync(settings.Latitude, settings.Longitude, request.Token);
            if (request.IsCancellationRequested) return;
            lastWeather = result;
            weatherOffline = false;
            RenderWeather(result);
            weatherTimer.Interval = TimeSpan.FromMinutes(settings.RefreshMinutes);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException or ArgumentException)
        {
            if (request.IsCancellationRequested) return;
            weatherOffline = true;
            StatusLabel.Text = T(lastWeather == null ? "Brak połączenia · spróbuję ponownie" : "Brak połączenia · ostatnie dane");
            StatusLabel.ToolTip = T("Nie udało się pobrać pogody. ") + ex.Message;
            if (lastWeather == null)
            { ConditionLabel.Text = T("Pogoda niedostępna"); TemperatureLabel.Text = "—°"; DetailsLabel.Text = T("PPM → Odśwież widżet"); }
            // Retry sooner after an error, without blocking the UI or clock.
            weatherTimer.Interval = TimeSpan.FromMinutes(Math.Min(settings.RefreshMinutes, 2));
        }
        finally
        {
            if (ReferenceEquals(fetch, request)) fetch = null;
            request.Dispose();
            UpdateStatus();
        }
    }
    private void RenderWeather(Weather result)
    {
        TemperatureLabel.Text = result.Temperature.ToString("0", Culture) + "°";
        ConditionLabel.Text = T(WeatherService.Description(result.Code));
        WeatherIcon.Text = WeatherService.Symbol(result.Code, result.IsDay);
        UpdateWeatherIconColor();
        DetailsLabel.Text = $"{T("Odcz.")} {result.FeelsLike.ToString("0", Culture)}° · {T("wiatr")} {result.Wind.ToString("0", Culture)} km/h";
        StatusLabel.ToolTip = T("Czas danych z Open-Meteo: ") + ClockFormat.Date(result.ObservedAt.LocalDateTime, settings.Language) + ", " + ClockFormat.Time(result.ObservedAt.LocalDateTime, settings.Use24HourClock);
        StatusLabel.Text = T("Dane z ") + ClockFormat.Time(result.ObservedAt.LocalDateTime, settings.Use24HourClock);
    }
    private void UpdateStatus()
    {
        if (lastWeather == null || fetch != null || weatherOffline) return;
        bool stale = DateTimeOffset.UtcNow - lastWeather.ObservedAt > TimeSpan.FromMinutes(45);
        StatusLabel.Text = T(stale ? "Starsze dane · " : "Dane z ") + ClockFormat.Time(lastWeather.ObservedAt.LocalDateTime, settings.Use24HourClock);
        weatherTimer.Interval = TimeSpan.FromMinutes(settings.RefreshMinutes);
    }
    private void TrySave()
    {
        try { store.Save(settings); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { StatusLabel.Text = T("Nie udało się zapisać ustawień"); StatusLabel.ToolTip = ex.Message; }
    }
    private void Restart()
    {
        TrySave();
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--restart") { UseShellExecute = true });
    }
    private void OpenAttribution(object sender, RequestNavigateEventArgs e)
    { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
}

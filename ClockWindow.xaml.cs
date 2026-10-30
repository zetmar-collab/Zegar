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
    private readonly SettingsStore store;
    private WidgetSettings settings;
    private readonly HttpClient http;
    private readonly WeatherCacheStore cache;
    private readonly IStartupSettings startup;
    private readonly WeatherState weatherState = new();
    private readonly WeatherService weather;
    private readonly DispatcherTimer clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer weatherTimer = new();
    private CancellationTokenSource? fetch;
    private bool closing;
    private string? persistenceError;
    private string? recoveryNotice;
    private ForecastWindow? forecastWindow;
    private bool lightBackground;
    private SettingsWindow? editor;
    private Forms.NotifyIcon? tray;
    private CultureInfo Culture => UiText.Culture(settings.Language);
    private string T(string text) => UiText.Get(text, settings.Language);

    public ClockWindow() : this(new SettingsStore(), new HttpClient { Timeout = TimeSpan.FromSeconds(18) }, new WeatherCacheStore()) { }
    internal ClockWindow(SettingsStore store, HttpClient http, WeatherCacheStore cache, IStartupSettings? startup = null)
    {
        InitializeComponent();
        this.store = store; this.http = http; this.cache = cache;
        this.startup = startup ?? new WindowsStartupSettings();
        settings = store.Load();
        if (store.RecoveredFromBackup) recoveryNotice = "Przywrócono ustawienia z kopii zapasowej.";
        else if (store.LoadFailed) recoveryNotice = "Nie udało się odczytać ustawień. Uszkodzony plik zostanie zachowany.";
        LoadWeatherCache();
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
            EnsureVisible(); RememberPosition(); UpdateClock(); clockTimer.Start();
            SetupTray(); await RefreshWeatherAsync();
        };
        Closed += (_, _) =>
        {
            closing = true; clockTimer.Stop(); weatherTimer.Stop(); fetch?.Cancel();
            editor?.Close(); forecastWindow?.Close(); tray?.Icon?.Dispose(); tray?.Dispose(); http.Dispose(); TrySave();
        };
        SystemEventsHook();
    }
    private void SystemEventsHook()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closed += (_, _) => { Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerChanged; Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged; };
    }
    private void OnDisplayChanged(object? sender, EventArgs args) => Dispatcher.InvokeAsync(() => { EnsureVisible(); RememberPosition(); TrySave(); });
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
        refresh.Click += async (_, _) => { UpdateClock(); await RefreshWeatherAsync(); };
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
            using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Zegar;component/assets/Zegar.ico")).Stream;
            using var sourceIcon = new System.Drawing.Icon(iconStream, 32, 32);
            tray = new Forms.NotifyIcon { Icon = (System.Drawing.Icon)sourceIcon.Clone(), Visible = true };
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWidget);
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
        var dialog = new SettingsWindow(settings, weather, recoveryNotice, startup.Enabled) { Owner = this, Topmost = Topmost, ShowInTaskbar = ShowInTaskbar };
        editor = dialog;
        dialog.CommitSettings = CommitSettings;
        Root.Opacity = .45;
        try { dialog.ShowDialog(); }
        finally { editor = null; Root.Opacity = 1; }
    }
    private string? CommitSettings(WidgetSettings candidate, bool startup)
    {
        string? previousCommand = this.startup.Command;
        candidate.Normalize();
        try { store.Commit(candidate, () => this.startup.Set(startup), () => this.startup.Restore(previousCommand)); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return UiText.Get("Nie udało się zapisać ustawień",candidate.Language) + ": " + ex.Message; }
        bool moved = !WeatherState.SameLocation(settings.Latitude, settings.Longitude, candidate.Latitude, candidate.Longitude);
        bool intervalChanged = settings.RefreshMinutes != candidate.RefreshMinutes;
        settings = candidate.Clone(); persistenceError = recoveryNotice = null;
        forecastWindow?.Close();
        if (moved)
        {
            fetch?.Cancel(); weatherState.SelectLocation(settings.Latitude, settings.Longitude); LoadWeatherCache();
        }
        ApplySettings();
        if (moved) Dispatcher.InvokeAsync(async () => await RefreshWeatherAsync());
        else if (intervalChanged) ScheduleRefresh();
        return null;
    }
    private void LoadWeatherCache()
    {
        var saved = cache.Load(settings.Latitude, settings.Longitude);
        if (saved != null) weatherState.LoadCache(saved);
    }
    private void ScheduleRefresh()
    {
        weatherTimer.Stop();
        weatherTimer.Interval = weatherState.RefreshDelay(settings.RefreshMinutes);
        if (!closing && IsLoaded) weatherTimer.Start();
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
        Opacity = 1; Topmost = settings.Topmost;
        Left = settings.Left; Top = settings.Top;
        CityLabel.Text = settings.City; CityLabel.ToolTip = settings.City;
        Title = T("Zegar HTC");
        HeaderLabel.Text = T("ZEGAR / POGODA"); CelsiusLabel.Text = T("CELSJUSZA");
        LockMark.Text = settings.Locked ? T("POŁOŻENIE ZABLOKOWANE") : settings.Use24HourClock ? "24 H" : "12 H";
        ContextMenu = CreateMenu();
        if (tray != null) SetupTray();
        UpdateClock();
        RenderWeather();
        ForecastButton.Content = T("Prognoza…");
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
        Resources["ThemePrimary"] = new SolidColorBrush(primary) { Opacity = settings.TextOpacity };
        Resources["ThemeSecondary"] = new SolidColorBrush(customText ? primary : Blend(primary, background, .12)) { Opacity = settings.TextOpacity };
        Resources["ThemeMuted"] = new SolidColorBrush(customText ? primary : Blend(primary, background, .20)) { Opacity = settings.TextOpacity };
        Resources["ThemeDigits"] = new SolidColorBrush(customText ? primary : Color.FromRgb(34, 38, 45)) { Opacity = settings.TextOpacity };
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
        Shell.Background.Opacity = settings.Opacity;
        UpdateWeatherIconColor();
    }
    private void UpdateWeatherIconColor()
    {
        WeatherIcon.Foreground = lightBackground ? (System.Windows.Media.Brush)Resources["ThemePrimary"] :
            new SolidColorBrush(weatherState.Snapshot?.Current.Code is 0 or 1 or 2 ? Color.FromRgb(255, 215, 124) : Color.FromRgb(168, 202, 230));
        WeatherIcon.Opacity = settings.TextOpacity;
    }
    private void EnsureVisible()
    {
        if (WindowPlacement.EnsureVisible(this))
        { settings.Scale = ((ScaleTransform)Root.LayoutTransform).ScaleX; RememberPosition(); }
    }
    private void RememberPosition()
    {
        settings.Left = Left; settings.Top = Top;
        if (WindowPlacement.Position(this) is Point point) { settings.PhysicalLeft = point.X; settings.PhysicalTop = point.Y; }
    }
    protected override void OnSourceInitialized(EventArgs e)
    { base.OnSourceInitialized(e); WindowPlacement.Restore(this, settings.PhysicalLeft, settings.PhysicalTop); }
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    { base.OnDpiChanged(oldDpi,newDpi); Dispatcher.InvokeAsync(() => { EnsureVisible(); RememberPosition(); TrySave(); }); }
    public void ShowWidget()
    {
        if (editor != null) { editor.WindowState = WindowState.Normal; editor.Activate(); return; }
        Show(); WindowState = WindowState.Normal; EnsureVisible(); Activate();
    }
    private void OpenForecast(object sender, RoutedEventArgs e)
    {
        if (forecastWindow != null) { forecastWindow.Activate(); return; }
        forecastWindow = new ForecastWindow(settings, RefreshWeatherAsync) { Owner = this, Topmost = Topmost };
        forecastWindow.Update(weatherState.Snapshot, weatherState.Status(settings.Language,settings.Use24HourClock,DateTimeOffset.UtcNow));
        forecastWindow.Closed += (_, _) => forecastWindow = null;
        forecastWindow.Show();
    }
    private void DragWidget(object sender, MouseButtonEventArgs e)
    {
        if (settings.Locked || e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is System.Windows.Documents.Run || e.OriginalSource is System.Windows.Documents.Hyperlink || WindowPlacement.IsButton(e.OriginalSource as DependencyObject)) return;
        DragMove(); RememberPosition(); TrySave();
    }
    private async Task RefreshWeatherAsync()
    {
        fetch?.Cancel();
        weatherTimer.Stop();
        var request = new CancellationTokenSource(); fetch = request;
        weatherState.Begin(); UpdateStatus();
        try
        {
            var result = await weather.GetSnapshotAsync(settings.Latitude, settings.Longitude, request.Token);
            if (request.IsCancellationRequested) return;
            weatherState.Complete(result);
            try { cache.Save(result); } catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { recoveryNotice = "Nie udało się zapisać pamięci pogody."; }
            RenderWeather();
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException or ArgumentException or FormatException or OverflowException)
        {
            if (request.IsCancellationRequested) return;
            weatherState.Fail(ex.Message); RenderWeather();
        }
        finally
        {
            if (ReferenceEquals(fetch, request)) { fetch = null; weatherState.Cancel(); if (!closing) ScheduleRefresh(); }
            request.Dispose();
            UpdateStatus();
        }
    }
    private void RenderWeather()
    {
        var result = weatherState.Snapshot?.Current;
        if (result == null)
        {
            ConditionLabel.Text = T(weatherState.Offline ? "Pogoda niedostępna" : "Pobieranie pogody…");
            TemperatureLabel.Text = "—°"; DetailsLabel.Text = ""; WeatherIcon.Text = "?";
            UpdateStatus(); return;
        }
        TemperatureLabel.Text = result.Temperature.ToString("0", Culture) + "°";
        ConditionLabel.Text = T(WeatherService.Description(result.Code));
        WeatherIcon.Text = WeatherService.Symbol(result.Code, result.IsDay);
        UpdateWeatherIconColor();
        DetailsLabel.Text = $"{T("Odcz.")} {result.FeelsLike.ToString("0", Culture)}° · {T("wiatr")} {result.Wind.ToString("0", Culture)} km/h";
        UpdateStatus();
    }
    private void UpdateStatus()
    {
        StatusLabel.Text = persistenceError == null ? weatherState.Status(settings.Language, settings.Use24HourClock, DateTimeOffset.UtcNow) : T("Nie udało się zapisać ustawień");
        string observed = weatherState.Snapshot == null ? "" : T("Czas danych z Open-Meteo: ") + ClockFormat.Date(weatherState.Snapshot.Current.ObservedAt.LocalDateTime, settings.Language) + ", " + ClockFormat.Time(weatherState.Snapshot.Current.ObservedAt.LocalDateTime, settings.Use24HourClock);
        StatusLabel.ToolTip = string.Join("\n", new[] { observed, weatherState.Error, persistenceError, recoveryNotice == null ? null : T(recoveryNotice) });
        forecastWindow?.Update(weatherState.Snapshot, weatherState.Status(settings.Language, settings.Use24HourClock, DateTimeOffset.UtcNow));
    }
    private void TrySave()
    {
        try { store.Save(settings); persistenceError = null; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { persistenceError = ex.Message; UpdateStatus(); }
    }
    private void Restart()
    {
        TrySave();
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--restart") { UseShellExecute = true });
    }
    private void OpenAttribution(object sender, RequestNavigateEventArgs e)
    { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
}

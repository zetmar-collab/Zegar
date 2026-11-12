using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
    private bool savingPreferences, deferredSave, closeAfterCommit;
    private ReminderWindow? reminderWindow;
    private DiagnosticsWindow? diagnosticsWindow;
    private DateTimeOffset lastReminderSound=DateTimeOffset.MinValue;
    private string? weatherFailure;
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
            else if(Keyboard.Modifiers==ModifierKeys.Control)
            {
                if(args.Key==Key.T){args.Handled=true;ShowReminder();}
                else if(args.Key==Key.D){args.Handled=true;ShowDiagnostics();}
                else if(args.Key==Key.F){args.Handled=true;OpenForecast(this,new RoutedEventArgs());}
            }
        };
        ApplySettings();
        clockTimer.Tick += (_, _) => { UpdateClock(); UpdateStatus(); TickReminder(); };
        weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();
        Loaded += async (_, _) =>
        {
            EnsureVisible(); RememberPosition(); UpdateClock(); clockTimer.Start();
            SetupTray(); await RefreshWeatherAsync();
        };
        Closed += (_, _) =>
        {
            closing = true; clockTimer.Stop(); weatherTimer.Stop(); fetch?.Cancel();
            editor?.Close(); forecastWindow?.Close(); reminderWindow?.Close();diagnosticsWindow?.Close();tray?.Icon?.Dispose(); tray?.Dispose(); http.Dispose();
            try { store.Save(settings); } catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException) {AppLog.Write("settings-close",ex);}
        };
        Closing+=(_,args)=>{if(savingPreferences){args.Cancel=true;closeAfterCommit=true;}};
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
        PeriodLabel.Visibility = settings.Use24HourClock || settings.Compact ? Visibility.Collapsed : Visibility.Visible;
        LockMark.Text=settings.Compact
            ? (settings.Use24HourClock?"24 H":ClockFormat.Period(now,false))+(settings.Locked?" · "+T("Blokada"):"")
            : settings.Locked?T("POŁOŻENIE ZABLOKOWANE"):settings.Use24HourClock?"24 H":"12 H";
        LockMark.ToolTip=settings.Locked?T("POŁOŻENIE ZABLOKOWANE"):null;
    }
    private ContextMenu CreateMenu()
    {
        var menu = new ContextMenu();
        var forecast = new MenuItem { Header=T("Prognoza…"),InputGestureText="Ctrl+F" };
        forecast.Click += (_,_)=>OpenForecast(this,new RoutedEventArgs());menu.Items.Add(forecast);
        if (settings.Favorites.Count>0)
        {
            var favorites = new MenuItem { Header=T("Ulubione miejscowości") };
            foreach (var place in settings.Favorites)
            {
                var item=new MenuItem {Header=place.Name,ToolTip=place.ToString()};
                item.Click+=async (_,_)=>{if(savingPreferences)return;var candidate=settings.Clone();candidate.City=place.Name;candidate.Latitude=place.Latitude;candidate.Longitude=place.Longitude;
                    string? error=await CommitSettingsAsync(candidate,startup.Enabled);if(error!=null) MessageBox.Show(error,Title);};
                favorites.Items.Add(item);
            }
            menu.Items.Add(favorites);
        }
        var configure = new MenuItem { Header = T("Ustawienia…"), InputGestureText = "Ctrl+," };
        configure.Click += (_, _) => ShowSettings(); menu.Items.Add(configure);
        var reminder=new MenuItem {Header=T("Minutnik i alarm"),InputGestureText="Ctrl+T"};reminder.Click+=(_,_)=>ShowReminder();menu.Items.Add(reminder);
        var diagnostics=new MenuItem {Header=T("Diagnostyka"),InputGestureText="Ctrl+D"};diagnostics.Click+=(_,_)=>ShowDiagnostics();menu.Items.Add(diagnostics);
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
        menu.Items.Add(T("Minutnik i alarm"),null,(_,_)=>Dispatcher.Invoke(ShowReminder));
        menu.Items.Add(T("Diagnostyka"),null,(_,_)=>Dispatcher.Invoke(ShowDiagnostics));
        menu.Items.Add(T("Odśwież"), null, (_, _) => Dispatcher.InvokeAsync(async () => await RefreshWeatherAsync()));
        menu.Items.Add(T("Zamknij"), null, (_, _) => Dispatcher.Invoke(Close));
        tray.ContextMenuStrip = menu;
    }
    private void ShowSettings()
    {
        if (editor != null) { editor.Activate(); return; }
        var dialog = new SettingsWindow(settings, weather, recoveryNotice, startup.Enabled) { Owner = this, Topmost = Topmost, ShowInTaskbar = ShowInTaskbar };
        editor = dialog;
        dialog.CommitSettingsAsync = CommitSettingsAsync;
        Root.Opacity = .45;
        try { dialog.ShowDialog(); }
        finally { editor = null; Root.Opacity = 1; }
    }
    private void PrepareCandidate(WidgetSettings candidate)
    {
        candidate.Left = settings.Left; candidate.Top = settings.Top;
        candidate.PhysicalLeft = settings.PhysicalLeft; candidate.PhysicalTop = settings.PhysicalTop;
        candidate.Reminder = settings.Reminder.Clone();
        if (editor != null && !editor.ScaleWasEdited) candidate.Scale = settings.Scale;
        candidate.Normalize();
    }
    private async Task<string?> CommitSettingsAsync(WidgetSettings candidate,bool startupEnabled)
    {
        if(savingPreferences) return T("Zapisywanie…");
        savingPreferences=true;
        try
        {
            PrepareCandidate(candidate);
            string? previous=this.startup.Command;
            await store.CommitAsync(candidate,()=>this.startup.Set(startupEnabled),()=>this.startup.Restore(previous));
            // System display events and reminder ticks can run while disk I/O is pending.
            candidate.Left=settings.Left;candidate.Top=settings.Top;candidate.PhysicalLeft=settings.PhysicalLeft;candidate.PhysicalTop=settings.PhysicalTop;
            candidate.Reminder=settings.Reminder.Clone();
            if(editor!=null && !editor.ScaleWasEdited)candidate.Scale=settings.Scale;
            ApplyCommitted(candidate);
            return null;
        }
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {AppLog.Write("settings-commit",ex);return UiText.Get("Nie udało się zapisać ustawień",candidate.Language)+": "+ex.Message;}
        finally
        {
            savingPreferences=false;
            if(deferredSave){deferredSave=false;TrySave();}
            if(closeAfterCommit)_ = Dispatcher.InvokeAsync(Close);
        }
    }
    private void ApplyCommitted(WidgetSettings candidate)
    {
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
        weatherState.NextAttempt=DateTimeOffset.UtcNow+weatherTimer.Interval;
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
        double baseWidth=settings.Compact?320:420,baseHeight=settings.Compact?252:382;
        Root.Width=baseWidth;Root.Height=baseHeight;Width=baseWidth*settings.Scale;Height=baseHeight*settings.Scale;
        WidgetContent.RowDefinitions[0].Height=new GridLength(settings.Compact?18:24);
        WidgetContent.RowDefinitions[1].Height=new GridLength(settings.Compact?100:146);
        WidgetContent.RowDefinitions[2].Height=new GridLength(settings.Compact?0:39);
        DateLabel.Visibility=PeriodLabel.Visibility=settings.Compact?Visibility.Collapsed:Visibility.Visible;
        ConditionLabel.Visibility=DetailsLabel.Visibility=ForecastButton.Visibility=settings.Compact?Visibility.Collapsed:Visibility.Visible;
        WeatherIcon.Width=settings.Compact?44:66;WeatherIcon.Height=settings.Compact?44:64;
        CelsiusLabel.Visibility=settings.Compact?Visibility.Collapsed:Visibility.Visible;
        TemperatureLabel.FontSize=settings.Compact?30:37;
        StatusLabel.MaxWidth=settings.Compact?160:255;
        Opacity = 1; Topmost = settings.Topmost;
        Left = settings.Left; Top = settings.Top;
        CityLabel.Text = settings.City; CityLabel.ToolTip = settings.City;
        Title = T("Zegar HTC");
        HeaderLabel.Text = T("ZEGAR / POGODA"); CelsiusLabel.Text = settings.TemperatureUnit=="F" ? T("FAHRENHEITA") : T("CELSJUSZA");
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
        var current=weatherState.Snapshot?.Current;
        WeatherIcon.Source=WeatherArtwork.Create(current?.Code??-1,current?.IsDay??true,lightBackground?((SolidColorBrush)Resources["ThemePrimary"]).Color:null);
        System.Windows.Automation.AutomationProperties.SetName(WeatherIcon,current==null?T("Pogoda niedostępna"):T(WeatherService.Description(current.Code)));
        WeatherIcon.Decorative=!settings.Compact;
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
            if (closing || request.IsCancellationRequested || !ReferenceEquals(fetch,request)) return;
            weatherState.Complete(result);
            weatherFailure=null;AppLog.Write("weather-success");
            RenderWeather();
            try { await cache.SaveAsync(result); } catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { recoveryNotice = "Nie udało się zapisać pamięci pogody.";AppLog.Write("weather-cache",ex); }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException or ArgumentException or FormatException or OverflowException)
        {
            if (request.IsCancellationRequested) return;
            weatherFailure=ex is HttpRequestException h ? $"HTTP {h.StatusCode?.ToString() ?? "connection"}" : ex is OperationCanceledException ? "timeout" : "invalid-data";
            AppLog.Write("weather-failure",ex);
            weatherState.Fail(ex.Message); RenderWeather();
        }
        finally
        {
            if (ReferenceEquals(fetch, request)) { fetch = null; weatherState.Cancel(); if (!closing) ScheduleRefresh(); }
            request.Dispose();
            if(!closing) UpdateStatus();
        }
    }
    private void RenderWeather()
    {
        var result = weatherState.Snapshot?.Current;
        if (result == null)
        {
            ConditionLabel.Text = T(weatherState.Offline ? "Pogoda niedostępna" : "Pobieranie pogody…");
            TemperatureLabel.Text = "—°"; DetailsLabel.Text = ""; UpdateWeatherIconColor();
            UpdateStatus(); return;
        }
        TemperatureLabel.Text = settings.Compact ? WeatherUnits.Temperature(result.Temperature,settings) : WeatherUnits.Temperature(result.Temperature,settings).Replace(settings.TemperatureUnit,"");
        ConditionLabel.Text = T(WeatherService.Description(result.Code));
        UpdateWeatherIconColor();
        DetailsLabel.Text = $"{T("Odcz.")} {WeatherUnits.Temperature(result.FeelsLike,settings)} · {T("wiatr")} {WeatherUnits.Wind(result.Wind,settings)}";
        UpdateStatus();
    }
    private void UpdateStatus()
    {
        StatusLabel.Text = persistenceError == null ? weatherState.Status(settings.Language, settings.Use24HourClock, DateTimeOffset.UtcNow) : T("Nie udało się zapisać ustawień");
        string observed = weatherState.Snapshot == null ? "" : T("Czas danych z Open-Meteo: ") + ClockFormat.Date(weatherState.Snapshot.Current.ObservedAt.LocalDateTime, settings.Language) + ", " + ClockFormat.Time(weatherState.Snapshot.Current.ObservedAt.LocalDateTime, settings.Use24HourClock);
        StatusLabel.ToolTip = string.Join("\n", new[] { observed, weatherState.Error, persistenceError, recoveryNotice == null ? null : T(recoveryNotice) });
        forecastWindow?.Update(weatherState.Snapshot, weatherState.Status(settings.Language, settings.Use24HourClock, DateTimeOffset.UtcNow));
    }
    private async void TrySave()
    {
        if(closing)return;
        if(savingPreferences){deferredSave=true;return;}
        try { await store.SaveAsync(settings); persistenceError = null; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { persistenceError = ex.Message;AppLog.Write("settings-save",ex);if(!closing)UpdateStatus(); }
    }
    private async void Restart()
    {
        if(savingPreferences)return;
        try {await store.SaveAsync(settings);}catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException){persistenceError=ex.Message;UpdateStatus();return;}
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--restart") { UseShellExecute = true });
    }
    private void ShowReminder()
    {
        if(reminderWindow!=null){reminderWindow.Activate();return;}
        reminderWindow=new ReminderWindow(settings.Language,()=>settings.Reminder,()=>{lastReminderSound=DateTimeOffset.MinValue;TrySave();}) {Owner=editor ?? (Window)this};
        reminderWindow.Closed+=(_,_)=>reminderWindow=null;reminderWindow.Show();
    }
    private void TickReminder()
    {
        var now=DateTimeOffset.UtcNow;
        if(settings.Reminder.Tick(now))TrySave();
        if(!settings.Reminder.Ringing)return;
        if(now-lastReminderSound<TimeSpan.FromSeconds(30))return;
        lastReminderSound=now;
        System.Media.SystemSounds.Exclamation.Play();
        tray?.ShowBalloonTip(8000,T("Minutnik i alarm"),T("Czas minął!"),Forms.ToolTipIcon.Info);
        if(editor==null){ShowReminder();reminderWindow?.Activate();}
    }
    private string DiagnosticReport()
    {
        string Stamp(DateTimeOffset? value)=>value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture)??"—";
        return $"Zegar HTC {typeof(App).Assembly.GetName().Version?.ToString(3)}\n.NET {Environment.Version}\nWindows {Environment.OSVersion.Version}\n\n{T("Stan")}: {weatherState.Status(settings.Language,settings.Use24HourClock,DateTimeOffset.UtcNow)}\n{T("Ostatnia próba")}: {Stamp(weatherState.LastAttempt)}\n{T("Ostatnie powodzenie")}: {Stamp(weatherState.LastSuccess)}\n{T("Następna próba")}: {Stamp(weatherState.NextAttempt)}\n{T("Czas obserwacji")}: {Stamp(weatherState.Snapshot?.Current.ObservedAt)}\n{T("Przyczyna błędu")}: {weatherFailure??"—"}\n{T("Zapis ustawień")}: {(persistenceError==null?"OK":"ERROR")}\n{T("Pamięć pogody")}: {(weatherState.Cached?"cache":"live")}\n";
    }
    private void ShowDiagnostics()
    {
        if(diagnosticsWindow!=null){diagnosticsWindow.Activate();return;}
        diagnosticsWindow=new DiagnosticsWindow(settings.Language,DiagnosticReport) {Owner=this};
        diagnosticsWindow.Closed+=(_,_)=>diagnosticsWindow=null;diagnosticsWindow.Show();
    }
    private void OpenAttribution(object sender, RequestNavigateEventArgs e)
    { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
}

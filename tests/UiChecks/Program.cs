using System.Net;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Zegar;

class Checks
{
    private static int count;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool value,string description)
    { if(!value)throw new Exception("FAIL: "+description);Console.WriteLine("PASS: "+description);count++; }
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private|BindingFlags.Public)!.GetValue(target)!;
    static object? Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private)!.Invoke(target,args);
    [STAThread]
    static void Main(string[] args)
    {
        if(args.Contains("--visual")){VisualChecks.Run();return;}
        if(args.Contains("--capture")){VisualChecks.Capture();return;}
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        string folder=Path.Combine(Path.GetTempPath(),"ZegarUiChecks-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        AppLog.FolderOverride=folder;
        try
        {
            var store=new SettingsStore(Path.Combine(folder,"settings.json"));
            store.Save(new WidgetSettings());
            var startup=new MemoryStartup();
            var handler=new FixtureHandler();
            using var http=new HttpClient(handler);
            var window=new ClockWindow(store,http,new WeatherCacheStore(Path.Combine(folder,"cache")),startup);
            var state=Field<WeatherState>(window,"weatherState");
            var snapshot=new WeatherSnapshot(52.2297,21.0122,new Weather(8,5,3,false,10,DateTimeOffset.UtcNow),new(),"Europe/Warsaw",DateTimeOffset.UtcNow);
            state.Complete(snapshot);
            int ticks=0;
            var timer=Field<DispatcherTimer>(window,"weatherTimer");timer.Interval=TimeSpan.FromMilliseconds(150);timer.Tick+=(_,_)=>ticks++;
            var updater=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
            updater.Tick+=(_,_)=>Call(window,"UpdateStatus");
            timer.Start();updater.Start();Pump(450);timer.Stop();updater.Stop();
            Check(ticks>=1 && handler.Requests>=1,"Rzeczywisty UpdateStatus nie blokuje automatycznego pobrania pogody");

            state.Complete(snapshot);state.Fail("network down");Call(window,"RenderWeather");
            var settings=Field<WidgetSettings>(window,"settings");settings.Locked=true;
            Call(window,"ApplySettings");
            Check(Field<TextBlock>(window,"StatusLabel").Text.StartsWith("Brak połączenia") && state.Offline,"Przełączenie blokady zachowuje komunikat offline");
            Call(window,"ScheduleRefresh");
            Check(timer.Interval==TimeSpan.FromMinutes(2),"Tryb offline zachowuje krótki czas ponowienia");
            int requests=handler.Requests;
            var candidate=settings.Clone();candidate.BackgroundColor="#0080FF";candidate.TextOpacity=.9;
            string? error=Commit(window,candidate,false);
            Check(error==null && state.Snapshot==snapshot && state.Offline && handler.Requests==requests,"Zapis wyglądu offline nie kasuje danych i nie pobiera ich ponownie");
            Check(window.Opacity==1 && Field<Border>(window,"Shell").Background.Opacity==candidate.Opacity,"Przezroczystość tła jest niezależna od całego okna");
            Check(Math.Abs(((SolidColorBrush)window.Resources["ThemePrimary"]).Opacity-.9)<.001,"Oddzielna widoczność czcionki");

            var dialog=new SettingsWindow(candidate,new WeatherService(http),startupEnabled:false);
            var dialogRoot=(FrameworkElement)dialog.Content;dialogRoot.Measure(new Size(620,540));dialogRoot.Arrange(new Rect(0,0,620,540));dialogRoot.UpdateLayout();
            var categories=Field<TabControl>(dialog,"SettingsTabs");
            Check(categories.Template.FindName("PART_SelectedContentHost",categories) is ContentPresenter host && host.Content==((TabItem)categories.SelectedItem).Content,"Zakładki udostępniają zawartość przez standardowy host WPF");
            Field<ComboBox>(dialog,"LanguageInput").SelectedIndex=1;
            Check(((TabItem)categories.Items[0]).Header.ToString()=="Clock" && ((TabItem)categories.Items[1]).Header.ToString()=="Weather" && ((TabItem)categories.Items[2]).Header.ToString()=="Appearance","Kategorie ustawień tłumaczą się na angielski");
            Check(Field<ComboBox>(dialog,"LanguageInput").FontWeight==FontWeights.Normal,"Wybór zakładki nie pogrubia treści formularza");
            dialogRoot.UpdateLayout();var save=Field<Button>(dialog,"SaveButton");var position=save.TranslatePoint(new Point(0,0),dialogRoot);
            Check(position.Y>=0 && position.Y+save.ActualHeight<=dialogRoot.RenderSize.Height && position.X+save.ActualWidth<=dialogRoot.RenderSize.Width,"Przycisk zapisu mieści się w małym oknie niezależnie od przewijania treści");
            Field<ComboBox>(dialog,"LanguageInput").SelectedIndex=0;
            bool closed=false;dialog.Closed+=(_,_)=>closed=true;
            dialog.CommitSettingsAsync=(_,_)=>Task.FromResult<string?>("simulated disk failure");
            Call(dialog,"Save",dialog,new RoutedEventArgs());
            Check(!closed && Field<TextBlock>(dialog,"SaveStatus").Text=="simulated disk failure","Błąd zapisu pozostawia ustawienia otwarte i widoczny komunikat");
            Check(store.Load().BackgroundColor=="#0080FF","Odrzucony zapis okna nie zmienia zapisanych preferencji");
            dialog.Close();

            using(var locked=File.Open(Path.Combine(folder,"settings.json"),FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var unsaved=candidate.Clone();unsaved.City="Nie zapisuj";
                string? failure=Commit(window,unsaved,true);
                Check(failure!=null && !startup.Enabled && store.Load().City!="Nie zapisuj","Blokada pliku wycofuje autostart i odrzuca zmianę konfiguracji");
            }
            Check(Appearance.MinimumContrast(Colors.Black,Colors.Black,1)<1.01 && Appearance.MinimumContrast(Colors.Black,Colors.White,1)>10,"Kontrast uwzględnia rzeczywisty gradient tła");
            var primary=new Rect(0,0,1920,1040);var leftScreen=new Rect(-2560,0,2560,1400);
            var onLeft=new Rect(-2300,100,420,382);
            Check(WindowPlacement.Clamp(onLeft,new[]{primary,leftScreen})==onLeft,"Ujemne współrzędne drugiego monitora są zachowane");
            var recovered=WindowPlacement.Clamp(onLeft,new[]{primary});
            Check(primary.Contains(recovered),"Odłączenie monitora przenosi całe okno do dostępnego obszaru");
            var oversize=WindowPlacement.Clamp(new Rect(4000,0,4000,2500),new[]{primary});
            Check(primary.Contains(oversize),"Duże okno mieści się w mniejszym obszarze roboczym");
            Check(WindowPlacement.Clamp(new Rect(0,0,100,100),Array.Empty<Rect>())==new Rect(0,0,100,100),"Pusta lista ekranów nie powoduje błędu");

            var forecast=new ForecastWindow(candidate,()=>Task.CompletedTask);
            forecast.Update(snapshot,"offline");forecast.Update(snapshot,"offline");forecast.Close();
            Check(Field<StackPanel>(forecast,"days").Children.OfType<TextBlock>().Any(text=>text.Text.Contains("niedostępna")),"Prognoza bez danych dziennych obsługuje pamięć offline");
            Check(Autostart.CommandMatches("\""+typeof(Checks).Assembly.Location+"\"",typeof(Checks).Assembly.Location) && !Autostart.CommandMatches("\"broken",typeof(Checks).Assembly.Location) && !Autostart.CommandMatches(typeof(Checks).Assembly.Location,"missing.exe"),"Autostart sprawdza poprawną ścieżkę i odrzuca wadliwe polecenia");
            var liveSettings=Field<WidgetSettings>(window,"settings");
            var oldCandidate=liveSettings.Clone();liveSettings.Left=300;liveSettings.PhysicalLeft=300;liveSettings.PhysicalTop=80;
            Commit(window,oldCandidate,false);
            Check(store.Load().Left==300 && store.Load().PhysicalLeft==300,"Zapis edytora zachowuje nowszą pozycję monitora");
            var light=Field<WidgetSettings>(window,"settings");light.BackgroundColor="#FFFFFF";light.TextOpacity=.4;Call(window,"ApplySettings");
            var icon=Field<Image>(window,"WeatherIcon");
            var iconPeer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(icon)!;
            Check(Math.Abs(icon.Opacity-.4)<.001 && icon.Source is DrawingImage && !iconPeer.IsControlElement(),"Wektorowa ikona ma jednokrotną przezroczystość i jest dekoracyjna obok opisu");
            var invalidZone=new ForecastWindow(candidate,()=>Task.CompletedTask);
            invalidZone.Update(snapshot with {TimeZone=null!,Forecast=new(){new(DateTimeOffset.UtcNow,0,10,0,0,null,null)}},"test");invalidZone.Close();
            Check(true,"Prognoza toleruje brak strefy w przekazanym modelu");
            var compact=Field<WidgetSettings>(window,"settings");compact.Compact=true;compact.Use24HourClock=false;compact.TemperatureUnit="F";Call(window,"ApplySettings");
            Check(window.Width==320*compact.Scale && window.Height==252*compact.Scale && Field<TextBlock>(window,"PeriodLabel").Visibility==Visibility.Collapsed && Field<Button>(window,"ForecastButton").Visibility==Visibility.Collapsed && iconPeer.IsControlElement(),"Tryb kompaktowy zmniejsza okno i zachowuje opis ikony dla czytnika ekranu");
            Check(Field<TextBlock>(window,"LockMark").Text.Contains(ClockFormat.Period(DateTime.Now,false)),"Tryb kompaktowy zachowuje AM/PM w nagłówku");
            var collections=new SettingsWindow(compact,new WeatherService(http),startupEnabled:false);
            Call(collections,"AddFavorite",collections,new RoutedEventArgs());
            Check(Field<ComboBox>(collections,"FavoritesInput").Items.Count==1 && compact.Favorites.Count==0,"Ulubione są edytowane w lokalnej kopii przed zapisem");
            Field<TextBox>(collections,"ProfileNameInput").Text="Test";Call(collections,"SaveProfile",collections,new RoutedEventArgs());
            Check(Field<ComboBox>(collections,"ProfilesInput").Items.Count==1 && compact.Profiles.Count==0,"Profile nie zapisują się po anulowaniu edytora");collections.Close();
            compact.Reminder.StartTimer(5,DateTimeOffset.UtcNow);var oldReminder=compact.Reminder.DueAt;
            var staleReminderCandidate=compact.Clone();staleReminderCandidate.Reminder.Stop();Commit(window,staleReminderCandidate,false);
            Check(Field<WidgetSettings>(window,"settings").Reminder.DueAt==oldReminder,"Zapis preferencji nie nadpisuje działającego minutnika");
            Check(!((string)Call(window,"DiagnosticReport")!).Contains(compact.City),"Raport diagnostyczny nie zawiera miasta");
            window.Close();

            var blockingStartup=new BlockingStartup();
            var asyncStore=new SettingsStore(Path.Combine(folder,"async-settings.json"));asyncStore.Save(new());
            using(var asyncHttp=new HttpClient(new FixtureHandler()))
            {
                var asyncWindow=new ClockWindow(asyncStore,asyncHttp,new WeatherCacheStore(Path.Combine(folder,"async-cache")),blockingStartup);
                var asyncCandidate=Field<WidgetSettings>(asyncWindow,"settings").Clone();asyncCandidate.BackgroundColor="#ABCDEF";
                var committing=(Task<string?>)Call(asyncWindow,"CommitSettingsAsync",asyncCandidate,true)!;
                PumpUntil(()=>blockingStartup.Entered.IsSet);
                var geometry=Field<WidgetSettings>(asyncWindow,"settings");geometry.Left=444;geometry.PhysicalLeft=444;geometry.PhysicalTop=80;
                geometry.Reminder.StartTimer(5,DateTimeOffset.UtcNow);Call(asyncWindow,"TrySave");
                blockingStartup.Release.Set();PumpUntil(()=>committing.IsCompleted);
                PumpUntil(()=>asyncStore.Load().Left==444);
                var committed=asyncStore.Load();
                Check(committing.Result==null && committed.BackgroundColor=="#ABCDEF" && committed.PhysicalLeft==444 && committed.Reminder.DueAt!=null,"Zapis w tle łączy preferencje z geometrią i minutnikiem zmienionymi podczas zapisu");
                using(var locked=File.Open(Path.Combine(folder,"async-settings.json"),FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    var rejected=Field<WidgetSettings>(asyncWindow,"settings").Clone();rejected.BackgroundColor="#000000";
                    var rejectedTask=(Task<string?>)Call(asyncWindow,"CommitSettingsAsync",rejected,false)!;PumpUntil(()=>rejectedTask.IsCompleted);
                    Check(rejectedTask.Result!=null && blockingStartup.Enabled && asyncStore.Load().BackgroundColor=="#ABCDEF","Produkcyjny zapis w tle wycofuje autostart po błędzie pliku");
                }
                asyncWindow.Close();
            }
            AppLog.Write("privacy-check",new Exception("Warszawa SECRET_TEST"));AppLog.FlushAsync().GetAwaiter().GetResult();
            Check(!File.ReadAllText(AppLog.LogPath).Contains("SECRET_TEST") && !File.ReadAllText(AppLog.LogPath).Contains("Warszawa"),"Log diagnostyczny pomija treść wyjątków i dane lokalizacji");

            using(var racingHttp=new HttpClient(new RacingHandler()))
            {
                var racingStore=new SettingsStore(Path.Combine(folder,"racing.json"));
                var racingWindow=new ClockWindow(racingStore,racingHttp,new WeatherCacheStore(Path.Combine(folder,"racecache")),new MemoryStartup());
                var pending=(Task)Call(racingWindow,"RefreshWeatherAsync")!;
                var racedSettings=Field<WidgetSettings>(racingWindow,"settings");racedSettings.Latitude=51;
                var newest=(Task)Call(racingWindow,"RefreshWeatherAsync")!;Pump(200);
                Check(newest.IsCompletedSuccessfully && pending.IsCompletedSuccessfully && Field<WeatherState>(racingWindow,"weatherState").Snapshot?.Latitude==51,"Opóźniona odpowiedź poprzedniej lokalizacji nie nadpisuje nowszej");racingWindow.Close();
            }

            string pipe="ZegarChecks-"+Guid.NewGuid().ToString("N");int activations=0;
            using var stop=new CancellationTokenSource();
            var listener=InstanceActivation.ListenAsync(()=>{Interlocked.Increment(ref activations);return Task.CompletedTask;},stop.Token,pipe);
            bool first=InstanceActivation.SendShowAsync(pipe).GetAwaiter().GetResult();
            bool second=InstanceActivation.SendShowAsync(pipe).GetAwaiter().GetResult();
            stop.Cancel();listener.GetAwaiter().GetResult();
            Check(first && second && activations==2,"Druga instancja przesyła bezpieczne żądanie pokazania okna");
            Console.WriteLine($"Wynik: {count} sprawdzeń WPF OK. Bez pokazywania okien i bez zmian rejestru.");
        }
        finally { AppLog.FlushAsync().GetAwaiter().GetResult();Directory.Delete(folder,true);AppLog.FolderOverride=null; }
    }
    static void Pump(int milliseconds)
    {
        var frame=new DispatcherFrame();var finish=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milliseconds)};
        finish.Tick+=(_,_)=>frame.Continue=false;finish.Start();Dispatcher.PushFrame(frame);finish.Stop();
    }
    static void PumpUntil(Func<bool> condition)
    {
        var timeout=System.Diagnostics.Stopwatch.StartNew();
        while(!condition()) {if(timeout.Elapsed>TimeSpan.FromSeconds(5))throw new TimeoutException("Dispatcher condition");Pump(20);}
    }
    static string? Commit(ClockWindow window,WidgetSettings settings,bool startup)
    {
        var task=(Task<string?>)Call(window,"CommitSettingsAsync",settings,startup)!;
        PumpUntil(()=>task.IsCompleted);return task.GetAwaiter().GetResult();
    }
}
sealed class BlockingStartup:IStartupSettings
{
    public readonly ManualResetEventSlim Entered=new(),Release=new();
    public string? Command {get;private set;}
    public bool Enabled=>Command!=null;
    public void Set(bool enabled){Entered.Set();if(!Release.Wait(TimeSpan.FromSeconds(5)))throw new IOException("test timeout");Command=enabled?"test.exe":null;}
    public void Restore(string? command)=>Command=command;
}
sealed class RacingHandler:HttpMessageHandler
{
    private int requests;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        int index=Interlocked.Increment(ref requests);await Task.Delay(index==1?100:10);
        return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("""{"timezone":"Europe/Warsaw","current":{"temperature_2m":8,"apparent_temperature":5,"weather_code":3,"is_day":0,"wind_speed_10m":10,"time":1792886400}}""")};
    }
}
sealed class MemoryStartup : IStartupSettings
{
    public string? Command {get;private set;}
    public bool Enabled=>Command!=null;
    public void Set(bool enabled)=>Command=enabled?"test.exe":null;
    public void Restore(string? command)=>Command=command;
}
sealed class FixtureHandler : HttpMessageHandler
{
    public int Requests;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("""{"timezone":"Europe/Warsaw","current":{"temperature_2m":8,"apparent_temperature":5,"weather_code":3,"is_day":0,"wind_speed_10m":10,"time":1792886400}}""")});
    }
}

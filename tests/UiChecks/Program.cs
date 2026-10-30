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
    static void Main()
    {
        string folder=Path.Combine(Path.GetTempPath(),"ZegarUiChecks-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
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
            string? error=(string?)Call(window,"CommitSettings",candidate,false);
            Check(error==null && state.Snapshot==snapshot && state.Offline && handler.Requests==requests,"Zapis wyglądu offline nie kasuje danych i nie pobiera ich ponownie");
            Check(window.Opacity==1 && Field<Border>(window,"Shell").Background.Opacity==candidate.Opacity,"Przezroczystość tła jest niezależna od całego okna");
            Check(Math.Abs(((SolidColorBrush)window.Resources["ThemePrimary"]).Opacity-.9)<.001,"Oddzielna widoczność czcionki");

            var dialog=new SettingsWindow(candidate,new WeatherService(http),startupEnabled:false);
            bool closed=false;dialog.Closed+=(_,_)=>closed=true;
            dialog.CommitSettings=(_,_)=>"simulated disk failure";
            Call(dialog,"Save",dialog,new RoutedEventArgs());
            Check(!closed && Field<TextBlock>(dialog,"SaveStatus").Text=="simulated disk failure","Błąd zapisu pozostawia ustawienia otwarte i widoczny komunikat");
            Check(store.Load().BackgroundColor=="#0080FF","Odrzucony zapis okna nie zmienia zapisanych preferencji");
            dialog.Close();

            using(var locked=File.Open(Path.Combine(folder,"settings.json"),FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var unsaved=candidate.Clone();unsaved.City="Nie zapisuj";
                string? failure=(string?)Call(window,"CommitSettings",unsaved,true);
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
            window.Close();

            string pipe="ZegarChecks-"+Guid.NewGuid().ToString("N");int activations=0;
            using var stop=new CancellationTokenSource();
            var listener=InstanceActivation.ListenAsync(()=>{Interlocked.Increment(ref activations);return Task.CompletedTask;},stop.Token,pipe);
            bool first=InstanceActivation.SendShowAsync(pipe).GetAwaiter().GetResult();
            bool second=InstanceActivation.SendShowAsync(pipe).GetAwaiter().GetResult();
            stop.Cancel();listener.GetAwaiter().GetResult();
            Check(first && second && activations==2,"Druga instancja przesyła bezpieczne żądanie pokazania okna");
            Console.WriteLine($"Wynik: {count} sprawdzeń WPF OK. Bez pokazywania okien i bez zmian rejestru.");
        }
        finally { Directory.Delete(folder,true); }
    }
    static void Pump(int milliseconds)
    {
        var frame=new DispatcherFrame();var finish=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milliseconds)};
        finish.Tick+=(_,_)=>frame.Continue=false;finish.Start();Dispatcher.PushFrame(frame);finish.Stop();
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

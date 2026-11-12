using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using Zegar;

internal static class VisualChecks
{
    public static void Capture()
    {
        string output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../test-results/ui"));Directory.CreateDirectory(output);
        _ = new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        using var http=new HttpClient(new PreviewHandler());
        var settings=new WidgetSettings();
        var editor=new SettingsWindow(settings,new WeatherService(http),startupEnabled:false);
        void Render(Window window,string name,double width,double height)
        {
            var root=(FrameworkElement)window.Content;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)width,(int)height,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            var background=new System.Windows.Media.DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(window.Background,null,new Rect(0,0,width,height));
            bitmap.Render(background);bitmap.Render(root);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream=File.Create(Path.Combine(output,name+".png"));encoder.Save(stream);
        }
        var tabs=(TabControl)editor.FindName("SettingsTabs");
        Render(editor,"settings-clock",720,760);tabs.SelectedIndex=1;Render(editor,"settings-weather",720,760);tabs.SelectedIndex=2;Render(editor,"settings-appearance",720,760);
        ((ComboBox)editor.FindName("LanguageInput")).SelectedIndex=1;Render(editor,"settings-small-en",620,540);editor.Close();
        var reminder=new ReminderWindow("pl",()=>settings.Reminder,()=>{});Render(reminder,"reminder",520,660);reminder.Close();
        var forecast=new ForecastWindow(settings,()=>Task.CompletedTask);
        var snapshot=new WeatherSnapshot(52,21,new Weather(8,5,3,false,10,DateTimeOffset.UtcNow),new(){new(DateTimeOffset.UtcNow,2,12,30,2,null,null)},"Europe/Warsaw",DateTimeOffset.UtcNow);
        forecast.Update(snapshot,"Dane demonstracyjne");Render(forecast,"forecast",640,680);forecast.Close();
        Console.WriteLine("UI captures: "+output);
    }
    // Interactive smoke test: real windows, isolated preferences, fixed HTTP data, no registry writes.
    public static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"ZegarVisual-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        AppLog.FolderOverride=folder;
        try
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
            string source=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../App.xaml"));
            if(File.Exists(source))
            {
                var xml=XDocument.Load(source);XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                foreach(var style in xml.Descendants(ns+"Style"))
                    if(XamlReader.Parse(style.ToString()) is Style value) app.Resources[value.TargetType]=value;
            }
            var settings=new WidgetSettings {City="Warszawa",Left=100,Top=100};
            settings.Favorites.Add(new Place("Gliwice","Polska","Śląskie",50.3,18.6));
            settings.Profiles.Add(AppearanceProfile.Capture("Noc",settings));
            var store=new SettingsStore(Path.Combine(folder,"settings.json"));store.Save(settings);
            var window=new ClockWindow(store,new HttpClient(new PreviewHandler()),new WeatherCacheStore(Path.Combine(folder,"cache")),new MemoryStartup()) {ShowInTaskbar=true};
            app.Run(window);
        }
        finally {AppLog.FlushAsync().GetAwaiter().GetResult();Directory.Delete(folder,true);AppLog.FolderOverride=null;}
    }
}
internal sealed class PreviewHandler:HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        var now=DateTimeOffset.UtcNow;
        if(request.RequestUri!.Host.StartsWith("geocoding"))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("""{"results":[{"name":"Kraków","country":"Polska","latitude":50.06,"longitude":19.94}]}""")});
        var snapshot=new {
            timezone="Europe/Warsaw",
            current=new {temperature_2m=8,apparent_temperature=5,weather_code=3,is_day=0,wind_speed_10m=10,time=now.ToUnixTimeSeconds()},
            daily=new {time=Enumerable.Range(0,3).Select(i=>now.AddDays(i).ToUnixTimeSeconds()).ToArray(),temperature_2m_min=new[]{2,3,5},temperature_2m_max=new[]{10,12,11},precipitation_probability_max=new[]{20,50,30},weather_code=new[]{3,61,2},sunrise=Enumerable.Range(0,3).Select(i=>now.AddDays(i).ToUnixTimeSeconds()).ToArray(),sunset=Enumerable.Range(0,3).Select(i=>now.AddDays(i).AddHours(8).ToUnixTimeSeconds()).ToArray()},
            hourly=new {time=Enumerable.Range(0,48).Select(i=>now.AddHours(i).ToUnixTimeSeconds()).ToArray(),temperature_2m=Enumerable.Repeat(8,48).ToArray(),precipitation_probability=Enumerable.Repeat(20,48).ToArray(),weather_code=Enumerable.Repeat(3,48).ToArray(),wind_speed_10m=Enumerable.Repeat(10,48).ToArray()}
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(snapshot))});
    }
}

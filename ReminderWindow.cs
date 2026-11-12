using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Zegar;

internal sealed class ReminderWindow : Window
{
    private readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromSeconds(1)};
    internal TextBox MinutesInput {get;}=new() {Text="25",MaxLength=4,Margin=new Thickness(0,6,0,10)};
    internal TextBox AlarmInput {get;}=new() {Text=DateTime.Now.AddHours(1).ToString("HH:mm",CultureInfo.InvariantCulture),MaxLength=5,Margin=new Thickness(0,6,0,10)};
    public ReminderWindow(string language,Func<ReminderState> state,Action changed)
    {
        string T(string key)=>UiText.Get(key,language);
        Title=T("Minutnik i alarm");Width=520;Height=700;MinWidth=440;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Icon=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Zegar;component/assets/Zegar.ico"));
        UiTheme.Apply(this);
        var panel=new StackPanel {Margin=new Thickness(24)};
        panel.Children.Add(UiTheme.Text(T("Minutnik i alarm"),28,strong:true));
        var subtitle=UiTheme.Text(T("Twój czas pod kontrolą"),14,"UiMuted");subtitle.Margin=new Thickness(0,4,0,20);panel.Children.Add(subtitle);
        var current=UiTheme.Text(T("Brak aktywnego przypomnienia"),14,"UiMuted");
        var countdown=UiTheme.Text("—",38,strong:true);countdown.FontFamily=new System.Windows.Media.FontFamily("Consolas");countdown.Margin=new Thickness(0,6,0,6);
        var due=UiTheme.Text("",12,"UiMuted");
        var summary=new StackPanel();summary.Children.Add(current);summary.Children.Add(countdown);summary.Children.Add(due);panel.Children.Add(UiTheme.Card(summary));
        var error=UiTheme.Text("",14,"UiDanger");error.Margin=new Thickness(0,6,0,8);
        var alarmError=UiTheme.Text("",14,"UiDanger");alarmError.Margin=new Thickness(0,6,0,8);
        error.SetResourceReference(FrameworkElement.StyleProperty,"FormError");alarmError.SetResourceReference(FrameworkElement.StyleProperty,"FormError");
        Button? snooze=null,stop=null;
        void Update()
        {
            var reminder=state();current.Text=reminder.Ringing?T("Czas minął!"):reminder.DueAt==null?T("Brak aktywnego przypomnienia"):T(reminder.Kind=="alarm"?"Alarm":"Minutnik");
            countdown.Text=reminder.Remaining(DateTimeOffset.UtcNow);
            countdown.Visibility=reminder.DueAt==null?Visibility.Collapsed:Visibility.Visible;
            countdown.SetResourceReference(TextBlock.ForegroundProperty,reminder.Ringing?"UiAccent":"UiText");
            due.Text=reminder.DueAt==null?T("Ustaw minutnik lub wybierz godzinę alarmu."):reminder.DueAt.Value.LocalDateTime.ToString("ddd, d MMM · HH:mm",UiText.Culture(language));
            if(snooze!=null){snooze.IsEnabled=reminder.DueAt!=null;snooze.Visibility=reminder.DueAt==null?Visibility.Collapsed:Visibility.Visible;}
            if(stop!=null){stop.IsEnabled=reminder.DueAt!=null;stop.Visibility=reminder.DueAt==null?Visibility.Collapsed:Visibility.Visible;}
        }
        var actions=new WrapPanel();summary.Children.Add(actions);actions.Margin=new Thickness(0,16,0,0);
        Button ActionButton(string label,Action action,Panel parent,bool primary=false)
        {
            var button=UiTheme.Button(T(label),primary);button.HorizontalAlignment=HorizontalAlignment.Left;
            button.Click+=(_,_)=>{action();Update();};parent.Children.Add(button);return button;
        }
        snooze=ActionButton("Odłóż o 5 minut",()=>{if(state().DueAt==null)return;state().Snooze(DateTimeOffset.UtcNow);changed();},actions);
        stop=ActionButton("Zatrzymaj / wycisz",()=>{state().Stop();changed();},actions);
        var timerPanel=new StackPanel();timerPanel.Children.Add(UiTheme.Text(T("Minutnik"),17,strong:true));
        var alarmPanel=new StackPanel();alarmPanel.Children.Add(UiTheme.Text(T("Alarm"),17,strong:true));
        panel.Children.Add(UiTheme.Card(timerPanel));panel.Children.Add(UiTheme.Card(alarmPanel));
        timerPanel.Children.Add(new TextBlock {Text=T("Minuty (1–1440)"),Margin=new Thickness(0,12,0,0)});timerPanel.Children.Add(MinutesInput);
        timerPanel.Children.Add(error);MinutesInput.TextChanged+=(_,_)=>error.Text="";
        System.Windows.Automation.AutomationProperties.SetName(MinutesInput,T("Minuty (1–1440)"));
        ActionButton("Uruchom minutnik",()=>{if(!int.TryParse(MinutesInput.Text,out int value)||value is <1 or >1440){error.Text=T("Wpisz od 1 do 1440 minut.");MinutesInput.Focus();return;}state().StartTimer(value,DateTimeOffset.UtcNow);error.Text="";changed();},timerPanel,true);
        alarmPanel.Children.Add(new TextBlock {Text=T("Godzina alarmu (HH:mm, czas Windows)"),Margin=new Thickness(0,12,0,0)});alarmPanel.Children.Add(AlarmInput);
        alarmPanel.Children.Add(alarmError);AlarmInput.TextChanged+=(_,_)=>alarmError.Text="";
        System.Windows.Automation.AutomationProperties.SetName(AlarmInput,T("Godzina alarmu (HH:mm, czas Windows)"));
        ActionButton("Ustaw alarm",()=>
        {
            if(!TimeOnly.TryParseExact(AlarmInput.Text,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)){alarmError.Text=T("Wpisz godzinę w formacie HH:mm.");AlarmInput.Focus();return;}
            try {state().StartAlarm(time,DateTime.Now,TimeZoneInfo.Local);alarmError.Text="";changed();}
            catch(ArgumentException){alarmError.Text=T("Ta godzina nie istnieje przy zmianie czasu.");}
        },alarmPanel);
        panel.Children.Add(UiTheme.Text(T("Nowy termin zastępuje poprzedni. Alarm działa, gdy aplikacja jest uruchomiona; po uśpieniu zostanie pokazany przy wznowieniu."),12,"UiMuted"));
        Content=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        timer.Tick+=(_,_)=>Update();Loaded+=(_,_)=>timer.Start();Closed+=(_,_)=>timer.Stop();Update();
    }
}

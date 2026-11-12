using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Zegar;

internal sealed class DiagnosticsWindow : Window
{
    private readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromSeconds(1)};
    public DiagnosticsWindow(string language, Func<string> report)
    {
        string T(string key)=>UiText.Get(key,language);
        Title=T("Diagnostyka");Width=680;Height=600;MinWidth=520;MinHeight=430;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Icon=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Zegar;component/assets/Zegar.ico"));
        UiTheme.Apply(this);
        var panel=new DockPanel {Margin=new Thickness(24)};
        var header=new StackPanel {Margin=new Thickness(0,0,0,20)};header.Children.Add(UiTheme.Text(T("Diagnostyka"),28,strong:true));
        header.Children.Add(UiTheme.Text(T("Stan połączenia i ostatnie aktualizacje"),14,"UiMuted"));DockPanel.SetDock(header,Dock.Top);panel.Children.Add(header);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);panel.Children.Add(footer);
        var privacy=UiTheme.Text(T("Eksport nie zawiera miasta, współrzędnych ani treści odpowiedzi API."),12,"UiMuted");privacy.Margin=new Thickness(0,12,0,16);footer.Children.Add(privacy);
        var export=UiTheme.Button(T("Eksportuj raport…"),true);export.HorizontalAlignment=HorizontalAlignment.Left;
        var status=UiTheme.Text("");status.Margin=new Thickness(0,8,0,0);
        footer.Children.Add(export);footer.Children.Add(status);
        var details=new TextBox {Text=report(),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(18),FontSize=14};panel.Children.Add(details);
        System.Windows.Automation.AutomationProperties.SetName(details,T("Raport diagnostyczny"));
        export.Click+=async (_,_)=>
        {
            var dialog=new SaveFileDialog {Filter="Text (*.txt)|*.txt",FileName="Zegar-diagnostics.txt"};
            if(dialog.ShowDialog(this)!=true) return;
            export.IsEnabled=false;
            try {await File.WriteAllTextAsync(dialog.FileName,report()+Environment.NewLine+await AppLog.ReadAsync());status.Text=T("Raport zapisany.");}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {status.Text=T("Nie udało się zapisać raportu.");AppLog.Write("diagnostics-export",ex);}
            finally {export.IsEnabled=true;}
        };
        timer.Tick+=(_,_)=>{if(details.Text!=report()) details.Text=report();};
        Loaded+=(_,_)=>timer.Start();Closed+=(_,_)=>timer.Stop();Content=panel;
    }
}

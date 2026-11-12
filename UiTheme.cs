using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zegar;

internal static class UiTheme
{
    public static void Apply(Window window)
    {
        bool loaded=false;
        foreach(var dictionary in window.Resources.MergedDictionaries)if(dictionary.Source?.OriginalString.EndsWith("UiTheme.xaml",StringComparison.Ordinal)==true)loaded=true;
        if(!loaded)window.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("pack://application:,,,/Zegar;component/UiTheme.xaml")});
        window.SetResourceReference(Control.BackgroundProperty,"UiBackground");
        window.SetResourceReference(Control.ForegroundProperty,"UiText");
        window.FontFamily=new FontFamily("Segoe UI");window.FontSize=14;
        window.UseLayoutRounding=true;window.SnapsToDevicePixels=true;
        void Update()
        {
            if(!SystemParameters.HighContrast) {foreach(string key in new[]{"UiBackground","UiSurface","UiText","UiMuted","UiBorder","UiInputBorder","UiAccent","UiAccentHover","UiSoft","UiHover","UiOnAccent","UiDanger","UiFocus"})window.Resources.Remove(key);return;}
            foreach(string key in new[]{"UiBackground","UiSurface","UiSoft","UiHover"}) window.Resources[key]=SystemColors.WindowBrush;
            foreach(string key in new[]{"UiText","UiMuted","UiBorder","UiInputBorder","UiDanger"})window.Resources[key]=SystemColors.WindowTextBrush;
            foreach(string key in new[]{"UiAccent","UiAccentHover","UiFocus"})window.Resources[key]=SystemColors.HighlightBrush;
            window.Resources["UiOnAccent"]=SystemColors.HighlightTextBrush;
        }
        PropertyChangedEventHandler handler=(_,e)=>{if(e.PropertyName==nameof(SystemParameters.HighContrast))_ = window.Dispatcher.InvokeAsync(Update);};
        SystemParameters.StaticPropertyChanged+=handler;window.Closed+=(_,_)=>SystemParameters.StaticPropertyChanged-=handler;Update();
    }
    public static TextBlock Text(string value,double size=14,string brush="UiText",bool strong=false)
    {
        var text=new TextBlock {Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,FontWeight=strong?FontWeights.SemiBold:FontWeights.Normal};
        text.SetResourceReference(TextBlock.ForegroundProperty,brush);return text;
    }
    public static Border Card(UIElement content)
    {
        var border=new Border {Child=content};border.SetResourceReference(FrameworkElement.StyleProperty,"SurfaceCard");return border;
    }
    public static Button Button(string label,bool primary=false)
    {
        var button=new Button {Content=label};if(primary)button.SetResourceReference(FrameworkElement.StyleProperty,"PrimaryButton");return button;
    }
}

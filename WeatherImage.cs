using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Zegar;

public sealed class WeatherImage : Image
{
    public static readonly DependencyProperty DecorativeProperty=DependencyProperty.Register(nameof(Decorative),typeof(bool),typeof(WeatherImage),new PropertyMetadata(false,OnDecorationChanged));
    public bool Decorative {get=>(bool)GetValue(DecorativeProperty);set=>SetValue(DecorativeProperty,value);}
    private static void OnDecorationChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
        => UIElementAutomationPeer.FromElement((WeatherImage)sender)?.InvalidatePeer();
    protected override AutomationPeer OnCreateAutomationPeer()=>new WeatherImagePeer(this);
    private sealed class WeatherImagePeer : ImageAutomationPeer
    {
        public WeatherImagePeer(WeatherImage owner):base(owner){}
        protected override bool IsControlElementCore()=>!((WeatherImage)Owner).Decorative;
        protected override bool IsContentElementCore()=>!((WeatherImage)Owner).Decorative;
    }
}

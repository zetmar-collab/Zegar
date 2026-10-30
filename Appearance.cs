using System;
using System.Windows.Media;
using System.Windows;
using System.Linq;

namespace Zegar;

internal static class Appearance
{
    public static bool IsLight(Color color)
    {
        static double Linear(byte value) => value / 255.0 <= .04045 ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + .055) / 1.055, 2.4);
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B) > .179;
    }
    public static Color TextColor(string background, string selected) =>
        !string.IsNullOrEmpty(selected) ? (Color)ColorConverter.ConvertFromString(selected) :
        MinimumContrast((Color)ColorConverter.ConvertFromString(background), Color.FromRgb(20,25,34), 1) >= MinimumContrast((Color)ColorConverter.ConvertFromString(background), Color.FromRgb(237,243,252), 1) ? Color.FromRgb(20,25,34) : Color.FromRgb(237,243,252);
    private static Color Blend(Color a, Color b, double amount) => Color.FromRgb((byte)(a.R+(b.R-a.R)*amount), (byte)(a.G+(b.G-a.G)*amount), (byte)(a.B+(b.B-a.B)*amount));
    public static LinearGradientBrush Background(Color color) => new(new GradientStopCollection
    { new(Blend(color,Colors.White,.13),0), new(color,.55), new(Blend(color,Colors.Black,.3),1) }, new Point(0,0), new Point(.7,1));
    public static double MinimumContrast(Color background, Color foreground, double opacity)
    {
        static double L(Color c)
        {
            static double C(byte v) => v/255.0 <= .04045 ? v/255.0/12.92 : Math.Pow((v/255.0+.055)/1.055,2.4);
            return .2126*C(c.R)+.7152*C(c.G)+.0722*C(c.B);
        }
        return Background(background).GradientStops.Select(stop =>
        {
            double a=L(stop.Color), b=L(Blend(stop.Color,foreground,opacity));
            return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
        }).Min();
    }
}

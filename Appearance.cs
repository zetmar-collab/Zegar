using System;
using System.Windows.Media;

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
        IsLight((Color)ColorConverter.ConvertFromString(background)) ? Color.FromRgb(20, 25, 34) : Color.FromRgb(237, 243, 252);
}

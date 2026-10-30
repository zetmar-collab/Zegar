using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Zegar;

internal static class WindowPlacement
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    private static IntPtr Handle(Window window) => new WindowInteropHelper(window).Handle;
    public static Point? Position(Window window) => GetWindowRect(Handle(window), out var rect) ? new Point(rect.Left, rect.Top) : null;
    public static void Restore(Window window, double? left, double? top)
    {
        if (left.HasValue && top.HasValue) SetWindowPos(Handle(window), IntPtr.Zero, (int)left.Value, (int)top.Value, 0, 0, 0x0015);
    }
    public static Rect Clamp(Rect bounds, Rect[] workAreas)
    {
        if (workAreas.Length == 0) return bounds;
        if (workAreas.Any(area => area.Contains(bounds))) return bounds;
        double Intersection(Rect area) { var overlap = Rect.Intersect(bounds, area); return overlap.IsEmpty ? 0 : overlap.Width * overlap.Height; }
        double Distance(Rect area)
        {
            double x = Math.Clamp(bounds.Left, area.Left, area.Right), y = Math.Clamp(bounds.Top, area.Top, area.Bottom);
            return Math.Pow(x-bounds.Left,2)+Math.Pow(y-bounds.Top,2);
        }
        var target = workAreas.OrderByDescending(Intersection).ThenBy(Distance).First();
        double width = Math.Min(bounds.Width, target.Width), height = Math.Min(bounds.Height, target.Height);
        return new Rect(Math.Clamp(bounds.Left,target.Left,target.Right-width), Math.Clamp(bounds.Top,target.Top,target.Bottom-height),width,height);
    }
    public static bool EnsureVisible(Window window)
    {
        IntPtr hwnd = Handle(window);
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd,out var native)) return false;
        var bounds = new Rect(native.Left,native.Top,native.Right-native.Left,native.Bottom-native.Top);
        var areas = Forms.Screen.AllScreens.Select(s => new Rect(s.WorkingArea.X,s.WorkingArea.Y,s.WorkingArea.Width,s.WorkingArea.Height)).ToArray();
        var clamped = Clamp(bounds,areas);
        if (clamped == bounds) return false;
        double factor = Math.Min(clamped.Width / Math.Max(1,bounds.Width),clamped.Height / Math.Max(1,bounds.Height));
        if (factor < 1 && window.Content is FrameworkElement root && root.LayoutTransform is ScaleTransform scale)
        {
            root.LayoutTransform = new ScaleTransform(scale.ScaleX*factor,scale.ScaleY*factor);
            window.Width *= factor; window.Height *= factor;
        }
        // All rectangles are physical pixels. Never apply one monitor's DPI transform to another.
        SetWindowPos(hwnd,IntPtr.Zero,(int)clamped.Left,(int)clamped.Top,0,0,0x0015);
        return true;
    }
    public static bool IsButton(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is ButtonBase) return true;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return false;
    }
}

using System;
using System.IO;
using Microsoft.Win32;

namespace Zegar;

public static class Autostart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "ZegarHTC";
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue(Name) is string; }
    }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled)
            key.SetValue(Name, "\"" + (Environment.ProcessPath ?? throw new IOException("Nie znaleziono aplikacji.")) + "\"");
        else key.DeleteValue(Name, false);
    }
}

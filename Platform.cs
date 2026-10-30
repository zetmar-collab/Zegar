using System;
using System.IO;
using Microsoft.Win32;

namespace Zegar;

internal sealed class WindowsStartupSettings : IStartupSettings
{
    public string? Command => Autostart.Command;
    public bool Enabled => Autostart.Enabled;
    public void Set(bool enabled) => Autostart.Set(enabled);
    public void Restore(string? command) => Autostart.Restore(command);
}

public static class Autostart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "ZegarHTC";
    public static bool Enabled
    {
        get => CommandMatches(Command,Environment.ProcessPath);
    }
    internal static bool CommandMatches(string? command,string? executable)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(executable)) return false;
            string path = command.Trim();
            if (path.StartsWith('"')) { int end = path.IndexOf('"', 1); if (end < 0) return false; path = path.Substring(1, end - 1); }
            return File.Exists(path) && string.Equals(Path.GetFullPath(path),Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { return false; }
    }
    public static string? Command { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue(Name) as string; } }
    public static void Restore(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (command == null) key.DeleteValue(Name, false); else key.SetValue(Name, command);
    }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled)
            key.SetValue(Name, "\"" + (Environment.ProcessPath ?? throw new IOException("Nie znaleziono aplikacji.")) + "\"");
        else key.DeleteValue(Name, false);
    }
}

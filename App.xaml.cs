using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Zegar;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // The separate restart shortcut can recover even an unresponsive UI.
        if (e.Args.Contains("--restart"))
        {
            string? ownPath = Environment.ProcessPath;
            foreach (var process in Process.GetProcessesByName("Zegar"))
            {
                using (process)
                {
                    if (process.Id == Environment.ProcessId) continue;
                    try
                    {
                        if (string.Equals(process.MainModule?.FileName, ownPath, StringComparison.OrdinalIgnoreCase))
                        { process.Kill(); process.WaitForExit(4000); }
                    }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
                }
            }
        }
        instance = new Mutex(true, @"Local\Marek.Zegar.HTC.Widget", out bool created);
        if (!created) { Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Nie udało się wykonać operacji. " + args.Exception.Message, "Zegar", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        MainWindow = new ClockWindow();
        if (e.Args.Contains("--inspect")) MainWindow.ShowInTaskbar = true;
        MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}

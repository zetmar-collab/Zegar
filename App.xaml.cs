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
    private readonly CancellationTokenSource activationStop = new();
    protected override async void OnStartup(StartupEventArgs e)
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
        if (!created)
        {
            if (!await InstanceActivation.SendShowAsync())
            {
                string language = new SettingsStore().Load().Language;
                MessageBox.Show(UiText.Get("Zegar już działa, ale nie odpowiada. Użyj skrótu Restart zegara HTC.",language),UiText.Get("Zegar HTC",language));
            }
            Shutdown(); return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            var language = new SettingsStore().Load().Language;
            MessageBox.Show(UiText.Get("Nie udało się wykonać operacji. ", language) + args.Exception.Message, UiText.Get("Zegar HTC", language), MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        MainWindow = new ClockWindow();
        if (e.Args.Contains("--inspect")) MainWindow.ShowInTaskbar = true;
        MainWindow.Show();
        _ = InstanceActivation.ListenAsync(() => Dispatcher.InvokeAsync(() => ((ClockWindow)MainWindow).ShowWidget()).Task,activationStop.Token);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        activationStop.Cancel(); instance?.Dispose(); activationStop.Dispose();
        base.OnExit(e);
    }
}

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
        AppLog.Write("startup");
        AppDomain.CurrentDomain.UnhandledException+=(_,args)=>AppLog.Write("fatal",args.ExceptionObject as Exception);
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
            AppLog.Write("ui-unhandled",args.Exception);
            bool recoverable=args.Exception is IOException or UnauthorizedAccessException or OperationCanceledException;
            MessageBox.Show(UiText.Get(recoverable?"Nie udało się wykonać operacji. Spróbuj ponownie.":"Nieoczekiwany błąd. Aplikacja zostanie zamknięta. Szczegóły zapisano w lokalnym logu.",language), UiText.Get("Zegar HTC", language), MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
            if(!recoverable)Shutdown(1);
        };
        MainWindow = new ClockWindow();
        if (e.Args.Contains("--inspect")) MainWindow.ShowInTaskbar = true;
        MainWindow.Show();
        _ = InstanceActivation.ListenAsync(() => Dispatcher.InvokeAsync(() => ((ClockWindow)MainWindow).ShowWidget()).Task,activationStop.Token);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        activationStop.Cancel(); instance?.Dispose(); activationStop.Dispose();
        AppLog.Write("shutdown");AppLog.FlushAsync().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}

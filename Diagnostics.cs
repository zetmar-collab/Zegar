using System;
using System.IO;
using System.Threading.Tasks;

namespace Zegar;

internal static class AppLog
{
    private static readonly object gate = new();
    private static Task pending = Task.CompletedTask;
    internal static string? FolderOverride { get; set; }
    internal static string LogPath => Path.Combine(FolderOverride ?? SettingsStore.Folder,"diagnostics.log");
    internal static void Write(string operation, Exception? exception = null)
    {
        // Deliberately omit exception messages, stack paths, city names and URLs.
        string entry=$"{DateTimeOffset.UtcNow:O} {operation} {exception?.GetType().Name} {exception?.HResult:X8}";
        lock(gate) pending=pending.ContinueWith(_=>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if(File.Exists(LogPath) && new FileInfo(LogPath).Length>262144) File.Move(LogPath,LogPath+".old",true);
                File.AppendAllText(LogPath,entry+Environment.NewLine);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
        },TaskScheduler.Default);
    }
    internal static Task FlushAsync() {lock(gate) return pending;}
    internal static async Task<string> ReadAsync()
    {
        await FlushAsync();
        try {return File.Exists(LogPath)?await File.ReadAllTextAsync(LogPath):"";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {return ex.GetType().Name;}
    }
}

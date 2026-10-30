using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Zegar;

internal static class InstanceActivation
{
    internal static string PipeName => "ZegarHTC-" + Environment.UserName + "-" + Process.GetCurrentProcess().SessionId;
    public static async Task<bool> SendShowAsync(string? name = null)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var pipe = new NamedPipeClientStream(".",name ?? PipeName,PipeDirection.InOut,PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await pipe.WriteAsync(new byte[] { 1 },timeout.Token).ConfigureAwait(false);
            var reply = new byte[1];
            return await pipe.ReadAsync(reply,timeout.Token).ConfigureAwait(false) == 1 && reply[0] == 1;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException or UnauthorizedAccessException) { return false; }
    }
    public static async Task ListenAsync(Func<Task> show, CancellationToken cancellation, string? name = null)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(name ?? PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                var command = new byte[1];
                if (await pipe.ReadAsync(command,timeout.Token).ConfigureAwait(false) == 1 && command[0] == 1)
                {
                    await show().ConfigureAwait(false);
                    await pipe.WriteAsync(new byte[] { 1 },timeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
            {
                if (cancellation.IsCancellationRequested) return;
                try { await Task.Delay(200,cancellation).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
            }
        }
    }
}

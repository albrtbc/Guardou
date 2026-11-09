using System.Diagnostics;
using System.Text;

namespace Guardou.Infrastructure.Previews;

internal sealed class ExternalTool(string name, ExternalToolLimits? limits = null)
{
    private const int BufferSize = 81920;

    private readonly ExternalToolLimits _limits = limits ?? ExternalToolLimits.Default;
    private readonly Lazy<string?> _path = new(() => Find(name));

    public bool IsAvailable => _path.Value is not null;

    public async Task<byte[]> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(_path.Value ?? throw new InvalidOperationException($"The tool '{name}' is not installed."))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_limits.Timeout);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"The tool '{name}' did not start.");
        Task<string>? error = null;
        try
        {
            error = ReadErrorAsync(process.StandardError.BaseStream, limit.Token);
            byte[] output = await ReadOutputAsync(process.StandardOutput.BaseStream, limit.Token);
            await process.WaitForExitAsync(limit.Token);
            return process.ExitCode == 0
                ? output
                : throw new InvalidDataException($"{name} failed: {(await error).Trim()}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && limit.IsCancellationRequested)
        {
            throw new TimeoutException($"{name} did not finish within {_limits.Timeout.TotalSeconds:0} seconds.");
        }
        finally
        {
            Kill(process);
            await limit.CancelAsync();
            if (error is not null)
            {
                await ((Task)error).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill.
        }
    }

    private async Task<byte[]> ReadOutputAsync(Stream stream, CancellationToken cancellationToken)
    {
        using MemoryStream output = new();
        byte[] buffer = new byte[BufferSize];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > _limits.MaximumOutputBytes)
            {
                throw new InvalidDataException($"{name} produced more than {_limits.MaximumOutputBytes} bytes of output.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private async Task<string> ReadErrorAsync(Stream stream, CancellationToken cancellationToken)
    {
        using MemoryStream head = new();
        byte[] buffer = new byte[BufferSize];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            int keep = Math.Min(read, _limits.MaximumErrorBytes - (int)head.Length);
            head.Write(buffer, 0, keep);
        }

        return Encoding.UTF8.GetString(head.GetBuffer(), 0, (int)head.Length);
    }

    private static string? Find(string name)
    {
        string[] candidates = OperatingSystem.IsWindows() ? [name + ".exe", name] : [name];
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(directory => candidates.Select(candidate => Path.Combine(directory, candidate)))
            .FirstOrDefault(File.Exists);
    }
}

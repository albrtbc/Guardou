using System.Globalization;
using Guardou.Infrastructure.Previews;
using Guardou.UnitTests.Files;

namespace Guardou.UnitTests.Previews;

public sealed class ExternalToolTests : IDisposable
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(1.5);

    private readonly TemporaryDirectory _directory = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task KillsAChildThatOutlivesTheTimeout()
    {
        ExternalTool shell = Shell(new ExternalToolLimits(ShortTimeout, 1024, 1024));
        string pidFile = _directory.Combine("child.pid");

        await Assert.ThrowsAsync<TimeoutException>(() => shell.RunAsync(["-c", $"echo $$ > '{pidFile}'; exec sleep 60"], CancellationToken));

        await AssertStoppedAsync(pidFile);
    }

    [Fact]
    public async Task KillsTheWholeProcessTreeOnTimeout()
    {
        ExternalTool shell = Shell(new ExternalToolLimits(ShortTimeout, 1024, 1024));
        string pidFile = _directory.Combine("grandchild.pid");

        await Assert.ThrowsAsync<TimeoutException>(() => shell.RunAsync(["-c", $"sleep 60 & echo $! > '{pidFile}'; wait"], CancellationToken));

        await AssertStoppedAsync(pidFile);
    }

    [Fact]
    public async Task KillsTheChildWhenTheCallerCancels()
    {
        ExternalTool shell = Shell();
        string pidFile = _directory.Combine("cancelled.pid");
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

        Task<byte[]> run = shell.RunAsync(["-c", $"echo $$ > '{pidFile}'; exec sleep 60"], cancellation.Token);
        await WaitForFileAsync(pidFile);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await AssertStoppedAsync(pidFile);
    }

    [Fact]
    public async Task RefusesOutputBeyondTheCapAndStopsTheProducer()
    {
        Assert.SkipUnless(new ExternalTool("yes").IsAvailable, "yes is not installed.");
        ExternalTool endless = new("yes", new ExternalToolLimits(TimeSpan.FromMinutes(1), 100_000, 1024));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() => endless.RunAsync([], CancellationToken));

        Assert.Contains("100000 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturnsOutputWithinTheCap()
    {
        ExternalTool shell = Shell(new ExternalToolLimits(TimeSpan.FromMinutes(1), 1000, 1024));

        byte[] output = await shell.RunAsync(["-c", "head -c 1000 /dev/zero"], CancellationToken);

        Assert.Equal(1000, output.Length);
    }

    [Fact]
    public async Task KeepsTheHeadOfTheErrorOutput()
    {
        ExternalTool shell = Shell(new ExternalToolLimits(TimeSpan.FromMinutes(1), 1024, 100));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => shell.RunAsync(["-c", "echo first; head -c 200000 /dev/zero | tr '\\0' x >&2; exit 3"], CancellationToken));

        Assert.StartsWith("sh failed: ", exception.Message, StringComparison.Ordinal);
        Assert.Equal("sh failed: ".Length + 100, exception.Message.Length);
    }

    [Fact]
    public async Task DoesNotDeadlockWhenBothStreamsFillTheirPipes()
    {
        ExternalTool shell = Shell(new ExternalToolLimits(TimeSpan.FromSeconds(30), 4_000_000, 1024));

        byte[] output = await shell.RunAsync(["-c", "head -c 3000000 /dev/zero >&2; head -c 3000000 /dev/zero"], CancellationToken);

        Assert.Equal(3_000_000, output.Length);
    }

    private static ExternalTool Shell(ExternalToolLimits? limits = null)
    {
        ExternalTool shell = new("sh", limits);
        Assert.SkipUnless(shell.IsAvailable, "sh is not installed.");
        return shell;
    }

    private static async Task WaitForFileAsync(string path)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private static async Task AssertStoppedAsync(string pidFile)
    {
        await WaitForFileAsync(pidFile);
        int pid = int.Parse((await File.ReadAllTextAsync(pidFile, CancellationToken)).Trim(), CultureInfo.InvariantCulture);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (IsRunning(pid))
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            string stat = File.ReadAllText($"/proc/{pid}/stat");
            return stat[(stat.LastIndexOf(')') + 2)..][0] != 'Z';
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }
}

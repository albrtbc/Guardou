namespace Guardou.Application.Quotas;

internal sealed class StorageWrites
{
    private long _inFlight;

    public SemaphoreSlim Gate { get; } = new(1, 1);

    public long InFlight => Interlocked.Read(ref _inFlight);

    public void Add(long bytes)
    {
        Interlocked.Add(ref _inFlight, bytes);
    }

    public void Remove(long bytes)
    {
        Interlocked.Add(ref _inFlight, -bytes);
    }
}

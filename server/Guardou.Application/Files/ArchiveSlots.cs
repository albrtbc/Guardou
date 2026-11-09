namespace Guardou.Application.Files;

internal sealed class ArchiveSlots(int signedInCapacity, int anonymousCapacity) : IDisposable
{
    public const int SignedInCapacity = 4;
    public const int AnonymousCapacity = 2;

    private readonly SemaphoreSlim _signedIn = new(signedInCapacity, signedInCapacity);
    private readonly SemaphoreSlim _anonymous = new(anonymousCapacity, anonymousCapacity);

    public static ArchiveSlots WithDefaultCapacity()
    {
        return new ArchiveSlots(SignedInCapacity, AnonymousCapacity);
    }

    public IDisposable Acquire(ArchiveAudience audience)
    {
        SemaphoreSlim pool = audience == ArchiveAudience.Anonymous ? _anonymous : _signedIn;
        return pool.Wait(0) ? new Slot(pool) : throw new ArchiveBusyException();
    }

    public void Dispose()
    {
        _signedIn.Dispose();
        _anonymous.Dispose();
    }

    private sealed class Slot(SemaphoreSlim pool) : IDisposable
    {
        private SemaphoreSlim? _pool = pool;

        public void Dispose()
        {
            Interlocked.Exchange(ref _pool, null)?.Release();
        }
    }
}

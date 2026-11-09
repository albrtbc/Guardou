using System.Collections.Concurrent;

namespace Guardou.Application.Files;

internal sealed class ItemMoveLocks
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public SemaphoreSlim For(string ownerId)
    {
        return _locks.GetOrAdd(ownerId, _ => new SemaphoreSlim(1, 1));
    }
}

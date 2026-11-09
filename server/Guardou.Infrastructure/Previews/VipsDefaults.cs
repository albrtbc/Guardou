using NetVips;

namespace Guardou.Infrastructure.Previews;

internal static class VipsDefaults
{
    private const int Concurrency = 2;

    public static void Apply()
    {
        NetVips.NetVips.BlockUntrusted = true;
        NetVips.NetVips.Concurrency = Concurrency;
        Cache.Max = 0;
    }
}

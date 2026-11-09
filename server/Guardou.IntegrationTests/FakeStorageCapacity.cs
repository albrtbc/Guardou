using Guardou.Application.Quotas;

namespace Guardou.IntegrationTests;

internal sealed class FakeStorageCapacity : IStorageCapacity
{
    public const long ReserveBytes = 1024L * 1024 * 1024;

    public long Available { get; set; } = long.MaxValue;

    public long AvailableBytes()
    {
        return Available;
    }

    public void LeaveRoomFor(long bytes)
    {
        Available = ReserveBytes + bytes;
    }
}

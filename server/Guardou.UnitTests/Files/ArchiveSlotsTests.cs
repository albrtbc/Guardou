using Guardou.Application.Files;

namespace Guardou.UnitTests.Files;

public sealed class ArchiveSlotsTests
{
    [Fact]
    public void RefusesWithoutWaitingWhenAllSlotsAreTaken()
    {
        using ArchiveSlots slots = new(signedInCapacity: 2, anonymousCapacity: 1);
        using IDisposable first = slots.Acquire(ArchiveAudience.SignedIn);
        using IDisposable second = slots.Acquire(ArchiveAudience.SignedIn);

        Assert.Throws<ArchiveBusyException>(() => slots.Acquire(ArchiveAudience.SignedIn));
    }

    [Fact]
    public void ReleasesASlotOnceEvenWhenDisposedTwice()
    {
        using ArchiveSlots slots = new(signedInCapacity: 1, anonymousCapacity: 1);
        IDisposable first = slots.Acquire(ArchiveAudience.SignedIn);
        first.Dispose();
        first.Dispose();
        using IDisposable second = slots.Acquire(ArchiveAudience.SignedIn);

        Assert.Throws<ArchiveBusyException>(() => slots.Acquire(ArchiveAudience.SignedIn));
    }

    [Fact]
    public void KeepsTheTwoPoolsApart()
    {
        using ArchiveSlots slots = ArchiveSlots.WithDefaultCapacity();
        List<IDisposable> anonymous = Enumerable.Range(0, ArchiveSlots.AnonymousCapacity).Select(_ => slots.Acquire(ArchiveAudience.Anonymous)).ToList();

        Assert.Throws<ArchiveBusyException>(() => slots.Acquire(ArchiveAudience.Anonymous));
        List<IDisposable> signedIn = Enumerable.Range(0, ArchiveSlots.SignedInCapacity).Select(_ => slots.Acquire(ArchiveAudience.SignedIn)).ToList();
        Assert.Throws<ArchiveBusyException>(() => slots.Acquire(ArchiveAudience.SignedIn));

        anonymous.ForEach(slot => slot.Dispose());
        signedIn.ForEach(slot => slot.Dispose());
    }
}

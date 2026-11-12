using Guardou.Application.Files;
using Guardou.Domain.Files;
using Guardou.Infrastructure.Indexing;
using Guardou.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Guardou.IntegrationTests;

public sealed class IndexingBrokenTreesTests : SignedInTests
{
    private const int DepthCap = TreeLimits.MaximumWalkDepth;
    private const string LevelPrefix = "level-";

    private readonly CapturingLoggerProvider _logs = new();

    [Fact(Timeout = 60_000)]
    public async Task IndexingATreeThatLoopsBackLogsItOnce()
    {
        Guid firstId = await ReindexAsync(chainLength: 0, loop: true);

        CapturedLog log = Assert.Single(BrokenTreeLogs());
        Assert.Equal(firstId, log.Properties["ItemId"]);
    }

    [Fact(Timeout = 60_000)]
    public async Task IndexingATreeDeeperThanTheWalkLimitStopsAtTheLimit()
    {
        const int ChainLength = DepthCap + 44;
        Guid firstId = await ReindexAsync(ChainLength, loop: false);

        CapturedLog log = Assert.Single(BrokenTreeLogs());
        Assert.Equal(firstId, log.Properties["ItemId"]);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        List<string> indexed = await scope.ServiceProvider.GetRequiredService<IndexDbContext>().Entries
            .Where(entry => entry.Name.StartsWith(LevelPrefix))
            .Select(entry => entry.Name)
            .ToListAsync(CancellationToken);
        Assert.Equal(DepthCap, indexed.Count);
        Assert.Contains($"{LevelPrefix}{DepthCap - 1}", indexed);
        Assert.DoesNotContain($"{LevelPrefix}{DepthCap}", indexed);
        Assert.DoesNotContain($"{LevelPrefix}{ChainLength - 1}", indexed);
    }

    private IEnumerable<CapturedLog> BrokenTreeLogs()
    {
        return _logs.Logs.Where(log => log.Level == LogLevel.Error
            && log.Category == typeof(ItemIndexWriter).FullName
            && log.Message.Contains("loops back on itself", StringComparison.Ordinal));
    }

    // Moves "first", with a chain of the given length below it, into "second" and indexes the result. With loop, "second" already sits in "first", so the subtree loops back through its root.
    private async Task<Guid> ReindexAsync(int chainLength, bool loop)
    {
        ItemEntry first = await Client.CreateFolderEntryAsync(ItemsApi.Root, "first");
        ItemEntry second = await Client.CreateFolderEntryAsync(ItemsApi.Root, "second");
        Guid firstId = OwnedItems.ParseId(first.Id);
        Guid secondId = OwnedItems.ParseId(second.Id);
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Item firstItem = await context.Set<Item>().SingleAsync(item => item.Id == firstId, CancellationToken);
            Item secondItem = await context.Set<Item>().SingleAsync(item => item.Id == secondId, CancellationToken);
            if (loop)
            {
                secondItem.MoveTo(firstItem, "second");
            }

            Item parent = firstItem;
            for (int level = 0; level < chainLength; level++)
            {
                Item folder = Item.CreateFolder(parent, $"{LevelPrefix}{level}", parent.OwnerId, DateTimeOffset.UnixEpoch);
                context.Set<Item>().Add(folder);
                parent = folder;
            }

            await context.SaveChangesAsync(CancellationToken);
        }

        _logs.Clear();
        await using AsyncServiceScope looping = Factory.Services.CreateAsyncScope();
        IItemStore store = looping.ServiceProvider.GetRequiredService<IItemStore>();
        Item firstLoaded = await store.FindAnyAsync(firstId, CancellationToken) ?? throw new InvalidOperationException("The folder is missing.");
        Item secondLoaded = await store.FindAnyAsync(secondId, CancellationToken) ?? throw new InvalidOperationException("The folder is missing.");
        firstLoaded.MoveTo(secondLoaded, "first");
        await store.SaveAsync(CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), CancellationToken);
        return firstId;
    }

    private protected override TestServerFactory CreateFactory()
    {
        return new TestServerFactory
        {
            TestServices = services => services.AddLogging(logging => logging.AddProvider(_logs)),
        };
    }
}

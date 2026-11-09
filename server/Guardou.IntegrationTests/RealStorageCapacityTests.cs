using System.Net;
using Guardou.Application.Quotas;
using Guardou.Application.Settings;
using Guardou.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Guardou.IntegrationTests;

public sealed class RealStorageCapacityTests : SignedInTests
{
    [Fact]
    public async Task UploadsWhenTheVolumeHasRoom()
    {
        HttpResponseMessage response = await Client.UploadAsync(ItemsApi.Root, "a.txt", "hello");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private protected override TestServerFactory CreateFactory()
    {
        return new TestServerFactory
        {
            Settings = new Dictionary<string, string>
            {
                [$"{SettingsOverrides.Section}:{nameof(SettingsOverrides.MinFreeSpaceMb)}"] = "0",
            },
            TestServices = services => services.Replace(
                ServiceDescriptor.Singleton<IStorageCapacity>(provider => new StorageCapacity(provider.GetRequiredService<StoragePaths>()))),
        };
    }
}

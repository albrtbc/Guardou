using Guardou.Application.Quotas;
using Guardou.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Guardou.IntegrationTests;

public sealed class TestServerFactory : WebApplicationFactory<Program>
{
    public const string AdminUsername = "admin";
    public const string AdminDisplayName = "Administrator";
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Integration-pass1";

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "guardou-tests", Guid.NewGuid().ToString("N"));

    internal string FilesDirectory => Path.Combine(_dataDir, "files");

    internal string ThumbsDirectory => Path.Combine(_dataDir, "thumbs");

    internal string ProfileDirectory => Path.Combine(_dataDir, "profile");

    internal TestTimeProvider Time { get; } = new();

    internal FakeStorageCapacity Capacity { get; } = new();

    internal Action<IServiceCollection> TestServices { get; init; } = _ => { };

    internal IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        string webRoot = Directory.CreateDirectory(Path.Combine(_dataDir, "wwwroot")).FullName;
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html>");
        builder.UseWebRoot(webRoot);
        foreach ((string key, string value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(new StoragePaths(_dataDir)));
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time));
            services.Replace(ServiceDescriptor.Singleton<IStorageCapacity>(Capacity));
            TestServices(services);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dataDir, recursive: true);
    }
}

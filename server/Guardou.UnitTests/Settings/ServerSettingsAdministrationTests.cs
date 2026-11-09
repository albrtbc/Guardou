using Guardou.Application.Settings;
using Guardou.Domain.Settings;
using Guardou.UnitTests.Audit;
using Guardou.UnitTests.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.UnitTests.Settings;

public sealed class ServerSettingsAdministrationTests
{
    private readonly FakeSettingsStore _store = new();
    private readonly TestAudit _audit = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(" https://files.example.com/ ", "https://files.example.com")]
    [InlineData("http://192.168.1.10:8080", "http://192.168.1.10:8080")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task NormalizesTheExternalUrl(string? value, string? expected)
    {
        ServerSettingsView settings = await Administration().ChangeAsync(new ServerSettings(value), CancellationToken);

        Assert.Equal(expected, settings.ExternalUrl);
        Assert.Equal(expected, (await _store.GetAsync<ServerSettings>(CancellationToken)).ExternalUrl);
    }

    [Theory]
    [InlineData("files.example.com")]
    [InlineData("https://user@files.example.com")]
    [InlineData("https://files.example.com/#top")]
    public async Task RefusesAnInvalidExternalUrl(string value)
    {
        await Assert.ThrowsAsync<InvalidSettingsException>(() =>
            Administration().ChangeAsync(new ServerSettings(value), CancellationToken));
    }

    [Fact]
    public async Task ChangesTheTrashRetention()
    {
        ServerSettingsView settings = await Administration().ChangeAsync(new ServerSettings(null, 7), CancellationToken);

        Assert.Equal(7, settings.TrashRetentionDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public async Task RefusesATrashRetentionOutOfRange(int days)
    {
        await Assert.ThrowsAsync<InvalidSettingsException>(() =>
            Administration().ChangeAsync(new ServerSettings(null, days), CancellationToken));
    }

    [Fact]
    public async Task ChangesTheUploadSettings()
    {
        ServerSettingsView settings = await Administration().ChangeAsync(
            new ServerSettings(null, UploadConcurrency: 5, UploadChunkSizeMb: 16),
            CancellationToken);

        Assert.Equal(5, settings.UploadConcurrency);
        Assert.Equal(16, settings.UploadChunkSizeMb);
    }

    [Theory]
    [InlineData(0, 64)]
    [InlineData(7, 64)]
    [InlineData(3, 0)]
    [InlineData(3, 91)]
    public async Task RefusesUploadSettingsOutOfRange(int concurrency, int chunkSizeMb)
    {
        await Assert.ThrowsAsync<InvalidSettingsException>(() =>
            Administration().ChangeAsync(
                new ServerSettings(null, UploadConcurrency: concurrency, UploadChunkSizeMb: chunkSizeMb),
                CancellationToken));
    }

    [Fact]
    public async Task LogsTheSectionAndNeverTheValues()
    {
        await Administration().ChangeAsync(new ServerSettings("https://private-host.example.com", 9), CancellationToken);

        LogRecord record = Assert.Single(_audit.Logger.Records);
        Assert.Equal(9020, record.EventId.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("server", record.Properties["Section"]);
        Assert.Equal(TestAudit.Actor, record.Properties["ActorId"]);
        Assert.Equal(TestAudit.Ip, record.Properties["ClientIp"]);
        Assert.DoesNotContain("private-host", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogsNothingWhenTheChangeIsRefused()
    {
        await Assert.ThrowsAsync<InvalidSettingsException>(() =>
            Administration().ChangeAsync(new ServerSettings("not a url"), CancellationToken));

        Assert.Empty(_audit.Logger.Records);
    }

    [Fact]
    public void HasNoMaximumFileSizeAndAGigabyteReserveByDefault()
    {
        ServerSettingsView view = Administration().Defaults();

        Assert.Null(view.MaxFileSizeMb);
        Assert.Equal(1024, view.MinFreeSpaceMb);
    }

    [Fact]
    public async Task ChangesTheStorageLimits()
    {
        ServerSettingsView settings = await Administration().ChangeAsync(
            new ServerSettings(null, MaxFileSizeMb: 500, MinFreeSpaceMb: 0),
            CancellationToken);

        Assert.Equal(500, settings.MaxFileSizeMb);
        Assert.Equal(0, settings.MinFreeSpaceMb);
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(-1, 1024)]
    [InlineData(1_048_577, 1024)]
    [InlineData(null, -1)]
    [InlineData(null, 1_048_577)]
    public async Task RefusesStorageLimitsOutOfRange(int? maxFileSizeMb, int minFreeSpaceMb)
    {
        await Assert.ThrowsAsync<InvalidSettingsException>(() =>
            Administration().ChangeAsync(
                new ServerSettings(null, MaxFileSizeMb: maxFileSizeMb, MinFreeSpaceMb: minFreeSpaceMb),
                CancellationToken));
    }

    [Fact]
    public async Task LocksTheStorageLimitsFromTheEnvironment()
    {
        ServerSettingsAdministration administration = Administration(new SettingsOverrides { MaxFileSizeMb = 100, MinFreeSpaceMb = 2048 });

        ServerSettingsView view = await administration.ChangeAsync(
            new ServerSettings(null, MaxFileSizeMb: 5, MinFreeSpaceMb: 5),
            CancellationToken);

        Assert.Equal(100, view.MaxFileSizeMb);
        Assert.Equal(2048, view.MinFreeSpaceMb);
        Assert.Equal(
            [
                new LockedSetting("maxFileSizeMb", SettingsVariables.MaxFileSizeMb),
                new LockedSetting("minFreeSpaceMb", SettingsVariables.MinFreeSpaceMb),
            ],
            view.Locked);
    }

    [Fact]
    public async Task UsesTheRequestOriginWithoutAnExternalUrl()
    {
        Assert.Equal("http://localhost", await Administration().PublicOriginAsync("http://localhost", CancellationToken));
    }

    [Fact]
    public async Task KeepsTheStoredValueOfALockedField()
    {
        await _store.SaveAsync(new ServerSettings("https://stored.example.com"), CancellationToken);
        ServerSettingsAdministration administration = Administration(new SettingsOverrides { ExternalUrl = "https://env.example.com" });

        ServerSettingsView view = await administration.ChangeAsync(new ServerSettings("https://other.example.com", 9), CancellationToken);

        Assert.Equal("https://env.example.com", view.ExternalUrl);
        Assert.Equal(9, view.TrashRetentionDays);
        Assert.Equal([new LockedSetting("externalUrl", SettingsVariables.ExternalUrl)], view.Locked);
        Assert.Equal(new ServerSettings("https://stored.example.com", 9), await _store.GetAsync<ServerSettings>(CancellationToken));
    }

    private ServerSettingsAdministration Administration(SettingsOverrides? overrides = null)
    {
        return new ServerSettingsAdministration(TestSettings.Server(_store, overrides), _audit.Log);
    }
}

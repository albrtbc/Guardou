using System.Reflection;
using Guardou.Application.Audit;
using Guardou.Application.Authentication;
using Guardou.Application.Configuration;
using Guardou.Application.Files;
using Guardou.Application.Preferences;
using Guardou.Application.Previews;
using Guardou.Application.Quotas;
using Guardou.Application.Search;
using Guardou.Application.Settings;
using Guardou.Application.Shares;
using Guardou.Application.Tasks;
using Guardou.Application.Uploads;
using Guardou.Application.Users;
using Guardou.Domain.Authentication;
using Guardou.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Guardou.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAuditContext, NoAuditContext>();
        services.AddSingleton<AuditLog>();
        services.AddSingleton<TaskSignal>();
        services.AddSingleton<StorageQuotaLocks>();
        services.AddSingleton<StorageWrites>();
        services.AddSingleton<ItemMoveLocks>();
        services.AddSingleton(ArchiveLimits.Default);
        services.AddSingleton(_ => ArchiveSlots.WithDefaultCapacity());
        services.AddScoped<StorageReservations>();
        services.AddScoped<ITaskHandler, PreviewTaskHandler>();
        services.AddScoped<ITaskHandler, IndexTaskHandler>();
        AddSettings(services, new ServerSettingsSchema());
        AddSettings(services, new OidcSettingsSchema());
        services.AddSingleton<IValidateOptions<SettingsOverrides>, SettingsOverridesValidation>();
        services.AddOptions<SettingsOverrides>().ValidateOnStart();
        services.AddScoped<RuntimeConfigurationQuery>();
        services.AddScoped<EffectiveConfigurationQuery>();
        services.AddScoped<UserAdministration>();
        services.AddScoped<UserAccounts>();
        services.AddScoped<ProfilePictures>();
        services.AddScoped<UserPreferencesService>();
        services.AddScoped<UserSearch>();
        services.AddScoped<Registration>();
        services.AddScoped<PasswordLogin>();
        services.AddScoped<ExternalSignIn>();
        services.AddScoped<OidcSettingsAdministration>();
        services.AddScoped<ServerSettingsAdministration>();
        services.AddScoped<OwnedItems>();
        services.AddScoped<AccessibleItems>();
        services.AddScoped<ItemEntryFactory>();
        services.AddScoped<ItemPlacement>();
        services.AddScoped<ItemTree>();
        services.AddScoped<ItemRemover>();
        services.AddScoped<ShareCleanup>();
        AddScopedWithInternalConstructor<ItemBrowser>(services);
        AddScopedWithInternalConstructor<ItemOrganizer>(services);
        services.AddScoped<OwnedUploads>();
        AddScopedWithInternalConstructor<UploadSessions>(services);
        AddScopedWithInternalConstructor<UploadCompleter>(services);
        AddScopedWithInternalConstructor<UploadCleanup>(services);
        AddScopedWithInternalConstructor<TrashService>(services);
        AddScopedWithInternalConstructor<ItemDownloads>(services);
        AddScopedWithInternalConstructor<ItemOpenRecorder>(services);
        AddScopedWithInternalConstructor<RecentItems>(services);
        AddScopedWithInternalConstructor<ItemStars>(services);
        AddScopedWithInternalConstructor<ItemSearch>(services);
        AddScopedWithInternalConstructor<PreviewTasks>(services);
        services.AddScoped<ITaskProducer>(provider => provider.GetRequiredService<PreviewTasks>());
        AddScopedWithInternalConstructor<IndexTasks>(services);
        services.AddScoped<ITaskProducer>(provider => provider.GetRequiredService<IndexTasks>());
        AddScopedWithInternalConstructor<ItemPreviews>(services);
        AddScopedWithInternalConstructor<ItemSharing>(services);
        AddScopedWithInternalConstructor<SharedItems>(services);
        AddScopedWithInternalConstructor<DirectSharing>(services);
        AddScopedWithInternalConstructor<TaskRunner>(services);
        AddScopedWithInternalConstructor<QueueSettings>(services);
        AddScopedWithInternalConstructor<TaskAdministration>(services);
        return services;
    }

    private static void AddSettings<TSettings>(IServiceCollection services, ISettingsSchema<TSettings> schema)
        where TSettings : class, ISettings<TSettings>
    {
        services.AddSingleton(schema);
        services.AddScoped(provider => new EffectiveSettings<TSettings>(
            provider.GetRequiredService<ISettingsStore>(),
            schema,
            provider.GetRequiredService<IOptions<SettingsOverrides>>()));
    }

    private static void AddScopedWithInternalConstructor<TService>(IServiceCollection services)
        where TService : class
    {
        ConstructorInfo constructor = typeof(TService).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        Type[] parameterTypes = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        services.AddScoped(provider => (TService)constructor.Invoke(parameterTypes.Select(provider.GetRequiredService).ToArray()));
    }
}

using System.CommandLine;
using Guardou.Application;
using Guardou.Application.Audit;
using Guardou.Application.Persistence;
using Guardou.Application.Users;
using Guardou.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Guardou.Cli.Hosting;

internal static class CliHost
{
    public static Task<int> ExecuteAsync(
        ParseResult parseResult,
        Func<UserAdministration, Task<UserAccountResult>> operation,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync<UserAdministration>(
            async administration => await ReportAsync(parseResult, await operation(administration)),
            cancellationToken);
    }

    public static async Task<int> ExecuteAsync<TService>(Func<TService, Task<int>> operation, CancellationToken cancellationToken)
        where TService : notnull
    {
        using IHost host = Build();
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().MigrateAsync(cancellationToken);
        return await operation(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private static async Task<int> ReportAsync(ParseResult parseResult, UserAccountResult result)
    {
        foreach (UserAccountError error in result.Errors)
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(error.Description);
        }

        return result.Succeeded ? 0 : 1;
    }

    private static IHost Build()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure();
        builder.Services.Replace(ServiceDescriptor.Singleton<IAuditContext, CliAuditContext>());
        return builder.Build();
    }
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Guardou.IntegrationTests;

internal sealed class QueryPlanRecorder : DbCommandInterceptor
{
    private const int DetailColumn = 3;

    private readonly List<IReadOnlyList<string>> _plans = [];

    public IReadOnlyList<IReadOnlyList<string>> Plans
    {
        get
        {
            lock (_plans)
            {
                return [.. _plans];
            }
        }
    }

    public void Clear()
    {
        lock (_plans)
        {
            _plans.Clear();
        }
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> plan = await ExplainAsync(command, cancellationToken);
        lock (_plans)
        {
            _plans.Add(plan);
        }

        return result;
    }

    private static async Task<IReadOnlyList<string>> ExplainAsync(DbCommand command, CancellationToken cancellationToken)
    {
        await using DbCommand explain = command.Connection!.CreateCommand();
        explain.Transaction = command.Transaction;
        explain.CommandText = "EXPLAIN QUERY PLAN " + command.CommandText;
        foreach (DbParameter parameter in command.Parameters)
        {
            DbParameter copy = explain.CreateParameter();
            copy.ParameterName = parameter.ParameterName;
            copy.Value = parameter.Value;
            explain.Parameters.Add(copy);
        }

        List<string> details = [];
        await using DbDataReader reader = await explain.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            details.Add(reader.GetString(DetailColumn));
        }

        return details;
    }
}

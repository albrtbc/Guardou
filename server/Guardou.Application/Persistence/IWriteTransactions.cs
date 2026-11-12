namespace Guardou.Application.Persistence;

public interface IWriteTransactions
{
    Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken);
}

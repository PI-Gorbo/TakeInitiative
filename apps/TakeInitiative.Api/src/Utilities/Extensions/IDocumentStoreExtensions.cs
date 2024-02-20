using CSharpFunctionalExtensions;
using Marten;

namespace TakeInitiative.Utilities.Extensions;
public static class IDocumentStoreExtensions
{
    public static async Task<Result> Try(this IDocumentStore store, Func<IDocumentSession, Task> func, CancellationToken token = default)
    {
        using var session = store.LightweightSession();
        return await Result.Try(async () => await func(session));
    }

    public static async Task<Result<T>> Try<T>(this IDocumentStore store, Func<IDocumentSession, Task<T>> func, CancellationToken token = default)
    {
        using var session = store.LightweightSession();
        return await Result.Try(async () => await func(session), (ex) =>
        {
            if (ex is FastEndpoints.ValidationFailureException)
            {
                throw ex; // A special case for fast endpoints
            }

            return $"Something unexpected happened while trying to contact the database: {ex}";
        });
    }

    public static async Task<Result<T>> Try<T>(this IDocumentStore store, Func<IDocumentSession, Task<Result<T>>> func, CancellationToken token = default)
    {
        using var session = store.LightweightSession();
        try
        {
            return await func(session);
        }
        catch (Exception ex)
        {
            return Result.Failure<T>($"Something unexpected happened while trying to contact the database: {ex}");
        }
    }

    public static async Task<Result> Try(this IDocumentStore store, Func<IDocumentSession, Task<Result>> func, CancellationToken token = default)
    {
        using var session = store.LightweightSession();
        try
        {
            return await func(session);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Something unexpected happened while trying to contact the database: {ex}");
        }
    }
}

using Alba;
using CSharpFunctionalExtensions;
using FluentAssertions;
using TakeInitiative.Api.Client;
using TakeInitiative.Api.Features.Users;
using TakeInitiative.Utilities;
using Testcontainers.PostgreSql;
namespace TakeInitiative.Api.Tests.Integration;

public interface IWebAppClient
{
    public IAlbaHost AlbaHost { get; }
    public PostgreSqlContainer PostgreSqlContainer { get; }
    public IDiceRoller DiceRollerSubstitute { get; }
    public TakeApiClient Client { get; }
}

public static class IWebAppClientExtensions
{
    public static async Task<UnitResult<ApiException>> Scenario(this IWebAppClient fixture, Func<TakeApiClient, Task> func)
    {
        try
        {
            await func(fixture.Client);
            return UnitResult.Success<ApiException>();
        }
        catch (ApiException ex)
        {
            return UnitResult.Failure(ex);
        }
    }

    public static async Task<Result<T, ApiException>> Scenario<T>(this IWebAppClient fixture, Func<TakeApiClient, Task<T>> func)
    {
        try
        {
            var result = await func(fixture.Client);
            return result;
        }
        catch (ApiException ex)
        {
            return ex;
        }
    }
}

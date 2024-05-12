using CSharpFunctionalExtensions;
using FastEndpoints;

namespace TakeInitiative.Utilities.Extensions;

public static class ApiErrorExtensions
{

    public static Task ToApiResult<TRequest, TResponse>(this Result<TResponse, ApiError> result, Endpoint<TRequest, TResponse> endpoint)
    {
        if (result.IsSuccess)
        {
            endpoint.Response = result.Value;
        }
        else
        {
            endpoint.ThrowError(result.Error.Message, (int)result.Error.StatusCode);
        }

        return Task.CompletedTask;
    }

    public static async Task ToApiResult<TRequest, TResponse>(this Task<Result<TResponse, ApiError>> task, Endpoint<TRequest, TResponse> endpoint)
    {
        var result = await task;
        await result.ToApiResult(endpoint);
        return;
    }

    public static Task ToApiResult<TRequest>(this UnitResult<ApiError> result, Endpoint<TRequest> endpoint)
    {
        if (result.IsFailure)
        {
            endpoint.ThrowError(result.Error.Message, (int)result.Error.StatusCode);
        }

        return Task.CompletedTask;
    }

    public static async Task ToApiResult<TRequest>(this Task<UnitResult<ApiError>> task, Endpoint<TRequest> endpoint)
    {
        var result = await task;
        await result.ToApiResult(endpoint);
        return;
    }

    public static Task ToApiResult<TRequest, TResponse>(this Result<TResponse, PropertyApiError<TRequest>> result, Endpoint<TRequest, TResponse> endpoint)
    {
        if (result.IsSuccess)
        {
            endpoint.Response = result.Value;
        }
        else
        {
            endpoint.ThrowError(result.Error.PropertyExpression, result.Error.Message, (int)result.Error.StatusCode);
        }

        return Task.CompletedTask;
    }

    public static async Task ToApiResult<TRequest, TResponse>(this Task<Result<TResponse, PropertyApiError<TRequest>>> task, Endpoint<TRequest, TResponse> endpoint)
    {
        var result = await task;
        await result.ToApiResult(endpoint);
        return;
    }
}
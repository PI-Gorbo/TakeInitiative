using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TakeInitiative.Utilities.Extensions;

public static class ApiErrorExtensions
{

    public static Task ReturnApiResult<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Result<TResponse, ApiError> result)
        where TRequest : notnull
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

    public static async Task ReturnApiResult<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Task<Result<TResponse, ApiError>> task)
            where TRequest : notnull
    {
        var result = await task;
        await endpoint.ReturnApiResult(result);
        return;
    }

    public static Task ReturnApiResult<TRequest>(this Endpoint<TRequest> endpoint, UnitResult<ApiError> result, HttpStatusCode successStatusCode = HttpStatusCode.OK)
        where TRequest : notnull
    {
        if (result.IsFailure)
        {
            endpoint.ThrowError(result.Error.Message, (int)result.Error.StatusCode);
        }
        else
        {
            endpoint.HttpContext.Response.StatusCode = (int)successStatusCode;
        }

        return Task.CompletedTask;
    }


    public static Task ReturnApiResult(this EndpointWithoutRequest endpoint, UnitResult<ApiError> result)
    {
        if (result.IsFailure)
        {
            endpoint.ThrowError(result.Error.Message, (int)result.Error.StatusCode);
        }

        return Task.CompletedTask;
    }

    public static Task ReturnApiResult<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Result<TResponse, PropertyApiError<TRequest>> result)
        where TRequest : notnull
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

    public static async Task ReturnApiResult<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, Task<Result<TResponse, PropertyApiError<TRequest>>> task)
        where TRequest : notnull
    {
        var result = await task;
        await endpoint.ReturnApiResult(result);
        return;
    }
}
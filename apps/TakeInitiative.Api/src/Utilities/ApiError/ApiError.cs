using System.Linq.Expressions;
using System.Net;
namespace TakeInitiative.Utilities;

public record ApiError
{
    public required string Message { get; set; }
    public required HttpStatusCode StatusCode { get; set; }
    public static PropertyApiError<T> Invalid<T>(Expression<Func<T, object?>> expression, string message) => new PropertyApiError<T>() { Message = message, StatusCode = HttpStatusCode.BadRequest, PropertyExpression = expression };
    public static ApiError BadRequest(string message) => new ApiError() { Message = message, StatusCode = HttpStatusCode.BadRequest };
    public static ApiError NotFound(string message) => new ApiError { Message = message, StatusCode = HttpStatusCode.NotFound };
    public static ApiError Unauthorized(string message) => new ApiError { Message = message, StatusCode = HttpStatusCode.Unauthorized };
    public static ApiError InternalServerError(string message) => new ApiError { Message = message, StatusCode = HttpStatusCode.InternalServerError };
    public static ApiError DbInteractionFailed(string message) => new ApiError { Message = $"Failed to fetch data from the database. {message}", StatusCode = HttpStatusCode.ServiceUnavailable };
    public static ApiError DbInteractionFailed(Exception ex) => new ApiError { Message = $"Failed to fetch data from the database. {ex.Message}", StatusCode = HttpStatusCode.ServiceUnavailable };

    public static implicit operator ApiError(string value) => BadRequest(value);
}



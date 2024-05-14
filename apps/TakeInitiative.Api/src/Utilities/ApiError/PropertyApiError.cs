using System.Linq.Expressions;
namespace TakeInitiative.Utilities;

public record PropertyApiError<T> : ApiError
{
    public required Expression<Func<T, object?>> PropertyExpression { get; set; }
}



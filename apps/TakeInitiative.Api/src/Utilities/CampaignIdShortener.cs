using CSharpFunctionalExtensions;

namespace TakeInitiative.Utilities;
public static class CampaignIdShortener
{
    public static string ToShortId(Guid id)
    {
        var result = Convert
            .ToBase64String(
                id.ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .Substring(0, 22);
        return result;
    }

    public static Result<Guid> ToId(string? id)
    {
        return Result.SuccessIf(id != null, "No provided value")
        .MapTry(() =>
        {
            var convertedId = id.Replace("_", "/")
                                .Replace("-", "+")
                                + "==";

            return new Guid(Convert.FromBase64String(convertedId));
        }, ex => "Invalid join code format. Please check that you have entered the code exactly as given.");
    }
}
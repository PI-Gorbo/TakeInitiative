using CSharpFunctionalExtensions;

namespace TakeInitiative.Api.Features.Users;
public interface IEmailSender
{
    public Task<Result> SendEmail(string to, string from, string subject, string body);
}


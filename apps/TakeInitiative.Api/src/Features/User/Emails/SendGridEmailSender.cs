using CSharpFunctionalExtensions;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace TakeInitiative.Api.Features.Users;

public class SendGridEmailSender(ISendGridClient client) : IEmailSender
{
    public async Task<Result> SendEmail(string to, string from, string subject, string body)
    {
        var msg = new SendGridMessage()
        {
            From = new EmailAddress(from, "Take Initiative"),
            Subject = subject
        };
        msg.AddContent(MimeType.Html, body);
        msg.AddTo(new EmailAddress(to));
        var response = await client.SendEmailAsync(msg).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return Result.Success();
        }

        return Result.Failure(await response.Body.ReadAsStringAsync());
    }
}


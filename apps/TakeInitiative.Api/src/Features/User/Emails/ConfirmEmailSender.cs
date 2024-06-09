using System.Reflection;
using System.Text;
using CSharpFunctionalExtensions;
using HandlebarsDotNet;
using JasperFx.Core;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Mjml.Net;

namespace TakeInitiative.Api.Features.Users;
public class ConfirmEmailSender(IEmailSender emailSender, IDocumentSession session,IOptions<EmailOptions> emailOpts, IOptions<UrlsOptions> urlsOpts, UserManager<ApplicationUser> userManager, ILogger<ConfirmEmailSender> logger, IWebHostEnvironment env)
{
    public async Task<Result> SendConfirmAccountEmail(ApplicationUser user)
    {
        if (user.EmailConfirmed) {
            return Result.Success("Email is already confirmed");
        }

        if (user.EmailConfirmationLastSent.HasValue) {
            var difference = (DateTimeOffset.UtcNow - user.EmailConfirmationLastSent.Value);
            if (difference.TotalSeconds < 120)
            {
                return Result.Failure("Must wait at least two minutes before sending another confirmation email.");
            }
        }
        user.EmailConfirmationLastSent = DateTimeOffset.UtcNow;
        session.Store(user);
        await session.SaveChangesAsync();

        // Get the HTML and parse the handlebars.
        string preHandlebarsConfirmationEmail = ReadConfirmationEmail();
        var template = Handlebars.Compile(preHandlebarsConfirmationEmail);

        // Get the handlebars data.
        var link = await GetEmailConfirmationLink(user);
        var data = new
        {
            ConfirmationLink = link,
            CurrentYear = DateTime.UtcNow.Year
        };
        var mjmlHTML = template(data);

        // Render the MJML into html
        var mjmlRenderer = new MjmlRenderer();
        var (html, errors) = mjmlRenderer.Render(mjmlHTML, new MjmlOptions
        {
            Beautify = true
        });

        if (errors.Count > 0)
        {
            return Result.Failure(string.Join(". ", errors));
        }

        if (env.IsDevelopment())
        {
            logger.LogInformation("Confirmation Link: {ConfirmationLink}", link);
        }

        return await emailSender.SendEmail(
            user.Email,
            $"no-reply@{emailOpts.Value.Domain}",
            "Take Initiative - Email Confirmation",
            html
        );
    }

    private string ReadConfirmationEmail()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "TakeInitiative.Api.Embedded.ConfirmationEmail.mjml";
        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task<string> GetEmailConfirmationLink(ApplicationUser user)
    {
        var emailConfirmationCode = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailConfirmationCode));
        return new Uri(new Uri(urlsOpts.Value.Web), $"/confirm?code={code}").ToString();
    }
}
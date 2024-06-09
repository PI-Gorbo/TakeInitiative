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
public class ResetPasswordEmailSender(
    IEmailSender emailSender,
    IDocumentSession session,
    IOptions<EmailOptions> emailOpts,
    IOptions<UrlsOptions> urlsOpts,
    UserManager<ApplicationUser> userManager,
    ILogger<ResetPasswordEmailSender> logger,
    IWebHostEnvironment env)
{
    public async Task<Result> SendResetPasswordEmail(ApplicationUser user, CancellationToken ct)
    {
        if (user.ResetPasswordLastSent.HasValue)
        {
            var difference = DateTimeOffset.UtcNow - user.ResetPasswordLastSent.Value;
            if (difference.TotalSeconds < 120)
            {
                return Result.Failure("Must wait at least two minutes before sending another reset email.");
            }
        }

        // Update the user with the last time sent.
        user.ResetPasswordLastSent = DateTimeOffset.UtcNow;
        session.Store(user);
        await session.SaveChangesAsync(ct);

        // Get the HTML and parse the handlebars.
        string preHandlebarsResetEmail = ReadResetEmail();
        var template = Handlebars.Compile(preHandlebarsResetEmail);

        // Get the handlebars data.
        var link = await GetPasswordResetLink(user);
        var data = new
        {
            Link = link,
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
            logger.LogInformation("Reset Link: {ConfirmationLink}", link);
        }

        return await emailSender.SendEmail(
            user.Email,
            $"no-reply@{emailOpts.Value.Domain}",
            "Take Initiative - Password Rest",
            html
        ).TapError(e => logger.LogError("Failed to send password reset email. {error}", e));
    }

    private string ReadResetEmail()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "TakeInitiative.Api.Embedded.ResetPasswordEmail.mjml";
        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task<string> GetPasswordResetLink(ApplicationUser user)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var urlEncodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return new Uri(new Uri(urlsOpts.Value.Web), $"/resetPassword/{urlEncodedToken}-{Uri.EscapeDataString(user.Email)}").ToString();
    }
}
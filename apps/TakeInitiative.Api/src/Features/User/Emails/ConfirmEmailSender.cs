using System.Text;
using CSharpFunctionalExtensions;
using JasperFx.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Mjml.Net;

namespace TakeInitiative.Api.Features.Users;
public class ConfirmEmailSender(IEmailSender emailSender, IOptions<EmailOptions> options, UserManager<ApplicationUser> userManager)
{
    public async Task<Result> SendConfirmAccountEmail(ApplicationUser user)
    {
        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var mjmlRenderer = new MjmlRenderer();
        string text = @"
        <mjml>
            <mj-head>
                <mj-title>Hello World Example</mj-title>
            </mj-head>
            <mj-body>
                <mj-section>
                    <mj-column>
                        <mj-text>
                            Hello World!
                        </mj-text>
                    </mj-column>
                </mj-section>
            </mj-body>
        </mjml>";

        var (html, errors) = mjmlRenderer.Render(text, new MjmlOptions
        {
            Beautify = false
        });

        if (errors.Count > 0)
        {
            return Result.Failure(string.Join(". ", errors));
        }

        return await emailSender.SendEmail(
            user.Email,
            options.Value.Domain,
            "Take Initiative - Email Confirmation",
            html
        );
    }
}
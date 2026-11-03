using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Users;

/// <summary>
/// Signs a request in from its SamStack session (SamStack spec §4): the session cookie goes to
/// auth.samstack.org's <c>/api/session</c>, and a signed-in answer becomes the same <c>UserId</c>
/// claim the password sign-in issues, so every endpoint, policy and hub works unchanged.
/// </summary>
public partial class SamstackAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ISamstackSessions sessions,
    SamstackUserLinker linker,
    IOptions<SamstackAuthOptions> authOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "Samstack";

    /// <summary>The default scheme: this one when the request has a SamStack session, the password cookie otherwise.</summary>
    public const string SessionOrCookie = "SamstackOrCookie";

    private const string DeniedKey = "SamstackAuth:Denied";

    // Better Auth names it `<cookiePrefix>.session_token`, with `__Secure-` in front on https.
    [GeneratedRegex(@"(?:^|;\s*)((?:__Secure-)?samstack\.session_token=[^;]+)")]
    private static partial Regex SessionCookiePattern();

    /// <summary>The session cookie as it arrived (<c>name=value</c>), or null when there is none.</summary>
    public static string? SessionCookie(HttpRequest request)
    {
        // Raw, not Request.Cookies: that decodes the value, and auth checks the signature on the
        // encoded one.
        var match = SessionCookiePattern().Match(request.Headers.Cookie.ToString());
        return match.Success ? match.Groups[1].Value : null;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Only the session cookie leaves for auth; the API's other cookies are none of its business.
        var cookie = SessionCookie(Request);
        if (cookie is null)
        {
            return authOptions.Value.DevUser is { } devUser
                ? await SignIn(devUser.ToUser())
                : AuthenticateResult.NoResult();
        }

        SamstackSession session;
        try
        {
            session = await sessions.Get(cookie, Context.RequestAborted);
        }
        catch (HttpRequestException e)
        {
            Logger.LogError(e, "Could not check a SamStack session");
            return AuthenticateResult.Fail("Could not reach auth.samstack.org.");
        }

        switch (session)
        {
            case SamstackSession.SignedIn(var user):
                return await SignIn(user);
            case SamstackSession.SignedOut:
                return AuthenticateResult.NoResult();
            case SamstackSession.Denied:
                Context.Items[DeniedKey] = true;
                return AuthenticateResult.Fail("SamStack does not give this user access to TakeInitiative.");
            default:
                throw new InvalidOperationException($"Unhandled SamStack session {session.GetType().Name}.");
        }
    }

    private async Task<AuthenticateResult> SignIn(SamstackUser user)
    {
        var userId = await linker.Resolve(user, Context.RequestAborted);
        if (userId.IsFailure)
        {
            return AuthenticateResult.Fail(userId.Error);
        }

        var identity = new ClaimsIdentity([new Claim("UserId", userId.Value.ToString())], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>
    /// 401, as the cookie scheme answers (no redirect: the web app sends people to sign in), except
    /// for someone auth signed in but refused, who gets a 403 so the web app can say so.
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = Context.Items.ContainsKey(DeniedKey)
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}

using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Identity;

public class MartenUserStore<TUser, TRole>(IDocumentSession session) :
    IUserClaimStore<TUser>,
    IUserLoginStore<TUser>,
    IUserRoleStore<TUser>,
    IUserPasswordStore<TUser>,
    IUserSecurityStampStore<TUser>,
    IUserTwoFactorStore<TUser>,
    IUserPhoneNumberStore<TUser>,
    IUserEmailStore<TUser>,
    IUserLockoutStore<TUser>,
    IUserTwoFactorRecoveryCodeStore<TUser>,
    IUserAuthenticatorKeyStore<TUser>,
    IQueryableUserStore<TUser>
    where TUser : MartenIdentityUser<TRole>
    where TRole : MartenIdentityRole
{
    private const string InternalLoginProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";

    public IQueryable<TUser> Users
    {
        get => session.Query<TUser>();
    }

    public void Dispose()
    {
    }

    public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
    {
        try
        {
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(
                new IdentityError() { Code = "CREATE USER ERROR", Description = ex.Message });
        }
    }

    public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
    {
        try
        {
            session.Delete(user.Id);
            await session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(
                new IdentityError() { Code = "CREATE USER ERROR", Description = ex.Message });
        }
    }


    public async Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var parsedSuccessfully = Guid.TryParse(userId, out Guid id);
            if (!parsedSuccessfully)
            {
                return null;
            }

            return await session.LoadAsync<TUser>(id);
        }
        catch
        {
            return null;
        }
    }

    public async Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        try
        {
            return await session
                .Query<TUser>()
                .SingleOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user?.NormalizedUserName);
    }

    public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.UserName);
    }

    public Task SetNormalizedUserNameAsync(TUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
    {
        try
        {
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError() { Code = "UPDATE USER ERROR", Description = ex.Message });
        }
    }

    public Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken)
        => Task.FromResult((IList<Claim>)user.Claims.Select(x => x.ToClaim()).ToList());

    public Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        foreach (var claim in claims)
        {
            user.Claims.Add(MartenIdentityClaim.FromClaim(claim));
        }

        return Task.CompletedTask;
    }

    public Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
    {
        var matchedClaims = user.Claims.Where(martenClaim =>
            string.Equals(martenClaim.Value, claim.Value, StringComparison.Ordinal)
            && string.Equals(martenClaim.Type, claim.Type, StringComparison.Ordinal)
        );
        foreach (var matchedClaim in matchedClaims)
        {
            matchedClaim.Value = newClaim.Value;
            matchedClaim.Type = newClaim.Type;
        }

        return Task.CompletedTask;
    }

    public Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        foreach (var claim in claims)
        {
            var matchedClaims = user.Claims.Where(uc =>
                string.Equals(uc.Value, claim.Value, StringComparison.Ordinal)
                && string.Equals(uc.Type, claim.Type, StringComparison.Ordinal)
            ).ToList();

            foreach (var c in matchedClaims)
            {
                user.Claims.Remove(c);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        var users = await session
            .Query<TUser>()
            .Where(u => u.Claims.Any(c => c.Type == claim.Type && c.Value == claim.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new List<TUser>(users);
    }

    public Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        user.Logins.Add(MartenIdentityUserLogin.FromLoginInfo(login));
        return Task.CompletedTask;
    }

    public Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey,
        CancellationToken cancellationToken)
    {
        var login = user.Logins.SingleOrDefault(l =>
            string.Equals(l.LoginProvider, loginProvider, StringComparison.Ordinal)
            && string.Equals(l.ProviderKey, providerKey, StringComparison.Ordinal));

        if (login != null)
        {
            user.Logins.Remove(login);
        }

        return Task.CompletedTask;
    }

    public Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken)
    {
        var logins = user.Logins.Select(l => l.ToUserLoginInfo()).ToList();
        return Task.FromResult<IList<UserLoginInfo>>(logins);
    }

    public async Task<TUser?> FindByLoginAsync(string loginProvider, string providerKey,
        CancellationToken cancellationToken)
    {
        var user = await session
            .Query<TUser>()
            .SingleOrDefaultAsync(
                u => u.Logins.Any(
                    l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey
                ),
                cancellationToken)
            .ConfigureAwait(false);

        return user;
    }

    public async Task AddToRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        var roleEntity = await session
            .Query<TRole>()
            .SingleOrDefaultAsync(r => r.NormalizedName == roleName, token: cancellationToken)
            .ConfigureAwait(false);
        if (roleEntity == null)
        {
            throw new InvalidOperationException($"Role '{roleName}' not found.");
        }

        user.Roles.Add(roleEntity);
    }

    public Task RemoveFromRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        var role = user.Roles.SingleOrDefault(r => string.Equals(r.NormalizedName, roleName, StringComparison.Ordinal));
        if (role != null)
        {
            user.Roles.Remove(role);
        }

        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken)
    {
        var roles = user.Roles.Select(r => r.Name!).ToList();

        return Task.FromResult<IList<string>>(roles);
    }

    public Task<bool> IsInRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        var result = user.Roles.Any(r => string.Equals(r.NormalizedName, roleName, StringComparison.Ordinal));
        return Task.FromResult(result);
    }

    public async Task<IList<TUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var result = await session
            .Query<TUser>()
            .Where(u => u.Roles.Any(r => r.NormalizedName == roleName))
            .ToListAsync(token: cancellationToken)
            .ConfigureAwait(false);

        return new List<TUser>(result);
    }

    public Task SetPasswordHashAsync(TUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.FromResult(user.PasswordHash);
    }

    public Task<string?> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PasswordHash != null);
    }

    public Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.SecurityStamp);
    }

    public Task SetTwoFactorEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.TwoFactorEnabled);
    }

    public Task SetPhoneNumberAsync(TUser user, string? phoneNumber, CancellationToken cancellationToken)
    {
        user.PhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    public Task<string?> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PhoneNumber);
    }

    public Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PhoneNumberConfirmed);
    }

    public Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<TUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var user = await session
            .Query<TUser>()
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, token: cancellationToken)
            .ConfigureAwait(false);

        return user;
    }

    public Task<string?> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.NormalizedEmail);
    }

    public Task SetNormalizedEmailAsync(TUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.LockoutEnd);
    }

    public Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.LockoutEnabled);
    }

    public Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task ReplaceCodesAsync(TUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        user.RecoveryCodes.Clear();
        foreach (var recoveryCode in recoveryCodes)
        {
            user.RecoveryCodes.Add(new MartenIdentityRecoveryCode() { Code = recoveryCode });
        }

        return Task.CompletedTask;
    }

    public Task<bool> RedeemCodeAsync(TUser user, string code, CancellationToken cancellationToken)
    {
        var martenRecoveryCode = new MartenIdentityRecoveryCode() { Code = code };
        if (!user.RecoveryCodes.Contains(martenRecoveryCode))
        {
            return Task.FromResult(false);
        }

        user.RecoveryCodes.Remove(martenRecoveryCode);
        return Task.FromResult(true);
    }

    public Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.RecoveryCodes.Count);
    }

    private static MartenIdentityUserToken? FindUserToken(TUser user, string loginProvider, string name)
    {
        return user.Tokens.SingleOrDefault(t =>
            string.Equals(t.LoginProvider, loginProvider, StringComparison.Ordinal)
            && string.Equals(t.Name, name, StringComparison.Ordinal)
        );
    }

    public Task SetAuthenticatorKeyAsync(TUser user, string key, CancellationToken cancellationToken)
    {
        var token = FindUserToken(user, InternalLoginProvider, AuthenticatorKeyTokenName);
        if (token == null)
        {
            var martenIdentityUserToken = new MartenIdentityUserToken
            {
                LoginProvider = InternalLoginProvider,
                Name = AuthenticatorKeyTokenName,
                Value = key
            };
            user.Tokens.Add(martenIdentityUserToken);
        }
        else
        {
            token.Value = key;
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken)
    {
        var martenIdentityUserToken = FindUserToken(user, InternalLoginProvider, AuthenticatorKeyTokenName);
        return Task.FromResult(martenIdentityUserToken?.Value);
    }
}
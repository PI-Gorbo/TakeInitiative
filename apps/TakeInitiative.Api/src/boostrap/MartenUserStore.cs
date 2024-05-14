using CSharpFunctionalExtensions;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Bootstrap;
public class MartenUserStore :
    IUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>
{
    private IDocumentSession session;
    public MartenUserStore(IDocumentStore Store)
    {
        session = Store.LightweightSession();
    }
    public void Dispose()
    {
        session.Dispose();
    }

    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
        }).Finally(outcome =>
        {
            if (outcome.IsSuccess)
            {
                return IdentityResult.Success;
            }
            return IdentityResult.Failed(new IdentityError() { Code = "CREATE USER ERROR", Description = outcome.Error });
        });
    }

    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            session.Delete(user.Id);
            await session.SaveChangesAsync(cancellationToken);
        }).Finally(outcome =>
        {
            if (outcome.IsSuccess)
            {
                return IdentityResult.Success;
            }
            return IdentityResult.Failed(new IdentityError() { Code = "CREATE USER ERROR", Description = outcome.Error });
        });
    }

    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            var parsedSuccessfully = Guid.TryParse(userId, out Guid id);
            if (!parsedSuccessfully)
            {
                return null;
            }
            return await session.LoadAsync<ApplicationUser>(id);
        }).Finally(outcome => outcome.IsSuccess ? outcome.Value : null);
    }

    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            return await session
                .Query<ApplicationUser>()
                .SingleOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken);
        }).Finally(x => x.IsSuccess ? x.Value : null);
    }

    public async Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return user?.NormalizedUserName;
    }

    public async Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return user.Id.ToString();
    }

    public async Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return user.UserName;
    }

    public async Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        await Result.Try(async () =>
        {
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
        });
    }

    public async Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        await Result.Try(async () =>
        {
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
        });
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return await Result.Try(async () =>
        {
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
        }).Finally(outcome =>
            outcome.IsSuccess ?
                IdentityResult.Success
                :
                IdentityResult.Failed(new IdentityError() { Code = "UPDATE USER ERROR", Description = outcome.Error })
        );
    }


    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Result.Try(async () =>
        {
            session.Store<ApplicationUser>(user);
            await session.SaveChangesAsync();
        });
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PasswordHash != null);
    }

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            user.Email = email;
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
        });
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            user.EmailConfirmed = confirmed;
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
        });
    }

    public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            return session.Query<ApplicationUser>().SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        }).Finally(x => x.IsSuccess ? x.Value : null);
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.NormalizedEmail);
    }

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            user.NormalizedEmail = normalizedEmail;
            session.Store(user);
            await session.SaveChangesAsync(cancellationToken);
        });
    }
}
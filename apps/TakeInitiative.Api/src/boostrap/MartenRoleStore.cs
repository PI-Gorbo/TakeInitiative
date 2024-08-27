using System.Security.Claims;
using CSharpFunctionalExtensions;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Bootstrap;
public class MartenRoleStore :
    IRoleClaimStore<ApplicationUserRole>
{
    private IDocumentSession session;
    public MartenRoleStore(IDocumentStore Store)
    {
        session = Store.LightweightSession();
    }

    public void Dispose()
    {
        session.Dispose();
        return;
    }

    public Task AddClaimAsync(ApplicationUserRole role, Claim claim, CancellationToken cancellationToken = default)
    {
        return Result.Try(async () =>
        {
            role.Claims.Add(claim);
            session.Store(role);
            await session.SaveChangesAsync(cancellationToken);
        });
    }

    public Task<IdentityResult> CreateAsync(ApplicationUserRole role, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            session.Store(role);
            await session.SaveChangesAsync(cancellationToken);
        }).Finally(
            x => x.IsSuccess ? IdentityResult.Success : IdentityResult.Failed(new IdentityError() { Code = "CREATE ROLE ERROR", Description = x.Error })
        );
    }

    public Task<IdentityResult> DeleteAsync(ApplicationUserRole role, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            session.Delete<ApplicationUserRole>(role);
            await session.SaveChangesAsync();
        }).Finally(
            x => x.IsSuccess ? IdentityResult.Success : IdentityResult.Failed(new IdentityError() { Code = "DELETE ROLE ERROR", Description = x.Error })
        );
    }


    public Task<ApplicationUserRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            var validId = Guid.TryParse(roleId, out Guid parsedId);
            if (!validId)
            {
                return null;
            }

            return await session.Query<ApplicationUserRole>().SingleOrDefaultAsync(x => x.Id == parsedId);
        }).Finally(x => x.IsSuccess ? x.Value : null);
    }

    public Task<ApplicationUserRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            return await session.Query<ApplicationUserRole>().SingleOrDefaultAsync(x => x.NormalizedName == normalizedRoleName);
        }).Finally(x => x.IsSuccess ? x.Value : null);
    }

    public Task<IList<Claim>> GetClaimsAsync(ApplicationUserRole role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(role.Claims);
    }

    public Task<string?> GetNormalizedRoleNameAsync(ApplicationUserRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.NormalizedName);
    }

    public Task<string> GetRoleIdAsync(ApplicationUserRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Id.ToString());
    }

    public Task<string?> GetRoleNameAsync(ApplicationUserRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Name);
    }

    public Task RemoveClaimAsync(ApplicationUserRole role, Claim claim, CancellationToken cancellationToken = default)
    {
        return Result.Try(async () =>
        {
            role.Claims.Add(claim);
            await session.SaveChangesAsync(cancellationToken);
        });
    }

    public Task SetNormalizedRoleNameAsync(ApplicationUserRole role, string? normalizedName, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            role.NormalizedName = normalizedName;
            session.Store(role);
            await session.SaveChangesAsync();
        });

    }

    public Task SetRoleNameAsync(ApplicationUserRole role, string? roleName, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
        {
            role.Name = roleName;
            session.Store(role);
            await session.SaveChangesAsync();
        });
    }

    public Task<IdentityResult> UpdateAsync(ApplicationUserRole role, CancellationToken cancellationToken)
    {
        return Result.Try(async () =>
       {
           session.Store(role);
           await session.SaveChangesAsync();
       }).Finally(
            x => x.IsSuccess ? IdentityResult.Success : IdentityResult.Failed(new IdentityError() { Code = "UPDATE ROLE ERROR", Description = x.Error })
        );
    }
}
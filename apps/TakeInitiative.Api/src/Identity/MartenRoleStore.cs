using Marten;
using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Identity;
public class MartenRoleStore<TRole>(IDocumentSession session) : IRoleStore<TRole>, IQueryableRoleStore<TRole> where TRole : MartenIdentityRole
{
    public IQueryable<TRole> Roles { get => session.Query<TRole>(); }

    public void Dispose()
    {
        return;
    }

    public async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken)
    {
        session.Insert(role);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken)
    {
        session.Delete(role);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }


    public async Task<TRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        var role = await session
           .Query<TRole>()
           .SingleOrDefaultAsync(r => r.Id == Guid.Parse(roleId), token: cancellationToken)
           .ConfigureAwait(false);

        return role;
    }

    public async Task<TRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        var role = await session
                  .Query<TRole>()
                  .SingleOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, token: cancellationToken)
                  .ConfigureAwait(false);

        return role;
    }

    public Task<string?> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.NormalizedName);
    }

    public Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Id.ToString());
    }

    public Task<string?> GetRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Name);
    }

    public Task SetNormalizedRoleNameAsync(TRole role, string? normalizedName, CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetRoleNameAsync(TRole role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken)
    {
        session.Update(role);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }
}
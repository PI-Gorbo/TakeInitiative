namespace TakeInitiative.Api.Identity;
public static class Extensions
{
    public static Marten.StoreOptions RegisterIdentityModels<TUser, TRole>(this Marten.StoreOptions opts)
        where TUser : MartenIdentityUser<TRole>
        where TRole : MartenIdentityRole
    {
        opts.Schema.For<TUser>().Index(x => x.NormalizedEmail!, x => { x.IsUnique = true; });
        opts.Schema.For<TRole>();
        return opts;
    }
}
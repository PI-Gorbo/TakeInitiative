using System.Security.Claims;

namespace TakeInitiative.Api.Identity;

public record MartenIdentityClaim
{
    public required string Type { get; set; }
    public required string Value { get; set; }
    public required string Issuer { get; set; }

    public Claim ToClaim() => new Claim(Type, Value, null, Issuer);

    public static MartenIdentityClaim FromClaim(Claim claim) => new MartenIdentityClaim()
    {
        Type = claim.Type,
        Value = claim.Value,
        Issuer = claim.Issuer,
    };
}


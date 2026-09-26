namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>
/// A user in a campaign. Membership lives only in <see cref="Campaign.Members"/>.
/// <see cref="MemberId"/> is per campaign; <see cref="UserId"/> is the account.
/// </summary>
public record Member
{
    public required Guid MemberId { get; init; }
    public required Guid UserId { get; init; }
    public required Role Role { get; init; }
    public required DateTimeOffset JoinedAt { get; init; }
}

namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>
/// The one shape of the visibility rule (invariant 5), shared by session notes, entries
/// and (from 15e) secret blocks: <c>Everyone</c>, <c>all DMs plus the owner</c> or
/// <c>the owner only</c>. The owner is a note's author or an entry's creator.
/// <see cref="Contains"/> is the in-memory read rule and <see cref="Groups"/> the push
/// rule; a viewer is in the audience exactly when they are in one of the groups
/// <see cref="CampaignGroups.Of"/> puts them in. The unit tests check that for every case.
/// </summary>
public sealed record Audience
{
    public enum Scope
    {
        Everyone,
        DmsAndOwner,
        OwnerOnly,
    }

    public Scope Reach { get; }
    public Guid OwnerMemberId { get; }

    private Audience(Scope reach, Guid ownerMemberId)
    {
        Reach = reach;
        OwnerMemberId = ownerMemberId;
    }

    public static Audience Of(Visibility visibility, Guid ownerMemberId) => visibility switch
    {
        Visibility.Everyone => new(Scope.Everyone, ownerMemberId),
        Visibility.DM => new(Scope.DmsAndOwner, ownerMemberId),
        _ => new(Scope.OwnerOnly, ownerMemberId),
    };

    /// <summary>Whether <paramref name="viewer"/> may see what this audience covers.</summary>
    public bool Contains(Member viewer) => viewer.MemberId == OwnerMemberId || Reach switch
    {
        Scope.Everyone => true,
        Scope.DmsAndOwner => viewer.Role == Role.DM,
        _ => false,
    };

    /// <summary>The hub groups to push to. A narrower audience never includes <c>campaign:{id}</c>.</summary>
    public IReadOnlyList<string> Groups(Guid campaignId) => Reach switch
    {
        Scope.Everyone => [CampaignGroups.Campaign(campaignId)],
        Scope.DmsAndOwner => [CampaignGroups.Dms(campaignId), CampaignGroups.Member(OwnerMemberId)],
        _ => [CampaignGroups.Member(OwnerMemberId)],
    };
}

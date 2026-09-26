using Marten;

namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>A campaign as its members see it. Returned by every campaign endpoint that writes or reads one campaign.</summary>
public record CampaignResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string JoinCode { get; init; }
    public required Guid OwnerMemberId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>The caller's member id in this campaign.</summary>
    public required Guid CurrentMemberId { get; init; }
    public required CampaignMemberResponse[] Members { get; init; }

    public static async Task<CampaignResponse> Build(IQuerySession session, Campaign campaign, Guid callerUserId, CancellationToken ct)
    {
        var userIds = campaign.Members.Select(m => m.UserId).ToArray();
        var usernames = (await session.Query<ApplicationUser>()
                .Where(u => u.Id.IsOneOf(userIds))
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync(ct))
            .ToDictionary(u => u.Id, u => u.UserName ?? "");

        return new CampaignResponse
        {
            Id = campaign.Id,
            Name = campaign.Name,
            JoinCode = campaign.JoinCode,
            OwnerMemberId = campaign.OwnerMemberId,
            CreatedAt = campaign.CreatedAt,
            CurrentMemberId = campaign.MemberForUser(callerUserId)!.MemberId,
            Members = campaign.Members
                .Select(m => new CampaignMemberResponse
                {
                    MemberId = m.MemberId,
                    UserId = m.UserId,
                    Username = usernames.GetValueOrDefault(m.UserId, ""),
                    Role = m.Role,
                    JoinedAt = m.JoinedAt,
                    IsOwner = campaign.IsOwner(m.MemberId),
                })
                .ToArray(),
        };
    }
}

public record CampaignMemberResponse
{
    public required Guid MemberId { get; init; }
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required Role Role { get; init; }
    public required DateTimeOffset JoinedAt { get; init; }
    public required bool IsOwner { get; init; }
}

using System.Security.Cryptography;
using Marten.Events;

namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>
/// Inline projection of the Campaign stream (one stream per campaign, stream id = campaign id).
/// This document is the only place campaign membership is stored.
/// </summary>
public record Campaign
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string JoinCode { get; init; } = "";
    public Guid OwnerMemberId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public List<Member> Members { get; init; } = [];

    public static Campaign Create(IEvent<CampaignCreated> @event)
    {
        var e = @event.Data;
        return new Campaign
        {
            Id = @event.StreamId,
            Name = e.Name,
            JoinCode = e.JoinCode,
            OwnerMemberId = e.OwnerMemberId,
            CreatedAt = @event.Timestamp,
            Members =
            [
                new Member
                {
                    MemberId = e.OwnerMemberId,
                    UserId = e.OwnerUserId,
                    Role = Role.DM,
                    JoinedAt = @event.Timestamp,
                },
            ],
        };
    }

    public Campaign Apply(IEvent<MemberJoined> @event)
    {
        var e = @event.Data;
        if (Members.Any(m => m.UserId == e.UserId))
        {
            return this;
        }

        return this with
        {
            Members =
            [
                .. Members,
                new Member { MemberId = e.MemberId, UserId = e.UserId, Role = Role.Player, JoinedAt = @event.Timestamp },
            ],
        };
    }

    public Campaign Apply(MemberRoleChanged e)
    {
        // The owner is always a DM, whatever the event says.
        if (e.MemberId == OwnerMemberId)
        {
            return this;
        }

        return this with
        {
            Members = Members.Select(m => m.MemberId == e.MemberId ? m with { Role = e.Role } : m).ToList(),
        };
    }

    public Member? MemberForUser(Guid userId) => Members.SingleOrDefault(m => m.UserId == userId);
    public Member? MemberById(Guid memberId) => Members.SingleOrDefault(m => m.MemberId == memberId);
    public bool IsOwner(Guid memberId) => OwnerMemberId == memberId;

    private const string JoinCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I/L
    public const int JoinCodeLength = 8;

    public static string NewJoinCode()
        => new(Enumerable.Range(0, JoinCodeLength)
            .Select(_ => JoinCodeAlphabet[RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length)])
            .ToArray());

    public static string NormaliseJoinCode(string joinCode) => joinCode.Trim().ToUpperInvariant();
}

namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>The owner changed a member's role.</summary>
public sealed record MemberRoleChanged(Actor Actor, Guid MemberId, Role Role) : IActorEvent;

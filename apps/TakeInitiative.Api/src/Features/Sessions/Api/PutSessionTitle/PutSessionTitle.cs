using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record PutSessionTitleRequest
{
    public Guid CampaignId { get; init; }
    public Guid SessionId { get; init; }
    /// <summary>The new title. Null or blank clears it.</summary>
    public string? Title { get; init; }
}

public class PutSessionTitleRequestValidator : Validator<PutSessionTitleRequest>
{
    public PutSessionTitleRequestValidator()
    {
        RuleFor(x => Session.NormaliseTitle(x.Title))
            .MaximumLength(Session.TitleMaxLength)
            .OverridePropertyName(nameof(PutSessionTitleRequest.Title));
    }
}

/// <summary>A DM sets or clears a session's title. An unchanged title appends nothing.</summary>
public class PutSessionTitle(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutSessionTitleRequest, SessionResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/sessions/{SessionId}/title");
    }

    public override async Task HandleAsync(PutSessionTitleRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        this.RequireDm(member, "Only a DM can change a session's title.");

        var target = await this.RequireSession(session, req.CampaignId, req.SessionId, ct);
        var title = Session.NormaliseTitle(req.Title);
        var changed = target.Title != title;
        if (changed)
        {
            session.Events.Append(target.Id, new SessionTitleChanged(Actor.Member(member.MemberId), title));
            await session.SaveChangesAsync(ct);
            target = (await session.LoadAsync<Session>(target.Id, ct))!;
        }

        var current = await this.RequireCurrentSession(session, req.CampaignId, ct);
        var response = SessionResponse.From(target, current.Id);
        if (changed)
        {
            await hub.NotifySessionTitleChanged(req.CampaignId, response);
        }
        await SendAsync(response, cancellation: ct);
    }
}

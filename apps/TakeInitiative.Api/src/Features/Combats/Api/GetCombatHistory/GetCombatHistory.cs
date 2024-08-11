using System.Net;
using FastEndpoints;
using JasperFx.Core;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class GetCombatHistory(IDocumentSession session) :
    Endpoint<GetCombatRequest, GetCombatHistoryResponse>
{

    public override void Configure()
    {
        Get("/api/combat/{id}/history");
    }

    public override async Task HandleAsync(GetCombatRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var events = await session.Events.FetchStreamAsync(req.Id);
        await SendAsync(new GetCombatHistoryResponse
        {
            Events = events
                .Where(x => !x.EventType.IsAssignableTo(typeof(IHistoryVisibleCombatEvent)))
                .Select(x => new HistoryEvent { EventName = x.EventTypeName, UserId = (x.Data as ICombatEvent)?.UserId ?? Guid.Empty })
                .ToArray(),
            PlayerList = events
                .Select(x => (x.Data as ICombatEvent)?.UserId ?? Guid.Empty)
                .Distinct()
                .ToArray(),
        });
    }
}

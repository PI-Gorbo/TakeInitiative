using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class DeletePlayerCharacter(IDocumentSession session) : Endpoint<DeletePlayerCharacterRequest, CampaignMember>
{
    public override void Configure()
    {
        Delete("/api/campaign/member/character");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(DeletePlayerCharacterRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Result
            .Try(async () => await session.LoadAsync<CampaignMember>(req.MemberId), ApiError.DbInteractionFailed)
                .EnsureNotNull(ApiError.NotFound("No Campaign Member with the given id exists."))
                .Ensure((member) => member.UserId == userId, "Cannot edit Campaign Member details of others")
            .Bind(async (member) =>
            {
                // Ensure the player character exists.
                var character = member.Characters.SingleOrDefault(x => x.Id == req.PlayerCharacterId);
                if (character == null)
                {
                    return ApiError.NotFound("There are no characters with the given id.");
                }

                session.Store(member);
                await session.SaveChangesAsync();

                return Result.Success<CampaignMember, ApiError>(member);
            });

        await this.ReturnApiResult(result);
    }
}

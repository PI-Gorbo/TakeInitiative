using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using JasperFx.Core;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class PutPlayerCharacter(IDocumentSession session) : Endpoint<PutPlayerCharacterRequest, CampaignMember>
{
    public override void Configure()
    {
        Post("/api/campaign/member/character");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(PutPlayerCharacterRequest req, CancellationToken ct)
    {

        var userId = this.GetUserIdOrThrowUnauthorized();

        // Construct new player character 
        PlayerCharacter newCharacter = PlayerCharacter.
            New(userId, req.PlayerCharacter.Name, req.PlayerCharacter.Initiative, req.PlayerCharacter.ArmorClass, req.PlayerCharacter.Health);

        var result = await Result
            .Try(
                async () => await session.LoadAsync<CampaignMember>(req.CampaignMemberId),
                ApiError.DbInteractionFailed
            )
                .EnsureNotNull(ApiError.NotFound("No Campaign Member with the given id exists."))
                .Ensure((campaignMember) => campaignMember.UserId == userId, "Cannot edit Campaign Member details of others")
            .Bind(async campaignMember =>
            {
                var index = campaignMember.Characters.FindIndex(x => x.Id == req.CampaignMemberId);
                if (index == -1)
                {
                    return ApiError.NotFound("There is no character with the provided character id.");
                }

                // Edit the character
                var character = campaignMember.Characters[index];
                character.Name = req.PlayerCharacter.Name;
                character.Health = req.PlayerCharacter.Health;
                character.Initiative = req.PlayerCharacter.Initiative;
                character.ArmorClass = req.PlayerCharacter.ArmorClass;

                session.Store(campaignMember);
                await session.SaveChangesAsync();
                return Result.Success<CampaignMember, ApiError>(campaignMember);
            });


        await this.ReturnApiResult(result);
    }
}

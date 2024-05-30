using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class PostPlayerCharacter(IDocumentSession session) : Endpoint<PostPlayerCharacterRequest, CampaignMember>
{
    public override void Configure()
    {
        Post("/api/campaign/member/character");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(PostPlayerCharacterRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        // Construct new player character 
        PlayerCharacter newCharacter = PlayerCharacter.
            New(userId, req.PlayerCharacter.Name, req.PlayerCharacter.Initiative, req.PlayerCharacter.ArmourClass, req.PlayerCharacter.Health);

        // Validate and add the new character
        var result = await Result
            .Try(async () => await session.LoadAsync<CampaignMember>(req.CampaignMemberId), ApiError.DbInteractionFailed)
                .EnsureNotNull(ApiError.NotFound("No Campaign Member with the given id exists."))
                .Ensure(member => member.UserId == userId, "Cannot edit campaign member detail of others.")
                .Ensure(member => !member.Characters.Any(x => x.Name == newCharacter.Name), $"Character name is not unique.There is already a character named {newCharacter.Name}")
            .TapTry(async (member) =>
            {
                member.Characters.Add(newCharacter);
                session.Store(member);
                await session.SaveChangesAsync();
            }, ApiError.DbInteractionFailed);

        await this.ReturnApiResult(result);
    }
}

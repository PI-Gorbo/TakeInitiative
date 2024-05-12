using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features;

public class PostAddPlayerCharacter(IDocumentStore Store) : Endpoint<AddPlayerCharacterRequest, CampaignMember>
{
    public override void Configure()
    {
        Post("/api/campaign/member/character");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(AddPlayerCharacterRequest req, CancellationToken ct)
    {

        var userId = this.GetUserIdOrThrowUnauthorized();

        // Construct new player character 
        PlayerCharacter newCharacter = PlayerCharacter.
            New(userId, req.PlayerCharacter.Name, req.PlayerCharacter.Initiative, req.PlayerCharacter.ArmorClass, req.PlayerCharacter.Health);

        var result = await Store.Try(async session =>
        {
            var campaignMember = await session.LoadAsync<CampaignMember>(req.CampaignMemberId);
            if (campaignMember == null)
            {
                ThrowError("No Campaign Member with the given id exists.", (int)HttpStatusCode.NotFound);
            }

            if (campaignMember.UserId != userId)
            {
                ThrowError("Cannot edit Campaign Member details of others", (int)HttpStatusCode.Unauthorized);
            }

            // Validate the character name is unique
            var characterNameIsUnique = !campaignMember.Characters.Any(x => x.Name == newCharacter.Name);
            if (!characterNameIsUnique)
            {
                ThrowError($"Character name is not unique.There is already a character named {newCharacter.Name}", (int)HttpStatusCode.BadRequest);
            }

            campaignMember.Characters.Add(newCharacter);

            if (campaignMember.CurrentCharacterId == null)
            {
                campaignMember.CurrentCharacterId = newCharacter.Id;
            }

            session.Store(campaignMember);
            await session.SaveChangesAsync();
            return campaignMember;
        });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}

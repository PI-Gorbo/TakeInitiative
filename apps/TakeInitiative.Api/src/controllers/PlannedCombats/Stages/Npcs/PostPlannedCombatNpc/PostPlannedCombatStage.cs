using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public record PostPlannedCombatNpcRequest {
	public required Guid CombatId {get; set;}
	public required Guid StageId {get; set;}
	public required PlannedCombatNonPlayerCharacter Npc {get; set;}
	
}

public class PostPlannedCombatNpcRequestValidator : Validator<PostPlannedCombatNpcRequest>
{
	public PostPlannedCombatNpcRequestValidator()
	{
		RuleFor(x => x.CombatId).NotEmpty();
		RuleFor(x => x.StageId).NotEmpty();
		RuleFor(x => x.Npc)
			.NotNull()
			.SetValidator(new PlannedCombatNonPlayerCharacterValidator)
	}
}


public class PostPlannedCombatNpc(IDocumentStore Store) : Endpoint<PostPlannedCombatNpcRequest, PlannedCombat>
{
    public override void Configure()
    {
        Post("/api/campaign/planned-combat/stage/npc");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(PostPlannedCombatNpcRequest req, CancellationToken ct)
    {

        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Store.Try(async session =>
        {
            var combat = await session.LoadAsync<PlannedCombat>(req.CombatId);
            if (combat == null)
            {
                ThrowError(x => x.CombatId, "Combat id does not correlate to any known combats.", (int)HttpStatusCode.NotFound);
            }

            // Verify that the user is a member of the campaign & a DM.
            var campaign = await session.LoadAsync<Campaign>(combat.CampaignId);
            if (campaign == null)
            {
                ThrowError(x => x.CombatId, "The combat does not belong to a campaign.", (int)HttpStatusCode.NotFound);
            }

            if (!campaign.isDm(userId))
            {
                ThrowError("Planned combats can only be edited by DMs.");
            }

            // Attempt to add the stage to the planned combat.
            var result = combat.AddStage(PlannedCombatStage.New(req.Name));
            if (result.IsFailure)
            {
                ThrowError(x => x.Name, result.Error);
            }

            session.Store(combat);
            await session.SaveChangesAsync();

            return combat;
        });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}

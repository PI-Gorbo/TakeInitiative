using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;
public record DeletePlayerCharacterRequest
{
	public required Guid MemberId { get; set; }
	public required Guid PlayerCharacterId { get; set; }
}

public class DeletePlayerCharacter(IDocumentStore Store) : Endpoint<DeletePlayerCharacterRequest, CampaignMember>
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

		var result = await Store.Try(async session =>
		{
			var campaignMember = await session.LoadAsync<CampaignMember>(req.MemberId);
			if (campaignMember == null)
			{
				ThrowError("No Campaign Member with the given id exists.", (int)HttpStatusCode.NotFound);
			}

			// Ensure only the owner of the campaign member details can edit
			if (campaignMember.UserId != userId)
			{
				ThrowError("Cannot edit Campaign Member details of others", (int)HttpStatusCode.Unauthorized);
			}

			// Ensure the player character exists.
			var character = campaignMember.Characters.SingleOrDefault(x => x.Id == req.PlayerCharacterId);
			if (character == null)
			{
				ThrowError("There are no characters with the given id.", (int)HttpStatusCode.NotFound);
			}

			campaignMember.Characters = campaignMember.Characters.Where(x => x.Id != req.PlayerCharacterId).ToList();
			if (campaignMember.Characters.Count == 0)
			{
				campaignMember.CurrentCharacterId = null;
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

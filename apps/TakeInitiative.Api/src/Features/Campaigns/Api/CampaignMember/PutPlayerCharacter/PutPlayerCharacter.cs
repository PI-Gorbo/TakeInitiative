using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class PutPlayerCharacter(IDocumentSession session) : Endpoint<PutPlayerCharacterRequest, CampaignMember>
{
    public override void Configure()
    {
        Put("/api/campaign/member/character");
    }

    public override async Task HandleAsync(PutPlayerCharacterRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Result
            .Try(
                async () => await session.LoadAsync<CampaignMember>(req.CampaignMemberId),
                ApiError.DbInteractionFailed
            )
                .EnsureNotNull(ApiError.NotFound("No Campaign Member with the given id exists."))
                .Ensure((campaignMember) => campaignMember.UserId == userId, "Cannot edit Campaign Member details of others")
            .Bind(async campaignMember =>
            {
                var index = campaignMember.Characters.FindIndex(x => x.Id == req.PlayerCharacterId);
                if (index == -1)
                {
                    return ApiError.NotFound("There is no character with the provided character id.");
                }

                // Edit the character
                var character = campaignMember.Characters[index] = campaignMember.Characters[index] with
                {
                    Name = req.PlayerCharacter.Name,
                    Health = req.PlayerCharacter.Health,
                    Initiative = req.PlayerCharacter.Initiative,
                    ArmourClass = req.PlayerCharacter.ArmourClass,
                };

                session.Store(campaignMember);
                await session.SaveChangesAsync();
                return Result.Success<CampaignMember, ApiError>(campaignMember);
            });


        await this.ReturnApiResult(result);
    }
}

using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public record StagePlayerCharactersCommand : ICommand<Result<Combat, ApiError>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
    public required Guid[] CharacterIds { get; set; }
}

public class StagePlayerCharactersCommandHandler(IDocumentSession session) : CommandHandler<StagePlayerCharactersCommand, Result<Combat, ApiError>>
{
    public override async Task<Result<Combat, ApiError>> ExecuteAsync(StagePlayerCharactersCommand command, CancellationToken ct = default)
    {
        // Fetch the combat.
        var combat = await session.LoadAsync<Combat>(command.CombatId);
        if (combat == null)
        {
            return ApiError.Invalid<StagePlayerCharactersCommand>(x => x.CombatId, "Combat does not exist");
        }

        // Check the state of the combat.
        if (combat.State == CombatState.Paused || combat.State == CombatState.Finished)
        {
            return ApiError.BadRequest($"Cannot activate character because the combat is {combat.State.ToString().ToLower()}.");
        }


        // Fetch the characters from the player's campaign member entry
        return await Result
            .Try(
                async () => await session
                    .Query<CampaignMember>()
                    .Where(member => member.CampaignId == combat.CampaignId && member.UserId == command.UserId)
                    .FirstOrDefaultAsync(),
                ApiError.DbInteractionFailed
            )
                .EnsureNotNull("There is no campaign member entry found for the given user.")
            .Bind((CampaignMember member) =>
            {
                // Check that all the ids match to real characters
                var characters = member.Characters.Where(x => x.Id.In(command.CharacterIds)).ToArray();
                if (characters.Length != command.CharacterIds.Length)
                {
                    return ApiError.BadRequest("One or more of the given ids do not correspond to characters for the player.");
                }

                return Result.Success<Character[], ApiError>(characters);
            })
            .MapTry(async (characters) =>
            {
                // Publish the event
                StagedPlayerCharacterEvent activateEvent = new()
                {
                    UserId = command.UserId,
                    Characters = characters
                };
                session.Events.Append(command.CombatId, activateEvent);
                await session.SaveChangesAsync();

                combat = await session.LoadAsync<Combat>(command.CombatId);
                return combat;

            }, ApiError.DbInteractionFailed);
    }
}

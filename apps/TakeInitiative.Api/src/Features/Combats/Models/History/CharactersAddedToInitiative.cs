namespace TakeInitiative.Api.Features.Combats;

public record CharactersAddedToInitiative : HistoryEvent
{
    public required InitiativeRolledDto[] NewInitiativeList { get; set; }
}

namespace TakeInitiative.Api.Features.Combats;
public interface ICombatEvent
{
    public Guid UserId { get; }
}

public interface IHistoryVisibleCombatEvent : ICombatEvent { }
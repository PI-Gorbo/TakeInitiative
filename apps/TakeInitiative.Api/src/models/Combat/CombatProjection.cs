using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Models;

public partial class CombatProjection : SingleStreamProjection<Combat>
{
    // COMBAT LIFECYCLE //
    public async Task<Combat> Apply(CombatOpenedEvent @event, Combat Combat, IEvent<CombatOpenedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat.New(
            Id: Combat.Id,
            CampaignId: @event.CampaignId,
            CombatName: @event.CombatName,
            State: CombatState.Open,
            DungeonMaster: @event.UserId,
            Timing: [
                new CombatTimingRecord(StartTime: eventDetails.Timestamp, EndTime: null)
            ],
            CombatLogs: [$"{user?.UserName} opened the Combat at {eventDetails.Timestamp:R}"],
            CurrentPlayers: [],
            PlannedStages: @event.Stages.ToImmutableList(),
            InitiativeList: [],
            StagedList: []
        );
    }

    public async Task<Combat> Apply(CombatStartedEvent @event, Combat Combat, IEvent<CombatStartedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var newInitiativeList = @event.InitiativeRolls.Select((charInitiative, index) =>
        {
            return Combat.StagedList.First(x => x.Id == charInitiative.id) with
            {
                InitiativeValue = charInitiative.rolls
            };
        })
        .OrderByDescending(x => x.InitiativeValue, new InitiativeComparer())
        .ToImmutableList();

        return Combat with
        {
            State = CombatState.Started,
            CombatLogs = Combat.CombatLogs.Add($"{user?.UserName} started the combat at {eventDetails.Timestamp:R}"),
            StagedList = [],
            InitiativeList = newInitiativeList,
            RoundNumber = 1,
            CurrentInitiative = newInitiativeList.FirstOrDefault()?.InitiativeValue ?? [],
        };
    }


    public async Task<Combat> Apply(CombatResumedEvent @event, Combat Combat, IEvent<CombatResumedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            Timing = Combat.Timing.Add(new(StartTime: eventDetails.Timestamp, EndTime: null)),
            State = CombatState.Started,
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} resumed the combat at {eventDetails.Timestamp:R}"],
        };
    }

    public async Task<Combat> Apply(CombatPausedEvent @event, Combat Combat, IEvent<CombatPausedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var latestTimingInstance = Combat.Timing.LastOrDefault();
        if (latestTimingInstance == null)
        {
            throw new InvalidDataException("Combat is not in the correct state. The combat was paused with no entires in the timings list.");
        }

        var timing = Combat.Timing.SetItem(
            Combat.Timing.Count - 1,
            latestTimingInstance with
            {
                EndTime = eventDetails.Timestamp
            }
        );

        return Combat with
        {
            Timing = timing,
            State = CombatState.Paused,
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} paused the combat at {eventDetails.Timestamp:R}"],
        };
    }

    public async Task<Combat> Apply(CombatFinishedEvent @event, Combat Combat, IEvent<CombatFinishedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);

        var latestTimingInstance = Combat.Timing.LastOrDefault();
        if (latestTimingInstance == null)
        {
            throw new InvalidDataException("Combat is not in the correct state. The combat was paused with no entires in the timings list.");
        }

        var timing = Combat.Timing.SetItem(
          Combat.Timing.Count - 1,
          latestTimingInstance with
          {
              EndTime = eventDetails.Timestamp
          });

        return Combat with
        {
            Timing = timing,
            State = CombatState.Finished,
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} finished the combat at {eventDetails.Timestamp:R}"],
        };
    }

    // COMBAT TURN MANAGEMENT //
    public async Task<Combat> Apply(TurnFinishedEvent @event, Combat Combat, IEvent<TurnFinishedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);

        return Combat with
        {
            CombatLogs = Combat.CombatLogs.Add($"{user?.UserName} finished their turn at {eventDetails.Timestamp:R}"),
        };
    }

    public async Task<Combat> Apply(StagedCharacterRemovedEvent @event, Combat Combat, IEvent<StagedCharacterRemovedEvent> eventDetails, IQuerySession session)
    {
        var character = Combat.StagedList.SingleOrDefault(x => x.Id == @event.CharacterId);
        if (character == null)
        {
            throw new OperationCanceledException("There is no staged character with the given id.");
        }
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);

        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} unstaged {character.Name} at {eventDetails.Timestamp:R}"],
            StagedList = Combat.StagedList
                .Remove(character)
        };
    }

    public async Task<Combat> Apply(StagedCharacterEditedEvent @event, Combat Combat, IEvent<StagedCharacterEditedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} edited staged character {@event.Character.Name} at {eventDetails.Timestamp:R}"],
            StagedList = Combat.StagedList!
                .ReplaceOrInsert(x => x.Id == @event.Character.Id, @event.Character)
        };
    }

    public async Task<Combat> Apply(StagedCharacterAddedEvent @event, Combat Combat, IEvent<StagedCharacterAddedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} staged {@event.Character.Name} at {eventDetails.Timestamp:R}"],
            StagedList = (Combat.StagedList ?? ImmutableList<CombatCharacter>.Empty)
                .Add(@event.Character)
        };
    }

    // Players joining & leaving. //
    public async Task<Combat> Apply(PlayerLeftEvent @event, Combat Combat, IEvent<PlayerLeftEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} left the combat at {eventDetails.Timestamp:R}"],
            CurrentPlayers = Combat.CurrentPlayers.RemoveAll(
                x => x.UserId == @event.UserId
            ),
        };
    }

    public async Task<Combat> Apply(PlayerJoinedEvent @event, Combat Combat, IEvent<PlayerJoinedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} joined the combat at {eventDetails.Timestamp:R}"],
            CurrentPlayers = Combat.CurrentPlayers.Add(
                new PlayerDto { UserId = @event.UserId }
            ),
        };
    }



}

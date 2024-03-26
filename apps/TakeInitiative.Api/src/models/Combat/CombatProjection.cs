using System.Collections.Immutable;
using JasperFx.Core;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Linq.SoftDeletes;
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
            InitiativeIndex = 0,
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
    public async Task<Combat> Apply(TurnEndedEvent @event, Combat Combat, IEvent<TurnEndedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var dungeonMasterEndedTurn = user?.Id == Combat.DungeonMaster;
        var isDungeonMastersTurn = Combat.InitiativeList[Combat.InitiativeIndex].PlayerId == user?.Id;
        var consoleMessage = dungeonMasterEndedTurn && !isDungeonMastersTurn
            ? $"{user?.UserName} had their turn ended by the DM at {eventDetails.Timestamp:R}"
            : $"{user?.UserName} ended their turn at {eventDetails.Timestamp:R}";

        var nextRoundNumber = Combat.RoundNumber;
        var nextInitiativeIndex = Combat.InitiativeIndex;
        if (nextInitiativeIndex + 1 == Combat.InitiativeList.Count)
        {
            nextInitiativeIndex = 0;
            nextRoundNumber++;
        }
        else
        {
            nextInitiativeIndex++;
        }

        return Combat with
        {
            InitiativeIndex = nextInitiativeIndex,
            RoundNumber = nextRoundNumber,
            CombatLogs = Combat.CombatLogs.Add(consoleMessage),
        };
    }

    // Staged Character management //
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

    public async Task<Combat> Apply(StagedCharacterEvent @event, Combat Combat, IEvent<StagedCharacterEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} staged {@event.Character.Name} at {eventDetails.Timestamp:R}"],
            StagedList = (Combat.StagedList ?? ImmutableList<CombatCharacter>.Empty)
                .Add(@event.Character)
        };
    }

    public async Task<Combat> Apply(StagedPlannedCharacterEvent @event, Combat Combat, IEvent<StagedPlannedCharacterEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);

        List<PlannedCombatStage> plannedStages = new(); // The new list of planned stages after the stage request is over.
        List<CombatCharacter> CharactersToStage = new(); // The list of characters to put into the staged list, from the plannedStages list.
        foreach (var plannedStage in Combat.PlannedStages)
        {
            
            if (!@event.PlannedCharactersToStage.ContainsKey(plannedStage.Id))
            {
                plannedStages.Add(plannedStage);
                continue;
            }

            var characterDTOsToStage = @event.PlannedCharactersToStage[plannedStage.Id];
            IEnumerable<PlannedCombatCharacter> npcsToKeepPlanned = Array.Empty<PlannedCombatCharacter>();
            IEnumerable<PlannedCombatCharacter> npcsToStage = Array.Empty<PlannedCombatCharacter>(); 
            if (characterDTOsToStage.Length == 0)
            {
                npcsToKeepPlanned = plannedStage.Npcs;
            }
            else
            {
                foreach (var dto in characterDTOsToStage)
                {
                    var plannedCharacter = plannedStage.Npcs.First(x => x.Id == dto.CharacterId);
                    if (plannedCharacter.Quantity == dto.Quantity) {
                        npcsToStage = npcsToStage.Append(plannedCharacter);
                    } else {
                        npcsToStage = npcsToStage.Append(plannedCharacter with {
                            Quantity = dto.Quantity
                        });
                        npcsToKeepPlanned = npcsToKeepPlanned.Append(plannedCharacter with {
                            Quantity = plannedCharacter.Quantity - dto.Quantity
                        });
                    }
                }
            }

            if (npcsToKeepPlanned.Count() > 0) {
                plannedStages.Add(
                    plannedStage with {
                        Npcs = npcsToKeepPlanned.ToList()
                    }
                );
            }

            // Map the Npcs to stage to combat characters.
            CharactersToStage.AddRange(
                npcsToStage.Select(npc => {
                    // Check if there are any characters in the current initiative list with the same name as the npc to stage.
                    var isMultipleQuantityCharacter = Combat.InitiativeList.Where(x => x.Name == npc.Name).Count() > 1 || npc.Quantity > 1;
                    if (!isMultipleQuantityCharacter) {
                        return [ new CombatCharacter() {
                            Id = Guid.NewGuid(),
                            PlannedCharacterId = npc.Id,
                            Name = npc.Name,
                            Initiative = npc.Initiative,
                            InitiativeValue = [],
                            PlayerId = @event.UserId,
                            ArmorClass = npc.ArmorClass,
                            Health = npc.Health,
                            Hidden = true,
                            CopyNumber = null
                        }];
                    }

                    var nextQuantityNumber = Combat.InitiativeList.Where(x => x.Name == npc.Name)
                        .Select(x => x.CopyNumber)
                        .Max() + 1 ?? 1;
                    var combatCharactersToOutput = new List<CombatCharacter>();
                    for(int i = 0; i < npc.Quantity; i++) {
                        combatCharactersToOutput.Add(
                            new CombatCharacter() {
                                Id = Guid.NewGuid(),
                                PlannedCharacterId = npc.Id,
                                Name = npc.Name,
                                Initiative = npc.Initiative,
                                InitiativeValue = [],
                                PlayerId = @event.UserId,
                                ArmorClass = npc.ArmorClass,
                                Health = npc.Health,
                                Hidden = true,
                                CopyNumber = nextQuantityNumber++
                            }
                        );
                    }

                    return combatCharactersToOutput;
                }).SelectMany(x => x)
            );
        }

        return Combat with
        {
            CombatLogs = Combat.CombatLogs.Add($"{user?.UserName} staged {CharactersToStage.Count} characters."),
            StagedList = Combat.StagedList.AddRange(CharactersToStage),
            PlannedStages = plannedStages.ToImmutableList()
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

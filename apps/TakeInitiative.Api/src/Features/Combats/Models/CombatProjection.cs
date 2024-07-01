using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using JasperFx.Core;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class CombatProjection : SingleStreamProjection<Combat>
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
            var stagedCharacter = Combat.StagedList.FirstOrDefault(x => x.Id == charInitiative.id, null);
            if (stagedCharacter == null)
            {
                return null;
            }

            return stagedCharacter with
            {
                InitiativeValue = charInitiative.rolls
            };
        })
        .Where(x => x != null)
        .Cast<CombatCharacter>()
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
            State = CombatState.Started,
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} resumed the combat at {eventDetails.Timestamp:R}"],
        };
    }

    public async Task<Combat> Apply(CombatPausedEvent @event, Combat Combat, IEvent<CombatPausedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);

        return Combat with
        {
            State = CombatState.Paused,
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} paused the combat at {eventDetails.Timestamp:R}"],
        };
    }

    public async Task<Combat> Apply(CombatFinishedEvent @event, Combat Combat, IEvent<CombatFinishedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            State = CombatState.Finished,
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} finished the combat at {eventDetails.Timestamp:R}"],
            FinishedTimestamp = DateTimeOffset.UtcNow,
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

        var (nextInitiativeIndex, nextRoundNumber) = Combat.GetNextTurnInfo();

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
            return Combat;
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
                .ReplaceOrInsertIfExists(x => x.Id == @event.Character.Id, @event.Character)
        };
    }

    public async Task<Combat> Apply(StagedCharacterEvent @event, Combat Combat, IEvent<StagedCharacterEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} staged {@event.Character.Name} at {eventDetails.Timestamp:R}"],
            StagedList = (Combat.StagedList ?? [])
                .Add(@event.Character)
        };
    }

    public async Task<Combat> Apply(StagedPlayerCharacterEvent @event, Combat Combat, IEvent<StagedPlayerCharacterEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} staged {@event.Characters.Length} character(s) with the names {string.Join(", ", @event.Characters.Select(x => x.Name))} at {eventDetails.Timestamp:R}"],
            StagedList = (Combat.StagedList ?? [])
                .AddRange(
                    @event.Characters.Map(x => CombatCharacter.NewCombatCharacter(
                        playerId: @event.UserId,
                        name: x.Name,
                        initiative: x.Initiative,
                        armourClass: x.ArmourClass,
                        health: x.Health,
                        hidden: Combat.DungeonMaster == user!.Id,
                        characterOriginDetails: CharacterOriginDetails.PlayerCharacter(@event.UserId),
                        copyNumber: null,
                        conditions: []
                    ))
                )
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
                foreach (var npc in plannedStage.Npcs)
                {
                    var dto = @event.PlannedCharactersToStage[plannedStage.Id].FirstOrDefault(x => x.CharacterId == npc.Id);
                    if (dto == null)
                    {
                        npcsToKeepPlanned = npcsToKeepPlanned.Append(npc);
                        continue;
                    }

                    if (npc.Quantity == dto.Quantity)
                    {
                        npcsToStage = npcsToStage.Append(npc);
                    }
                    else
                    {
                        npcsToStage = npcsToStage.Append(npc with
                        {
                            Quantity = dto.Quantity
                        });
                        npcsToKeepPlanned = npcsToKeepPlanned.Append(npc with
                        {
                            Quantity = npc.Quantity - dto.Quantity
                        });
                    }
                }
            }

            if (npcsToKeepPlanned.Count() > 0)
            {
                plannedStages.Add(
                    plannedStage with
                    {
                        Npcs = npcsToKeepPlanned.ToList()
                    }
                );
            }

            // Map the Npcs to stage to combat characters.
            CharactersToStage.AddRange(
                npcsToStage.Select(npc =>
                {
                    // Check if there are any characters in the current initiative list with the same name as the npc to stage.
                    var isMultipleQuantityCharacter = Combat.InitiativeList.Where(x => x.Name == npc.Name).Count() > 1 || npc.Quantity > 1;
                    if (!isMultipleQuantityCharacter)
                    {
                        return [ new CombatCharacter() {
                            Id = Guid.NewGuid(),
                            CharacterOriginDetails = CharacterOriginDetails.PlannedCharacter(npc.Id),
                            Name = npc.Name,
                            Initiative = npc.Initiative,
                            InitiativeValue = [],
                            PlayerId = @event.UserId,
                            ArmourClass = npc.ArmourClass,
                            Health = npc.Health,
                            Hidden = true,
                            CopyNumber = null
                        }];
                    }

                    var nextQuantityNumber = Combat.InitiativeList.Where(x => x.Name == npc.Name)
                        .Select(x => x.CopyNumber)
                        .Max() + 1 ?? 1;

                    var combatCharactersToOutput = new List<CombatCharacter>();
                    for (int i = 0; i < npc.Quantity; i++)
                    {
                        combatCharactersToOutput.Add(
                            new CombatCharacter()
                            {
                                Id = Guid.NewGuid(),
                                CharacterOriginDetails = CharacterOriginDetails.PlannedCharacter(npc.Id),
                                Name = $"{npc.Name} ({nextQuantityNumber})",
                                Initiative = npc.Initiative,
                                InitiativeValue = [],
                                PlayerId = @event.UserId,
                                ArmourClass = npc.ArmourClass,
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

    public async Task<Combat> Apply(StagedCharactersRolledIntoInitiativeEvent @event, Combat Combat, IEvent<StagedCharactersRolledIntoInitiativeEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);

        // Determine new Initiative list.
        var newInitiativeList = @event.InitiativeRolls.Select((charInitiative, index) =>
        {
            // If there is an existing character, return it with the newly computed rolls.
            var exitingCharacter = Combat.InitiativeList.Find(x => x.Id == charInitiative.id).AsMaybe();
            if (exitingCharacter.HasValue)
            {
                return exitingCharacter.Value with
                {
                    InitiativeValue = charInitiative.rolls
                };
            }

            // Otherwise, we expect there to be a staged character.
            var stagedChar = Combat.StagedList.FirstOrDefault(x => x.Id == charInitiative.id, null).AsMaybe();
            if (stagedChar.HasNoValue) // If there is not one (due to poor data), then return null, it will be filtered out later.
            {
                return null;
            }
            return stagedChar.Value with
            {
                InitiativeValue = charInitiative.rolls
            };
        })
        .Where(x => x != null)
        .Cast<CombatCharacter>()
        .OrderByDescending(x => x.InitiativeValue, new InitiativeComparer())
        .ToImmutableList();

        // Determine new Staged List
        var stagedCharactersToRemove = @event.InitiativeRolls.Select(x => x.id).ToArray();
        var newStagedList = Combat.StagedList.Where(x => !x.Id.In(stagedCharactersToRemove)).ToImmutableList();

        // Determine new Initiative Index
        Maybe<Guid> characterWithCurrentTurn = Combat.InitiativeList.Count > 0 ? Combat.InitiativeList[Combat.InitiativeIndex].Id : Maybe.None;
        var newInitiativeIndex = characterWithCurrentTurn.HasValue ? newInitiativeList.FindIndex(x => x.Id == characterWithCurrentTurn) : 0; // Maintains the initiative index, so that it still points to the character whos turn it was before.
        return Combat with
        {
            CombatLogs = Combat.CombatLogs.Add($"{user?.UserName} rolled {@event.InitiativeRolls.Count} characters into initiative at {eventDetails.Timestamp:R}"),
            StagedList = newStagedList,
            InitiativeList = newInitiativeList,
            InitiativeIndex = newInitiativeIndex
        };
    }

    // Initiative Character management //
    public async Task<Combat> Apply(InitiativeCharacterEditedEvent @event, Combat Combat, IEvent<InitiativeCharacterEditedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var (character, index) = Combat.InitiativeList.Select((value, index) => (value, index)).FirstOrDefault(x => x.value.Id == @event.Character.Id, (null, -1));
        if (character == null)
        {
            return Combat; // Ignore this character if it cannot be found.
        }

        return Combat with
        {
            CombatLogs = Combat.CombatLogs.Add($"{user?.UserName} edited the character {@event.Character.Name} at {eventDetails.Timestamp:R}"),
            InitiativeList = Combat.InitiativeList.SetItem(index, character with
            {
                Name = @event.Character.Name,
                InitiativeValue = @event.Character.InitiativeValue,
                Health = @event.Character.Health,
                Hidden = @event.Character.Hidden,
                ArmourClass = @event.Character.ArmourClass,
                Conditions = @event.Character.Conditions,
            }),
        };
    }

    public async Task<Combat> Apply(InitiativeCharacterRemovedEvent @event, Combat Combat, IEvent<InitiativeCharacterRemovedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var (character, index) = Combat.InitiativeList.Select((value, index) => (value, index)).FirstOrDefault(x => x.value.Id == @event.CharacterId, (null, -1));
        if (character == null)
        {
            return Combat;
        }

        var initiativeIndex = Combat.InitiativeIndex;
        var roundNumber = Combat.RoundNumber;
        if (index == Combat.InitiativeIndex)
        {
            (initiativeIndex, roundNumber) = Combat.GetNextTurnInfo();
        }

        return Combat with
        {
            CombatLogs = Combat.CombatLogs.Add($"{user?.UserName} edited the character {character.Name} at {eventDetails.Timestamp:R}"),
            InitiativeList = Combat.InitiativeList.RemoveAt(index),
            InitiativeIndex = initiativeIndex,
            RoundNumber = roundNumber
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

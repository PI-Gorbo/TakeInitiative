using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
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
            StagedList = Combat.StagedList.AddRange(CharactersToStage),
            PlannedStages = plannedStages.ToImmutableList()
        };
    }
}
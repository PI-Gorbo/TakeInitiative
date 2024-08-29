using System.Text.Json;
using CSharpFunctionalExtensions;
using FakeItEasy;
using FluentAssertions;
using Marten;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Features.Combats;
using VerifyTests;

namespace TakeInitiative.Api.Tests.Integration;


public class FullCombatTest : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private readonly AuthenticatedWebAppWithDatabaseFixture fixture;
    private readonly CombatVerifier verifier;

    public FullCombatTest(AuthenticatedWebAppWithDatabaseFixture fixture)
    {
        this.fixture = fixture;
        verifier = new CombatVerifier(nameof(FullCombatTest));
    }

    [Fact]
    public async Task Test()
    {
        fixture.LoginAsUser(Users.DM);

        // Create a planned combat.
        var createPlannedCombat = await fixture.PostPlannedCombat(new()
        {
            CampaignId = fixture.SeedData!.CampaignId,
            CombatName = "My planned combat"
        });
        createPlannedCombat.Should().Succeed();
        var plannedCombat = createPlannedCombat.Value;

        // Add a character to the first stage.
        var addIntroEnemyToStageOne = await fixture.PostPlannedCombatNpc(new()
        {
            CombatId = plannedCombat.Id,
            StageId = plannedCombat.Stages.First().Id,
            Name = "Intro Enemy",
            Initiative = new UnevaluatedCharacterInitiative("1d20 + 1"),
            Health = new UnevaluatedCharacterHealth.Fixed(0, 0),
            ArmourClass = 12,
            Quantity = 1,
        });
        addIntroEnemyToStageOne.Should().Succeed();
        plannedCombat = addIntroEnemyToStageOne.Value;

        // Add a second stage
        var addSecondStageResult = await fixture.PostPlannedCombatStage(new()
        {
            Name = "Boss Fight",
            CombatId = plannedCombat.Id
        });
        addSecondStageResult.Should().Succeed();
        plannedCombat = addSecondStageResult.Value;

        // Add 1 character with a quantity of 10.
        var addCharactersToSecondStageResult = await fixture.PostPlannedCombatNpc(new()
        {
            CombatId = plannedCombat.Id,
            Name = "10 goblins in a trench coat",
            Quantity = 10,
            ArmourClass = 12,
            StageId = plannedCombat.Stages.ElementAt(1).Id,
            Initiative = new UnevaluatedCharacterInitiative("1d20 + 2"),
            Health = new UnevaluatedCharacterHealth.None(), // No health
        });
        addCharactersToSecondStageResult.Should().Succeed();
        plannedCombat = addCharactersToSecondStageResult.Value;

        // Edit a planned character by setting the health.
        plannedCombat.Stages.Count.Should().Be(2);
        plannedCombat.Stages[1].Npcs.Count.Should().Be(1);
        var npc = plannedCombat.Stages[1].Npcs.First();
        var setPlannedCharacterHealth = await fixture.PutPlannedCombatNpc(new()
        {
            CombatId = plannedCombat.Id,
            StageId = npc.StageId,
            NpcId = npc.Id,
            Name = npc.Name,
            Quantity = npc.Quantity,
            ArmourClass = npc.ArmourClass,
            Health = new UnevaluatedCharacterHealth.Roll("20d20 + 10"),
            Initiative = npc.Initiative,
        });
        A.CallTo(() => fixture.DiceRoller.EvaluateRoll("20d20 + 10"))
           .Returns(300);

        // Open the combat
        var openedCombat = await fixture.PostOpenCombat(new()
        {
            PlannedCombatId = plannedCombat.Id,
        });
        openedCombat.Should().Succeed();
        var combat = openedCombat.Value.Combat;
        await verifier
            .RegisterKnownGuid(combat.Id, "CombatId")
            .RegisterKnownGuid(combat.DungeonMaster, "DmId")
            .Verify(combat, "OpenedCombat");

        // Player adds their character to the combat.
        var addStagedPlayerCharacterResult = await fixture
            .LoginAsUser(Users.Player)
            .PostAddStagedCharacter(new()
            {
                CombatId = openedCombat.Value.Combat.Id,
                Character = new(
                    ArmourClass: 20,
                    Health: new UnevaluatedCharacterHealth.Fixed(10, 20),
                    Hidden: false,
                    Name: "My Super Duper Character",
                    Initiative: new("1d20 + 3")
                )
            });
        addStagedPlayerCharacterResult.Should().Succeed();
        combat = addStagedPlayerCharacterResult.Value.Combat;
        var firstCharacterId = combat.StagedList.Find(x => x.Name == "My Super Duper Character")!.Id;
        await verifier
            .RegisterKnownGuid(firstCharacterId, "PlayerFirstCharacterId")
            .RegisterKnownGuid(combat.CurrentPlayers[0].UserId, "PlayerId")
            .Verify(combat, "PlayerStagedCharacter");

        // DM stages their own characters
        var addPlannedCharactersResult = await fixture
            .LoginAsUser(Users.DM)
            .PostStagePlannedCharacters(new PostStagePlannedCharactersRequest()
            {
                CombatId = combat.Id,
                PlannedCharactersToStage = new()
                {
                    [combat.PlannedStages.First().Id] = [
                        new () {
                            CharacterId = combat.PlannedStages.First().Npcs.First().Id,
                            Quantity = 1,
                        },
                    ]
                }
            });
        addPlannedCharactersResult.Should().Succeed();
        combat = addPlannedCharactersResult.Value.Combat;
        await verifier.Verify(combat, "DmStagedPlannedCharacter");

        // Setup a mock for the initial initiative rolls.
        combat.StagedList.Count.Should().Be(2);
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<IEnumerable<StagedCharacter>>._))
            .Returns(new Dictionary<Guid, CharacterInitiative>()
            {
                [combat.StagedList[0].Id] = new CharacterInitiative([20]),
                [combat.StagedList[1].Id] = new CharacterInitiative([15]),
            });

        // Roll Initiative
        var startCombatResult = await fixture.PostRollCombatInitiative(new PostRollCombatInitiativeRequest()
        {
            CombatId = combat.Id,
        });
        startCombatResult.Should().Succeed();
        combat = startCombatResult.Value.Combat;
        await verifier.Verify(combat, "CombatStarted");

        // The DM will update the character to be not be hidden.
        InitiativeCharacter notVisibleDmCharacter = combat.InitiativeList[0];
        Guid conditionId = Guid.NewGuid();
        var setDmCharacterToVisible = await fixture.PutUpdateInitiativeCharacter(new()
        {
            Character = new()
            {
                Id = notVisibleDmCharacter.Id,
                Name = notVisibleDmCharacter.Name,
                ArmourClass = notVisibleDmCharacter.ArmourClass,
                Hidden = false,
                Initiative = notVisibleDmCharacter.Initiative,
                Health = notVisibleDmCharacter.Health,
                Conditions = [
                    new(conditionId, "Paralyzed", "AHHHH")
                ]
            },
            CombatId = combat.Id,
        });
        setDmCharacterToVisible.Should().Succeed();
        combat = setDmCharacterToVisible.Value.Combat;
        await verifier
            .RegisterKnownGuid(conditionId, "ConditionId")
            .Verify(combat, "DmSetsCharacterToVisible");

        // It is the player's turn.
        // Imagine the player rolls and does some damage to the enemy.
        // The DM would subtract some damage from their character's health.
        InitiativeCharacter dmCharacter = combat.InitiativeList[0];
        var damageDmCharacter = await fixture.PutUpdateInitiativeCharacter(new()
        {
            Character = new()
            {
                Id = dmCharacter.Id,
                Name = dmCharacter.Name,
                ArmourClass = dmCharacter.ArmourClass,
                Hidden = dmCharacter.Hidden,
                Initiative = dmCharacter.Initiative,
                Health = dmCharacter.Health switch
                {
                    CharacterHealth.None none => none,
                    CharacterHealth.Fixed fixedValue => fixedValue with
                    {
                        CurrentHealth = fixedValue.CurrentHealth - 5
                    },
                    _ => throw new NotImplementedException()
                },
                Conditions = [
                    new(conditionId, "Paralyzed", "AHHHH")
                ]
            },
            CombatId = combat.Id,
        });
        damageDmCharacter.Should().Succeed();
        combat = damageDmCharacter.Value.Combat;
        await verifier.Verify(combat, "DamageDmCharacter");

        // Player ends their turn.
        var endTurnResult = await fixture
            .LoginAsUser(Users.Player)
            .PostEndTurn(new PostEndTurnRequest()
            {
                CombatId = combat.Id,
            });
        endTurnResult.Should().Succeed();
        combat = endTurnResult.Value.Combat;
        await verifier.Verify(combat, "PlayerEndsTurn");

        // The DM adds a character to the staged list.
        var addStagedCharacterResult = await fixture
            .LoginAsUser(Users.DM)
            .PostAddStagedCharacter(new()
            {
                CombatId = combat.Id,
                Character = new(
                    Health: new UnevaluatedCharacterHealth.Fixed(10, 20),
                    Initiative: new UnevaluatedCharacterInitiative("10"),
                    Name: "Another Enemy!",
                    ArmourClass: null,
                    Hidden: false
                )
            });
        addStagedCharacterResult.Should().Succeed();
        combat = addStagedCharacterResult.Value.Combat;
        await verifier.Verify(combat, "DmStagesAnotherCharacter");

        // The DM then rolls the character into initiative.
        var characterId = combat.StagedList.First().Id;
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<List<StagedCharacter>>._, A<List<InitiativeCharacter>>._))
            .Returns(new Dictionary<Guid, CharacterInitiative>()
            {
                [combat.InitiativeList[0].Id] = new CharacterInitiative([20]),
                [combat.InitiativeList[1].Id] = new CharacterInitiative([15]),
                [characterId] = new CharacterInitiative([10])
            });
        var addStagedCharacterToInitiativeResult = await fixture
            .PostRollStagedCharactersIntoInitiative(new()
            {
                CombatId = combat.Id,
                CharacterIds = [
                    characterId
                ]
            });
        addStagedCharacterToInitiativeResult.Should().Succeed();
        combat = addStagedCharacterToInitiativeResult.Value.Combat;
        await verifier.Verify(combat, "DmRolledStagedCharacterIntoInitiative");

        // Remove the paralyzed condition from the DM's character.
        InitiativeCharacter dmCharacterWithCondition = combat.InitiativeList[0];
        var removedCondition = await fixture.PutUpdateInitiativeCharacter(new()
        {
            Character = new()
            {
                Id = dmCharacterWithCondition.Id,
                Name = dmCharacterWithCondition.Name,
                ArmourClass = dmCharacterWithCondition.ArmourClass,
                Hidden = dmCharacterWithCondition.Hidden,
                Initiative = dmCharacterWithCondition.Initiative,
                Health = dmCharacterWithCondition.Health switch
                {
                    CharacterHealth.None none => none,
                    CharacterHealth.Fixed fixedValue => fixedValue with
                    {
                        CurrentHealth = fixedValue.CurrentHealth - 5
                    },
                    _ => throw new NotImplementedException()
                },
                Conditions = []
            },
            CombatId = combat.Id,
        });
        removedCondition.Should().Succeed();
        combat = removedCondition.Value.Combat;
        await verifier.Verify(combat, "RemovedParalyzedCondition");

        // Remove the player's character from the combat.
        var deleteCharacterResponse = await fixture.DeleteInitiativeCharacter(
            new()
            {
                CombatId = combat.Id,
                CharacterId = firstCharacterId,
            }
        );
        deleteCharacterResponse.Should().Succeed();
        combat = deleteCharacterResponse.Value.Combat;
        await verifier.Verify(combat, "CharacterRemoved");

        // Finish the combat
        var finishCombatResult = await fixture
            .LoginAsUser(Users.DM)
            .PostFinishCombat(new()
            {
                CombatId = combat.Id,
            });
        finishCombatResult.Should().Succeed();
        combat = finishCombatResult.Value.Combat;
        await verifier.Verify(combat, "CombatFinished");
    }
}
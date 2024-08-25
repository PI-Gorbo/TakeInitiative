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
        verifier = new CombatVerifier();
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
            Name = "Intro Enemy",
            Initiative = new CharacterInitiative()
            {
                Value = "1d20 + 1",
                Strategy = InitiativeStrategy.Roll
            },
            ArmourClass = 12,
            StageId = plannedCombat.Stages.First().Id,
            Quantity = 1,
            Health = new()
            {
                HasHealth = false,
                MaxHealth = 0,
                CurrentHealth = 0,
            },
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
            Initiative = new CharacterInitiative()
            {
                Value = "1d20 + 2",
                Strategy = InitiativeStrategy.Roll // Roll,
            },
            Health = new()
            {
                CurrentHealth = 0,
                MaxHealth = 0,
                HasHealth = false,
            },
        });
        addCharactersToSecondStageResult.Should().Succeed();
        plannedCombat = addCharactersToSecondStageResult.Value;

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
            .Verify(combat, "FullCombatTest.00.OpenedCombat");

        // Player adds their character to the combat.
        var firstCharacterId = Guid.NewGuid();
        var addStagedPlayerCharacterResult = await fixture
            .LoginAsUser(Users.Player)
            .PutUpsertStagedCharacter(new()
            {
                CombatId = openedCombat.Value.Combat.Id,
                Character = new StagedCombatCharacterDto(
                    Id: firstCharacterId,
                    ArmourClass: 20,
                    Health: new CharacterHealth()
                    {
                        CurrentHealth = 10,
                        MaxHealth = 20,
                        HasHealth = true,
                    },
                    Hidden: false,
                    Name: "My Super Duper Character",
                    Initiative: new()
                    {
                        Value = "1d20 + 3",
                        Strategy = InitiativeStrategy.Roll // Roll,
                    }
                )
            });
        addStagedPlayerCharacterResult.Should().Succeed();
        combat = addStagedPlayerCharacterResult.Value.Combat;
        await verifier
            .RegisterKnownGuid(firstCharacterId, "PlayerFirstCharacterId")
            .RegisterKnownGuid(combat.CurrentPlayers[0].UserId, "PlayerId")
            .Verify(combat, "FullCombatTest.01.PlayerStagedCharacter");

        // DM stages their own characters
        var addPlannedCharactersResult = await fixture
            .LoginAsUser(Users.DM)
            .PostStagePlannedCharacters(new PutStagePlannedCharactersRequest()
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
        await verifier.Verify(combat, "FullCombatTest.02.DmStagedPlannedCharacter");

        // Setup a mock for the initial initiative rolls.
        combat.StagedList.Count.Should().Be(2);
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<IEnumerable<StagedCharacter>>._))
            .Returns(new List<CharacterInitiativeRoll>()
            {
                new(combat.StagedList[0].Id, [20]),
                new(combat.StagedList[1].Id, [15]),
            });

        // Start the combat.
        var startCombatResult = await fixture.PostStartCombat(new PostRollCombatInitiativeRequest()
        {
            CombatId = combat.Id,
        });
        startCombatResult.Should().Succeed();
        combat = startCombatResult.Value.Combat;
        await verifier.Verify(combat, "FullCombatTest.03.CombatStarted");

        // The DM will update the character to be not be hidden.
        InitiativeCharacter notVisibleDmCharacter = combat.InitiativeList[0];
        var setDmCharacterToVisible = await fixture.PutUpdateInitiativeCharacter(new()
        {
            Character = new()
            {
                Id = notVisibleDmCharacter.Id,
                Name = notVisibleDmCharacter.Name,
                ArmourClass = notVisibleDmCharacter.ArmourClass,
                Hidden = false,
                InitiativeValue = notVisibleDmCharacter.InitiativeValue,
                Health = notVisibleDmCharacter.Health,
            },
            CombatId = combat.Id,
        });
        setDmCharacterToVisible.Should().Succeed();
        combat = startCombatResult.Value.Combat;
        await verifier.Verify(combat, "FullCombatTest.04.DmSetsCharacterToVisible");

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
                InitiativeValue = dmCharacter.InitiativeValue,
                Health = dmCharacter.Health! with
                {
                    CurrentHealth = dmCharacter.Health.CurrentHealth - 5,
                },
            },
            CombatId = combat.Id,
        });
        damageDmCharacter.Should().Succeed();
        combat = damageDmCharacter.Value.Combat;
        await verifier.Verify(combat, "FullCombatTest.05.DamageDmCharacter");

        // Player ends their turn.
        var endTurnResult = await fixture
            .LoginAsUser(Users.Player)
            .PostEndTurn(new PostEndTurnRequest()
            {
                CombatId = combat.Id,
            });
        endTurnResult.Should().Succeed();
        combat = endTurnResult.Value.Combat;
        await verifier.Verify(combat, "FullCombatTest.06.PlayerEndsTurn");

        // The DM adds a character to the staged list.
        var addStagedCharacterResult = await fixture
            .LoginAsUser(Users.DM)
            .PutUpsertStagedCharacter(new()
            {
                CombatId = combat.Id,
                Character = new(
                    Id: Guid.NewGuid(),
                    Health: new()
                    {
                        CurrentHealth = 10,
                        MaxHealth = 20,
                        HasHealth = true,
                    },
                    Initiative: new()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "10"
                    },
                    Name: "Another Enemy!",
                    ArmourClass: null,
                    Hidden: false
                )
            });
        addStagedCharacterResult.Should().Succeed();
        combat = addStagedCharacterResult.Value.Combat;
        await verifier.Verify(combat, "FullCombatTest.07.DmStagesAnotherCharacter");

        // The DM then rolls the character into initiative.
        var characterId = combat.StagedList.First().Id;
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<List<StagedCharacter>>._, A<List<InitiativeCharacter>>._))
            .Returns(new List<CharacterInitiativeRoll>()
            {
                new(combat.InitiativeList[0].Id, [20]),
                new(combat.InitiativeList[1].Id, [15]),
                new(characterId, [10])
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
        await verifier.Verify(combat, "FullCombatTest.08.DmRolledStagedCharacterIntoInitiative");

        // Finish the combat
        var finishCombatResult = await fixture
            .LoginAsUser(Users.DM)
            .PostFinishCombat(new()
            {
                CombatId = combat.Id,
            });
        finishCombatResult.Should().Succeed();
        combat = finishCombatResult.Value.Combat;
        await verifier.Verify(combat, "FullCombatTest.09.CombatFinished");
    }
}
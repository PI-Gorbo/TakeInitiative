using System.Text.Json;
using CSharpFunctionalExtensions;
using FluentAssertions;
using NSubstitute;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Features.Combats;
using VerifyTests;

namespace TakeInitiative.Api.Tests.Integration;

public class ComprehensiveCombatTests(AuthenticatedWebAppWithDatabaseFixture fixture) : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    [Fact]
    public async Task FullCombatTest()
    {
        fixture.LoginAsUser(Users.DM);

        // Create a planned combat.
        var createPlannedCombat = await fixture.PostPlannedCombat(new()
        {
            CampaignId = fixture.SeedData.CampaignId,
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
        await VerifyWithFileName(combat, "0.OpenedCombat");

        // Player adds their character to the combat.
        var addStagedPlayerCharacterResult = await fixture
            .LoginAsUser(Users.Player)
            .PutUpsertStagedCharacter(new()
            {
                CombatId = openedCombat.Value.Combat.Id,
                Character = new StagedCombatCharacterDto()
                {
                    Id = Guid.NewGuid(), // ??????
                    ArmourClass = 20,
                    Health = new CharacterHealth()
                    {
                        CurrentHealth = 10,
                        MaxHealth = 20,
                        HasHealth = true,
                    },
                    Hidden = false,
                    Name = "My Super Duper Character",
                    Initiative = new()
                    {
                        Value = "1d20 + 3",
                        Strategy = InitiativeStrategy.Roll // Roll,
                    }
                },
            });
        addStagedPlayerCharacterResult.Should().Succeed();
        combat = addStagedPlayerCharacterResult.Value.Combat;
        await VerifyWithFileName(combat, "1.PlayerStagedCharacter");

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
                            CharacterId = combat.PlannedStages.First().Npcs.First().Id, Quantity = 1
                        },
                    ]
                }
            });
        addPlannedCharactersResult.Should().Succeed();
        combat = addPlannedCharactersResult.Value.Combat;
        await VerifyWithFileName(combat, "2.DmStagedPlannedCharacter");

        // Setup a mock for the initial initiative rolls.
        fixture.InitiativeRoller.ComputeRolls(default, default)
            .ReturnsForAnyArgs(new List<CharacterInitiativeRoll>()
            {
                new(combat.StagedList[0].Id, [20]),
                new(combat.StagedList[1].Id, [15]),
            });

        // Start the combat.
        var startCombatResult = await fixture.PostStartCombat(new PostStartCombatRequest()
        {
            CombatId = combat.Id,
        });
        startCombatResult.Should().Succeed();
        combat = startCombatResult.Value.Combat;
        await VerifyWithFileName(combat, "3.CombatStarted");


    }

    private Task VerifyWithFileName(object target, string fileName)
    {
        var verifySettings = new VerifySettings();
        verifySettings.DontIgnoreEmptyCollections();
        verifySettings.UseFileName(fileName);
        var options = new JsonSerializerOptions();
        options.TypeInfoResolverChain.Add(new PolymorphicTypeResolver());
        var serializedValue = JsonSerializer.Serialize(target, options: options);
        serializedValue = serializedValue.Replace("$type", "TYPE");
        return VerifyJson(serializedValue, verifySettings);
    }
}
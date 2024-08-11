using System.Formats.Tar;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace TakeInitiative.Api.Tests.Integration;

public class ComprehensiveCombatTests(AuthenticatedWebAppWithDatabaseFixture fixture) : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    [Fact]
    public async Task FullCombatTest()
    {
        fixture.LoginAsUser(Users.DM);

        // Create a planned combat.
        var createPlannedCombat = await fixture.Scenario(client => client.TakeInitiativeApiFeaturesCombatsPostPlannedCombatAsync(new()
        {
            CampaignId = fixture.SeedData.CampaignId,
            CombatName = "My planned combat"
        }));
        createPlannedCombat.Should().Succeed();
        var plannedCombat = createPlannedCombat.Value;

        // Add a character to the first stage.
        var addIntroEnemyToStageOne = await fixture.Scenario((client) => client.TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpcAsync(new()
        {
            CombatId = plannedCombat.Id,
            Name = "Intro Enemy",
            Initiative = new Client.CharacterInitiative()
            {
                Value = "1d20 + 1",
                Strategy = Client.CharacterInitiativeStrategy._1 // Roll,
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
        }));
        addIntroEnemyToStageOne.Should().Succeed();
        plannedCombat = addIntroEnemyToStageOne.Value;

        // Add a second stage
        var addSecondStageResult = await fixture.Scenario((client) => client.TakeInitiativeApiFeaturesCombatsPostPlannedCombatStageAsync(new()
        {
            Name = "Boss Fight",
            CombatId = plannedCombat.Id
        }));
        addSecondStageResult.Should().Succeed();
        plannedCombat = addSecondStageResult.Value;

        // Add 1 character with a quantity of 10.
        var addCharactersToSecondStageResult = await fixture.Scenario((client) => client.TakeInitiativeApiFeaturesCombatsPostPlannedCombatNpcAsync(new()
        {
            CombatId = plannedCombat.Id,
            Name = "10 goblins in a trench coat",
            Quantity = 10,
            ArmourClass = 12,
            StageId = plannedCombat.Stages.ElementAt(1).Id,
            Initiative = new Client.CharacterInitiative()
            {
                Value = "1d20 + 4",
                Strategy = Client.CharacterInitiativeStrategy._1 // Roll,
            },
            Health = new()
            {
                CurrentHealth = 0,
                MaxHealth = 0,
                HasHealth = false,
            },
        }));
        addCharactersToSecondStageResult.Should().Succeed();
        plannedCombat = addCharactersToSecondStageResult.Value;

        // Open the combat
        var openedCombat = await fixture.Scenario((client) => client.TakeInitiativeApiFeaturesCombatsPostOpenCombatAsync(new()
        {
            PlannedCombatId = plannedCombat.Id,
        }));
        openedCombat.Should().Succeed();

        var combat = openedCombat.Value.Combat;
        await VerifyWithFileName(combat, "0.OpenedCombat");

        // Player adds their character to the combat.
        var addStagedCharacterResult = await fixture.LoginAsUser(Users.Player)
            .Scenario(client => client.TakeInitiativeApiFeaturesCombatsPutUpsertStagedCharacterAsync(new Client.PutUpsertStagedCharacterRequest()
            {
                CombatId = openedCombat.Value.Combat.Id,
                Character = new Client.StagedCombatCharacterDto()
                {
                    Id = Guid.NewGuid(), // ??????
                    ArmourClass = 20,
                    Health = new Client.CharacterHealth()
                    {
                        CurrentHealth = 10,
                        MaxHealth = 20,
                        HasHealth = true,
                    },
                    Hidden = false,
                    Name = "My Super Duper Character",
                    Initiative = new()
                    {
                        Value = "1d20 + 1",
                        Strategy = Client.CharacterInitiativeStrategy._1 // Roll,
                    }
                },
            }));

        addStagedCharacterResult.Should().Succeed();
        await VerifyWithFileName(combat, "1.PlayerStagedCharacter");

    }

    private Task VerifyWithFileName(object target, string fileName)
    {
        var verifySettings = new VerifySettings();
        verifySettings.DontIgnoreEmptyCollections();
        verifySettings.UseFileName(fileName);
        return Verify(target, verifySettings);
    }
}
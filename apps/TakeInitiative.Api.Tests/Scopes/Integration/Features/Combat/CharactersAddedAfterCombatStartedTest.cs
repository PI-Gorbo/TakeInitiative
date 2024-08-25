using System.Text.Json;
using CSharpFunctionalExtensions;
using FakeItEasy;
using FluentAssertions;
using Marten;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Features.Combats;
using VerifyTests;

namespace TakeInitiative.Api.Tests.Integration;


public class CharactersAddedAfterCombatStartedTest : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private readonly AuthenticatedWebAppWithDatabaseFixture fixture;
    private readonly CombatVerifier verifier;

    public CharactersAddedAfterCombatStartedTest(AuthenticatedWebAppWithDatabaseFixture fixture)
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
            CampaignId = fixture.SeedData.CampaignId,
            CombatName = "My planned combat"
        });
        createPlannedCombat.Should().Succeed();
        var plannedCombat = createPlannedCombat.Value;

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
            .Verify(combat, "CharactersAddedAfterCombatStartedTest.00.OpenedCombat");

        // Setup a mock for the initial initiative rolls.
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<IEnumerable<StagedCharacter>>._))
            .Returns(new List<CharacterInitiativeRoll>() { });

        // Start the combat.
        var startCombatResult = await fixture.PostStartCombat(new PostRollCombatInitiativeRequest()
        {
            CombatId = combat.Id,
        });
        startCombatResult.Should().Succeed();
        combat = startCombatResult.Value.Combat;
        await verifier.Verify(combat, "CharactersAddedAfterCombatStartedTest.01.CombatStarted");

        // The DM adds a character to the staged list.
        var addStagedCharacterResult = await fixture
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
        await verifier.Verify(combat, "CharactersAddedAfterCombatStartedTest.02.DmStagesCharacterAfterCombatStarted");

        // The DM then rolls the character into initiative.
        var characterId = combat.StagedList.First().Id;
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<List<StagedCharacter>>._, A<List<InitiativeCharacter>>._))
            .Returns(new List<CharacterInitiativeRoll>()
            {
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
        await verifier.Verify(combat, "CharactersAddedAfterCombatStartedTest.03.DmRolledStagedCharacterIntoInitiative");

        // Finish the combat
        var finishCombatResult = await fixture
            .LoginAsUser(Users.DM)
            .PostFinishCombat(new()
            {
                CombatId = combat.Id,
            });
        finishCombatResult.Should().Succeed();
        combat = finishCombatResult.Value.Combat;
        await verifier.Verify(combat, "CharactersAddedAfterCombatStartedTest.04.CombatFinished");
    }
}
using System.Text.Json;
using CSharpFunctionalExtensions;
using FakeItEasy;
using FluentAssertions;
using Marten;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Features.Combats;
using VerifyTests;

namespace TakeInitiative.Api.Tests.Integration;


public class EmptyCombatTest : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private readonly AuthenticatedWebAppWithDatabaseFixture fixture;
    private readonly CombatVerifier verifier;

    public EmptyCombatTest(AuthenticatedWebAppWithDatabaseFixture fixture)
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
            .Verify(combat, "EmptyCombatTest.00.OpenedCombat");

        // Start the combat.
        // Prep the mock.
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<IEnumerable<StagedCharacter>>._))
            .Returns(Result.Success(new List<CharacterInitiativeRoll>()));

        var startCombatResult = await fixture.PostStartCombat(new PostRollCombatInitiativeRequest()
        {
            CombatId = combat.Id,
        });
        startCombatResult.Should().Succeed();
        combat = startCombatResult.Value.Combat;
        await verifier.Verify(combat, "EmptyCombatTest.01.CombatStarted");

        // Dm finishes the combat.
        var finishCombatResult = await fixture.PostFinishCombat(new()
        {
            CombatId = combat.Id,
        });
        finishCombatResult.Should().Succeed();
        combat = finishCombatResult.Value.Combat;
        await verifier.Verify(combat, "EmptyCombatTest.02.CombatFinished");
    }
}
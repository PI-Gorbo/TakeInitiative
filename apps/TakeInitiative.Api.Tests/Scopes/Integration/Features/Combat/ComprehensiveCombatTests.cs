using System.Formats.Tar;
using CSharpFunctionalExtensions;
using FluentAssertions;

namespace TakeInitiative.Api.Tests.Integration;
public class ComprehensiveCombatTests(AuthenticatedWebAppWithDatabaseFixture fixture) : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    [Fact]
    public async Task FullCombatTest()
    {
        // Create a planned combat.
        var createPlannedCombat = await Result.Try(() => fixture.Client.TakeInitiativeApiFeaturesCombatsPostPlannedCombatAsync(new()
        {
            CampaignId = fixture.SeedData.CampaignId,
            // CombatName = "My planned combat"
        }));
        createPlannedCombat.Should().Succeed();

        // Add some characters to the first stage.
        var setCharactersResult = await Result.Try(() => fixture.Client.TakeInitiativeApiFeaturesCombatsPutPlannedCombatNpcAsync(new()
        {
            CombatId = createPlannedCombat.Value.Id,
            NpcId = Guid.NewGuid(),
            Name = "My Character",
            Initiative = new Client.CharacterInitiative()
            {
                Roll = "1d20 + 1",
                Strategy =
            }

        }));

        // Open the combat
        var openedCombat = await Result.Try(() => fixture.Client.TakeInitiativeApiFeaturesCombatsPostOpenCombatAsync(new()
        {
            PlannedCombatId = createPlannedCombat.Value.Id,
        }));
        openedCombat.Should().Succeed();

        var combat = openedCombat.Value.Combat;
        await VerifyWithFileName(combat, "0.OpenedCombat");



    }

    private Task VerifyWithFileName(object target, string fileName)
    {
        var verifySettings = new VerifySettings();
        verifySettings.UseFileName(fileName);
        return Verify(target, verifySettings);
    }
}
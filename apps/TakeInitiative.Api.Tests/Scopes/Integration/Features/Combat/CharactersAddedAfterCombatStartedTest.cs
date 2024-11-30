using FakeItEasy;
using FluentAssertions;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Tests.Integration;


public class CharactersAddedAfterCombatStartedTest : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private readonly AuthenticatedWebAppWithDatabaseFixture fixture;
    private readonly CombatVerifier verifier;

    public CharactersAddedAfterCombatStartedTest(AuthenticatedWebAppWithDatabaseFixture fixture)
    {
        this.fixture = fixture;
        verifier = new CombatVerifier(nameof(CharactersAddedAfterCombatStartedTest));
    }

    [Theory]
    [InlineData(0)]
    public async Task Test(int stageCount)
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
            .Verify(combat, "OpenedCombat", stageCount);

        // Setup a mock for the initial initiative rolls.
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<IEnumerable<StagedCharacter>>._))
            .Returns(new Dictionary<Guid, CharacterInitiative>());

        // Start the combat.
        var startCombatResult = await fixture.PostRollCombatInitiative(new PostRollCombatInitiativeRequest()
        {
            CombatId = combat.Id,
        });
        startCombatResult.Should().Succeed();
        combat = startCombatResult.Value.Combat;
        await verifier.Verify(combat, "CombatStarted", stageCount);

        // The DM adds a character to the staged list.
        var addStagedCharacterResult = await fixture
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
        await verifier.Verify(combat, "DmStagesCharacterAfterCombatStarted", stageCount);

        // The DM then rolls the character into initiative.
        var characterId = combat.StagedList.First().Id;
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<List<StagedCharacter>>._, A<List<InitiativeCharacter>>._))
            .Returns(new Dictionary<Guid, CharacterInitiative>()
            {
                [characterId] = new CharacterInitiative([new DiceRoll(10, "10", "10")])
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
        await verifier.Verify(combat, "DmRolledStagedCharacterIntoInitiative", stageCount);

        // Finish the combat
        var finishCombatResult = await fixture
            .LoginAsUser(Users.DM)
            .PostFinishCombat(new()
            {
                CombatId = combat.Id,
            });
        finishCombatResult.Should().Succeed();
        combat = finishCombatResult.Value.Combat;
        await verifier.Verify(combat, "CombatFinished", stageCount);
    }
}
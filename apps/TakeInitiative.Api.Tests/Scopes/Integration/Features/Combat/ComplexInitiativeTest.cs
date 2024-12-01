using CSharpFunctionalExtensions;

using FakeItEasy;

using FluentAssertions;

using TakeInitiative.Api.Features;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Tests.Integration;

public class ComplexInitiativeTest : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private readonly AuthenticatedWebAppWithDatabaseFixture fixture;
    private readonly CombatVerifier verifier;

    public ComplexInitiativeTest(AuthenticatedWebAppWithDatabaseFixture fixture)
    {
        this.fixture = fixture;
        this.verifier = new CombatVerifier(nameof(ComplexInitiativeTest));

        InitiativeRoller roller = new InitiativeRoller(this.fixture.DiceRoller);
        A.CallTo(() => fixture.InitiativeRoller.ComputeRolls(A<IEnumerable<StagedCharacter>>._))
            .ReturnsLazily((IEnumerable<StagedCharacter> arg) =>
            {
                return roller.ComputeRolls(arg);
            });

        A.CallTo(
                () => fixture.InitiativeRoller.ComputeRolls(A<List<StagedCharacter>>._, A<List<InitiativeCharacter>>._))
            .ReturnsLazily((List<StagedCharacter> newCharacters, List<InitiativeCharacter> existingInitiativeList) =>
            {
                return roller.ComputeRolls(newCharacters, existingInitiativeList);
            });
    }

    [Fact]
    public async Task ComplexInitiativeRollsTest()
    {
        fixture.LoginAsUser(Users.DM);

        // Create a planned combat.
        var createPlannedCombat = await fixture.PostPlannedCombat(new()
        {
            CampaignId = fixture.SeedData!.CampaignId, CombatName = "My planned combat"
        });
        createPlannedCombat.Should().Succeed();
        var plannedCombat = createPlannedCombat.Value;

        // Open the combat
        var openedCombat = await fixture.PostOpenCombat(new() { PlannedCombatId = plannedCombat.Id, });
        openedCombat.Should().Succeed();

        var combat = openedCombat.Value.Combat;

        await verifier
            .RegisterKnownGuid(combat.Id, "CombatId")
            .RegisterKnownGuid(combat.DungeonMaster, "DmId")
            .Verify(combat, "OpenedCombat");

        // Add two staged characters with colliding initiative rolls.
        var stagedCharacter1 = await fixture.PostStageCharacter(new()
        {
            CombatId = combat.Id,
            Character = new(
                Name: "Player - 1",
                Health: new UnevaluatedCharacterHealth.None(),
                Initiative: new UnevaluatedCharacterInitiative("5"),
                ArmourClass: null,
                Hidden: false
            ),
        });
        stagedCharacter1.Should().Succeed();

        var stagedCharacter2 = await fixture.PostStageCharacter(new()
        {
            CombatId = combat.Id,
            Character = new(
                Name: "Player - 2",
                Health: new UnevaluatedCharacterHealth.None(),
                Initiative: new UnevaluatedCharacterInitiative("3"),
                ArmourClass: null,
                Hidden: false
            ),
        });
        stagedCharacter2.Should().Succeed();
        
        // Mocking
        A.CallTo(() => fixture.DiceRoller.EvaluateRoll("5")).Returns(Result.Success<DiceRoll>(new(5, "5", "5")));
        A.CallTo(() => fixture.DiceRoller.EvaluateRoll("3")).Returns(Result.Success<DiceRoll>(new(3, "3", "3")));
        
        DiceRoll[] responses =
        [
            new DiceRoll(19, "1d20", "1d20(19)"),       // Player 2     - rolls a 19 after rolling a 3.
            new DiceRoll(20, "1d20", "1d20(**20**)"),   // Enemy 1      - rolls a 20 after rolling a 3
            new DiceRoll(12, "1d20", "1d20(12)"),       // Player 1     - rolls a 12 after conflicting with enemy 2
            new DiceRoll(17, "1d20", "1d20(17)"),       // Enemy 2      - rolls a 17 after conflicting with Player 1
        ];
        var callCount = 0;
        A.CallTo(() => fixture.DiceRoller.RollD20())
            .ReturnsLazily(() =>
            {
                if (callCount < responses.Length)
                {
                    return responses[callCount++];
                }

                throw new InvalidOperationException("Should not be called more than expected");
            });


        // Start the combat.
        var startCombatResult =
            await fixture.PostRollCombatInitiative(new PostRollCombatInitiativeRequest() { CombatId = combat.Id, });
        startCombatResult.Should().Succeed();
        combat = startCombatResult.Value.Combat;
        await verifier.Verify(combat, "CombatStarted");

        // Add a character that will conflict with the 3s
        var addStagedCharacterResult = await fixture
            .PostStageCharacter(new()
            {
                CombatId = combat.Id,
                Character = new(
                    Health: new UnevaluatedCharacterHealth.None(),
                    Initiative: new UnevaluatedCharacterInitiative("3"), 
                    Name: "Another Enemy - 1!",
                    ArmourClass: null,
                    Hidden: false
                )
            });
        addStagedCharacterResult.Should().Succeed();
        combat = addStagedCharacterResult.Value.Combat;
        await verifier.Verify(combat, "DmStagesCharacterAfterCombatStarted");
        
        // The DM then rolls the character into initiative.
        var characterIds = combat.StagedList.Select(x => x.Id);
        var addStagedCharacterToInitiativeResult = await fixture
            .PostRollStagedCharactersIntoInitiative(new() { CombatId = combat.Id, CharacterIds = [..characterIds] });
        addStagedCharacterToInitiativeResult.Should().Succeed();
        combat = addStagedCharacterToInitiativeResult.Value.Combat;
        await verifier.Verify(combat, "DmRolledStagedCharacterIntoInitiative");
        
        // The DM adds a character that will conflict with the 5s
        addStagedCharacterResult = await fixture
            .PostStageCharacter(new()
            {
                CombatId = combat.Id,
                Character = new(
                    Health: new UnevaluatedCharacterHealth.None(),
                    Initiative: new UnevaluatedCharacterInitiative("5"), 
                    Name: "Another Enemy - 2!",
                    ArmourClass: null,
                    Hidden: false
                )
            });
        addStagedCharacterResult.Should().Succeed();
        combat = addStagedCharacterResult.Value.Combat;
        await verifier.Verify(combat, "DmStagesCharacterAfterCombatStarted-2");
        
        // The DM then rolls the character into initiative.
        characterIds = combat.StagedList.Select(x => x.Id);
        addStagedCharacterToInitiativeResult = await fixture
            .PostRollStagedCharactersIntoInitiative(new() { CombatId = combat.Id, CharacterIds = [..characterIds] });
        addStagedCharacterToInitiativeResult.Should().Succeed();
        combat = addStagedCharacterToInitiativeResult.Value.Combat;
        await verifier.Verify(combat, "DmRolledStagedCharacterIntoInitiative-2");
    }
}
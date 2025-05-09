using FluentAssertions;

using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Tests.Unit;

public class InitiativeOrderingTests
{
    [Fact]
    public void RollsCorrectly()
    {
        int[][] inputList =
        [
            [25],
            [24, 4, 11],
            [24, 4, 16],
            [22],
            [21],
            [20, 8],
            [20, 13],
            [20, 5],
            [20, 2],
            [20, 17],
            [19, 15],
            [19, 12, 1],
            [19, 12, 4],
            [19, 4],
            [19, 11],
            [19, 7],
            [19, 8],
            [19, 1],
        ];


        var orderedValue = inputList.Order(new InitiativeComparer()).ToArray();
        orderedValue.Should().BeEquivalentTo(new int[][]
        {
            [25], [24, 4, 16], [24, 4, 11], [22], [21], [20, 17], [20, 13], [20, 8], [20, 5], [20, 2], [19, 15],
            [19, 12, 4], [19, 12, 1], [19, 11], [19, 8], [19, 7], [19, 4], [19, 1],
        });
    }
}
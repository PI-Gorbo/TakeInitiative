using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The integration fixtures substitute <see cref="IDiceRoller"/>, so these are the
/// tests that prove the real roller is what the app registers and validates with.
/// </summary>
public class DiceRollerAdapterTests
{
	private readonly DiceRoller roller = new(new Random(1234));

	[Fact]
	public void The_app_registers_the_dice_language_roller()
	{
		var services = new ServiceCollection()
			.AddDiceRollers(new ConfigurationBuilder().Build())
			.BuildServiceProvider();

		services.GetRequiredService<IDiceRoller>().Should().BeOfType<DiceRoller>();
	}

	[Theory]
	[InlineData("1d20 + 2")]
	[InlineData("adv(1d20)")]
	[InlineData("kh1(2d20) + 3")]
	public void Rolls_valid_expressions(string expression)
	{
		var result = roller.EvaluateRoll(expression);

		result.IsSuccess.Should().BeTrue();
		result.Value.Roll.Should().Be(expression);
		result.Value.Evaluation.Should().EndWith($"= {result.Value.Total}");
	}

	[Fact]
	public void Failure_is_a_message_for_the_dm_not_an_exception()
	{
		var result = roller.EvaluateRoll("kh3(2d10)");

		result.Error.Should().Be("can't keep the highest 3 of only 2 dice");
	}

	[Fact]
	public void RollD20_is_in_range()
	{
		Enumerable.Range(0, 200).Select(_ => roller.RollD20().Total).Should().OnlyContain(x => x >= 1 && x <= 20);
	}
}

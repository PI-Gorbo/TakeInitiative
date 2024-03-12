using CSharpFunctionalExtensions;
using Python.Runtime;

namespace TakeInitiative.Utilities;

public static class DiceRoller
{

	public static Result<int> EvaluateRoll(string roll)
	{
		return Result.Try(() =>
		{
			using (Py.GIL())
			{
				dynamic d20 = Py.Import("d20");
				var result = d20.roll(roll);
				return (int)result.total;
			};
		}, ex => $"Failed to evaluate dice roll, please check your syntax. {ex}");
	}
}
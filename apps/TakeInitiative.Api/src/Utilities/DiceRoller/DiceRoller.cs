using CSharpFunctionalExtensions;
using Python.Runtime;
using TakeInitiative.Api.Features;

namespace TakeInitiative.Utilities;

public class DiceRoller : IDiceRoller
{
    public Result<int> EvaluateRoll(string roll)
    {
        return Result.Try(() =>
        {
            using (Py.GIL())
            {
                dynamic d20 = Py.Import("d20");
                var result = d20.roll(roll);
                return (int)result.total;
            };
        }, ex => $"Failed to evaluate dice roll, please check your syntax. {ex.Message}");
    }

    public int RollD20() => EvaluateRoll("1d20").Value;
}


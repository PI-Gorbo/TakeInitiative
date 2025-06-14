using CSharpFunctionalExtensions;
using Python.Runtime;

namespace TakeInitiative.Utilities;

public class DiceRoller : IDiceRoller
{
    public Result<DiceRoll> EvaluateRoll(string roll)
    {
        return Result.Try(() =>
        {
            using (Py.GIL())
            {
                dynamic d20 = Py.Import("d20");
                var result = d20.roll(roll);
                return new DiceRoll((int)result.total, roll, (string)result.result);
                // return (int)result.total;
            };
        }, ex => $"Failed to evaluate dice roll, please check your syntax. {ex.Message}");
    }

    public DiceRoll RollD20() => EvaluateRoll("1d20").Value;
}


using CSharpFunctionalExtensions;
using Python.Runtime;

namespace TakeInitiative.Utilities;

/// <summary>
/// The previous roller, backed by Python's d20 via pythonnet. Unregistered since
/// roadmap step 10; kept only as a fallback until step 11 deletes it.
/// </summary>
public class PythonDiceRoller : IDiceRoller
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
            };
        }, ex => $"Failed to evaluate dice roll, please check your syntax. {ex.Message}");
    }

    public DiceRoll RollD20() => EvaluateRoll("1d20").Value;

    public Result Check(string roll) => EvaluateRoll(roll);
}

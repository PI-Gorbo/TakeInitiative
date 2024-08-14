using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace TakeInitiative.Utilities;

public interface IDiceRoller
{
    public Result<int> EvaluateRoll(string roll);

    public int RollD20();
}

using System.Text.Json;
using TakeInitiative.Api.Features.Combats;

namespace TakeInitiative.Api.Tests.Integration;



public class CombatVerifier
{
    private VerifySettings settings;
    private int Count = 0;
    private string fileName;

    public CombatVerifier(string fileName)
    {
        settings = new VerifySettings();
        settings.DontIgnoreEmptyCollections();
        this.fileName = fileName;
    }

    public CombatVerifier RegisterKnownGuid(Guid guid, string name)
    {
        settings.AddNamedGuid(guid, name);
        return this;
    }

    public Task Verify(Combat combat, string? description, int? onlyVerifyCount = null)
    {
        // The counter has to advance on every call, verified or not. It used to be
        // incremented inside the guard below, so a run pinned to stage N > 0 never
        // reached stage N and silently asserted nothing at all.
        var stage = this.Count++;

        if (onlyVerifyCount.HasValue && onlyVerifyCount != stage)
        {
            return Task.CompletedTask;
        }

        settings.UseFileName($"{fileName}.{stage:D2}.{description}");
        var serializedValue = JsonSerializer.Serialize(combat);
        serializedValue = serializedValue.Replace("\"!\"", "\"TYPE\"");
        return VerifyJson(serializedValue, settings);
    }
}

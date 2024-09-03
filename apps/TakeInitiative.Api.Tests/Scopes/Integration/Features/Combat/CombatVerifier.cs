using System.Text.Json;
using DiffEngine;
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

    public Task Verify(Combat combat, string? description)
    {
        settings.UseFileName($"{fileName}.{this.Count++:D2}.{description}");
        var serializedValue = JsonSerializer.Serialize(combat);
        serializedValue = serializedValue.Replace("\"!\"", "\"TYPE\"");
        return VerifyJson(serializedValue, settings);
    }
}

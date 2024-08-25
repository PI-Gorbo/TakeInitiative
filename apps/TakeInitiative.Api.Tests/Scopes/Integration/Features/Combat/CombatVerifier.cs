using System.Text.Json;
using DiffEngine;
using TakeInitiative.Api.Features.Combats;

namespace TakeInitiative.Api.Tests.Integration;

public class CombatVerifier
{
    private VerifySettings settings;

    public CombatVerifier()
    {
        DiffTools.UseOrder(DiffTool.WinMerge, DiffTool.VisualStudioCode);
        settings = new VerifySettings();
        settings.DontIgnoreEmptyCollections();
    }

    public CombatVerifier RegisterKnownGuid(Guid guid, string name)
    {
        settings.AddNamedGuid(guid, name);
        return this;
    }

    public Task Verify(Combat combat, string fileName)
    {
        settings.UseFileName(fileName);
        var serializedValue = JsonSerializer.Serialize(combat);
        serializedValue = serializedValue.Replace("!", "TYPE");
        return VerifyJson(serializedValue, settings);
    }
}

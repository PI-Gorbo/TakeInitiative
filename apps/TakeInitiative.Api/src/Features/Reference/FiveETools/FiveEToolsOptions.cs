namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// Where the 5eTools index is (21b.1): <c>Reference:FiveETools:IndexPath</c>, or
/// <c>Reference__FiveETools__IndexPath</c> in the environment. A relative path is resolved from the
/// content root. With no value the 5eTools provider is off, which is the default everywhere but
/// Development: the index is built per deployment from data the deployer supplies, and is never
/// committed or baked into an image.
/// </summary>
public record FiveEToolsOptions
{
    public const string Section = "Reference:FiveETools";

    /// <summary>The index file <c>scripts/5etools/build-5etools-index.mjs</c> wrote, or null for off.</summary>
    public string? IndexPath { get; init; }
}

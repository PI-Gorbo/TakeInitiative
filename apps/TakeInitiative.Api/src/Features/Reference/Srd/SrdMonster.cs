namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// One SRD 5.2 monster as <see cref="SrdCatalog"/> holds it: the stat block as read from
/// <c>monsters.json</c> (our schema, 1:1), its name folded once for matching, and the summary a
/// search row and + Wiki use, built once.
/// </summary>
public record SrdMonster(StatBlock StatBlock, string FoldedName, ReferenceSummary Summary)
{
    public string Id => StatBlock.Id;
    public string Name => StatBlock.Name;
}

/// <summary>
/// <c>Reference/Srd52/source.json</c>: where the data came from and whom to credit. Written by
/// <c>scripts/srd/build-srd52.mjs</c> next to <c>monsters.json</c>.
/// </summary>
/// <param name="Document">"SRD 5.2": the provider's label comes from here, so a move to 5.2.1 is a data change.</param>
/// <param name="Sha">The open5e-api commit the data was built from.</param>
/// <param name="Attribution">SRD 5.2's own attribution statement, shown under every stat-block card.</param>
/// <param name="Changes">What the script changed, which CC-BY-4.0 asks us to say.</param>
public record SrdSource(
    string Document,
    string License,
    string LicenseUrl,
    string SourceUrl,
    string SourceRepo,
    string Sha,
    int Count,
    string Attribution,
    string Changes);

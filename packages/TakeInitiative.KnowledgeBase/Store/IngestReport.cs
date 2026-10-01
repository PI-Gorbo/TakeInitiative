namespace TakeInitiative.KnowledgeBase.Store;

/// <summary>
/// What a run would change, counted before anything is written. The CLI prints this whether or not
/// it goes on to write, which is what makes <c>--dry-run</c> a real rehearsal rather than a
/// different code path.
/// </summary>
/// <param name="Provider">The corpus the run is about.</param>
/// <param name="Parsed">How many rows the source folder yielded.</param>
/// <param name="New">Rows the database has never seen.</param>
/// <param name="Updated">
/// Rows whose content hash moved, plus rows a previous prune marked <c>stale</c> that this source
/// has back — those need writing even with an unchanged hash, to clear the flag.
/// </param>
/// <param name="Unchanged">Rows already stored with this hash, and not stale.</param>
/// <param name="MissingIds">
/// Ids in the database for this provider that this source does not have. A plain run leaves every
/// one of them exactly as it is.
/// </param>
/// <param name="StoredBefore">How many rows the provider had before the run.</param>
public sealed record IngestReport(
    string Provider,
    int Parsed,
    int New,
    int Updated,
    int Unchanged,
    IReadOnlyList<string> MissingIds,
    int StoredBefore)
{
    /// <summary>How many stored rows this source does not have.</summary>
    public int Missing => MissingIds.Count;

    /// <summary>Whether a write would do anything at all.</summary>
    public bool HasWrites => New > 0 || Updated > 0;
}

/// <summary>
/// What a prune did, or refused to do. Separate from <see cref="IngestReport" /> because a plain run
/// never prunes: additive-by-default is the whole safety story, and a report that mixed the two
/// would make a refusal easy to miss.
/// </summary>
/// <param name="Provider">The corpus the prune is about.</param>
/// <param name="StoredBefore">How many rows the provider had before the prune.</param>
/// <param name="Considered">
/// How many rows the source did not have, and so how many the prune was asked to remove.
/// </param>
/// <param name="Deleted">How many were actually deleted.</param>
/// <param name="MarkedStale">
/// How many were kept and flagged instead, because an entry links to them (step 27). A link is
/// something a user made; the ingest does not get to erase it.
/// </param>
/// <param name="Refused">
/// True when the prune removed nothing because it would have taken more than
/// <see cref="KnowledgeBaseStore.PruneThreshold" /> of the provider's rows and <c>--force</c> was
/// not given. Mass deletion is the signature of the wrong folder.
/// </param>
public sealed record PruneReport(
    string Provider,
    int StoredBefore,
    int Considered,
    int Deleted,
    int MarkedStale,
    bool Refused)
{
    /// <summary>The share of the provider's rows the prune was asked to remove, 0 to 1.</summary>
    public double Share => StoredBefore == 0 ? 0 : (double)Considered / StoredBefore;

    /// <summary>A prune that was asked to remove nothing.</summary>
    public static PruneReport Nothing(string provider, int storedBefore) =>
        new(provider, storedBefore, 0, 0, 0, Refused: false);
}

/// <summary>An ingest's report, and its prune's where one ran.</summary>
public sealed record IngestOutcome(IngestReport Report, PruneReport? Prune)
{
    /// <summary>True when a prune was asked for and the threshold stopped it.</summary>
    public bool PruneRefused => Prune is { Refused: true };
}

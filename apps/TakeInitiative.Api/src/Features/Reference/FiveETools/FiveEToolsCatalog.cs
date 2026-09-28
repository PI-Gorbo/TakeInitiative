using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// The 5eTools index (21b.2), read once from the file at <see cref="FiveEToolsOptions.IndexPath"/>
/// when the singleton is built; <c>Program</c> builds it right after startup. It holds each row's
/// summary and folded name. It never touches the network (invariant 10).
/// <list type="bullet">
/// <item>No path, or no file there: not an error. One information line, and no rows, so the
/// provider is off and the app is as it was after step 20.</item>
/// <item>A file that is there but unreadable, or has an unknown <c>format</c>: throws, so a bad
/// deploy fails startup loudly.</item>
/// </list>
/// </summary>
public class FiveEToolsCatalog
{
    public const string ProviderKey = "5etools";
    public const string DefaultBaseUrl = "https://5e.tools/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IReadOnlyDictionary<string, ReferenceSummary> byId;

    /// <summary>Loads <paramref name="path"/> (absolute, or null for off).</summary>
    public FiveEToolsCatalog(string? path, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        IReadOnlyList<FiveEToolsRow> rows = [];
        if (string.IsNullOrWhiteSpace(path))
        {
            logger.LogInformation("5eTools index not configured; the 5eTools provider is off");
        }
        else if (!File.Exists(path))
        {
            logger.LogInformation("5eTools index not found at {Path}; the 5eTools provider is off", path);
        }
        else
        {
            rows = Read(path);
            logger.LogInformation(
                "5eTools index loaded from {Path}: {Count} items ({Monsters} monsters, {Spells} spells, {Items} items)",
                path, rows.Count,
                rows.Count(r => r.Category == ReferenceCategory.Monster),
                rows.Count(r => r.Category == ReferenceCategory.Spell),
                rows.Count(r => r.Category == ReferenceCategory.Item));
        }

        Rows = rows;
        var summaries = rows.Select(Summarise).ToList();
        byId = summaries.ToDictionary(s => s.Id, StringComparer.Ordinal);
        Candidates = summaries.Select(s => new ReferenceMatcher.Candidate<ReferenceSummary>(s, s.Name, ReferenceMatcher.Fold(s.Name))).ToList();
        var first = rows.FirstOrDefault(r => Uri.TryCreate(r.Url, UriKind.Absolute, out _));
        BaseUrl = first is null ? DefaultBaseUrl : new Uri(first.Url).GetLeftPart(UriPartial.Authority) + "/";
        Attribution = new ReferenceAttribution("Found in the 5eTools index. Opens 5etools in a new tab.", "", "", BaseUrl);
    }

    /// <summary>Builds it from the configuration, resolving a relative path from the content root.</summary>
    public static FiveEToolsCatalog FromConfiguration(IConfiguration? configuration, string? contentRoot, ILogger? logger)
    {
        var options = configuration?.GetSection(FiveEToolsOptions.Section).Get<FiveEToolsOptions>() ?? new FiveEToolsOptions();
        var path = string.IsNullOrWhiteSpace(options.IndexPath)
            ? null
            : Path.GetFullPath(options.IndexPath, contentRoot ?? Directory.GetCurrentDirectory());
        return new FiveEToolsCatalog(path, logger);
    }

    /// <summary>Every row, in the file's order (category, then name, then source).</summary>
    public IReadOnlyList<FiveEToolsRow> Rows { get; }

    /// <summary>Each row's summary with its folded name, ready for <see cref="ReferenceMatcher.Search{T}"/>.</summary>
    public IReadOnlyList<ReferenceMatcher.Candidate<ReferenceSummary>> Candidates { get; }

    /// <summary>True when the index loaded rows; otherwise the provider answers nothing.</summary>
    public bool IsOn => Rows.Count > 0;

    /// <summary>The 5etools site the rows link to: "https://5e.tools/".</summary>
    public string BaseUrl { get; }

    public ReferenceAttribution Attribution { get; }

    /// <summary>The summary with this id ("monster_beholder_mm"), or null.</summary>
    public ReferenceSummary? Find(string id) => byId.GetValueOrDefault(id);

    private static IReadOnlyList<FiveEToolsRow> Read(string path)
    {
        FiveEToolsIndex index;
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("format", out var format)
                || format.ValueKind != JsonValueKind.Number
                || format.GetInt32() != FiveEToolsIndex.CurrentFormat)
            {
                throw new InvalidOperationException(
                    $"The 5eTools index at {path} has an unknown format; this API reads format {FiveEToolsIndex.CurrentFormat}. Rebuild it with `pnpm 5etools:build`.");
            }
            index = document.RootElement.Deserialize<FiveEToolsIndex>(JsonOptions)
                ?? throw new InvalidOperationException($"The 5eTools index at {path} is empty.");
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or FormatException)
        {
            throw new InvalidOperationException(
                $"The 5eTools index at {path} could not be read: {e.Message} Rebuild it with `pnpm 5etools:build`, or unset {FiveEToolsOptions.Section}:IndexPath.", e);
        }

        var rows = index.Items ?? throw new InvalidOperationException($"The 5eTools index at {path} has no items.");
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Name)
                || string.IsNullOrWhiteSpace(row.Source) || string.IsNullOrWhiteSpace(row.Url))
            {
                throw new InvalidOperationException($"The 5eTools index at {path} has a row without an id, name, source or url.");
            }
        }
        var duplicate = rows.GroupBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"The 5eTools index at {path} has the id {duplicate.Key} twice.");
        }
        return rows;
    }

    /// <summary>
    /// What a search row and + Wiki show of a row: "CR 13 · Large Aberration · MM", the link, the
    /// kind of entry + Wiki makes, the Stats for a monster that has them, and the book ("MM p. 28").
    /// Only index fields.
    /// </summary>
    public static ReferenceSummary Summarise(FiveEToolsRow row) => new(
        Provider: ProviderKey,
        Id: row.Id,
        Name: row.Name,
        Category: row.Category,
        Detail: string.IsNullOrWhiteSpace(row.Label) ? row.Source : $"{row.Label} · {row.Source}",
        Url: row.Url,
        SuggestedKind: row.Category switch
        {
            ReferenceCategory.Monster => EntryKind.Character,
            ReferenceCategory.Item => EntryKind.Item,
            _ => EntryKind.Other,
        },
        Stats: row.Stats is { } stats
            ? Stats.Of(stats.InitiativeBonus is { } bonus ? ReferenceStats.Initiative(bonus) : null, stats.Hp, stats.Ac)
            : null,
        Book: row.Page is { } page ? $"{row.Source} p. {page}" : row.Source);
}

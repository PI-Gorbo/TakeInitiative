using FastEndpoints;
using FluentValidation.Results;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Connections;

public record GetConnectionGraphRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>The entry to centre on. Without it, every entry with a connection.</summary>
    public Guid? Focus { get; init; }
    /// <summary>With a focus: 1 (its neighbours, the default) or 2 (theirs too).</summary>
    public int? Depth { get; init; }
    /// <summary>A comma list of entry kinds (<c>Character,Place</c>, any case). All kinds by default. Never removes the focus.</summary>
    public string? Kinds { get; init; }
}

public record ConnectionGraphResponse
{
    /// <summary>The focus first, then by depth, then heaviest first.</summary>
    public required GraphNodeResponse[] Nodes { get; init; }
    /// <summary>Every connection between two returned nodes.</summary>
    public required GraphEdgeResponse[] Edges { get; init; }
    /// <summary>Whether nodes were dropped to keep to <see cref="GetConnectionGraph.MaxNodes"/>.</summary>
    public required bool Truncated { get; init; }
}

public record GraphNodeResponse
{
    public required EntrySummaryResponse Entry { get; init; }
    /// <summary>How many notes and blocks the viewer can see mention it (<see cref="MentionIndex.CountsFor"/>). Sizes the node.</summary>
    public required int MentionCount { get; init; }
    /// <summary>0 for the focus, 1 for its neighbours, 2 for theirs; null without a focus.</summary>
    public int? Depth { get; init; }
}

public record GraphEdgeResponse
{
    public required Guid A { get; init; }
    public required Guid B { get; init; }
    public required int Weight { get; init; }
    public required int Notes { get; init; }
    public required int Blocks { get; init; }
    /// <summary>Combats both fought in: the web dashes such an edge.</summary>
    public required int Combats { get; init; }
}

/// <summary>
/// The Wiki's graph (19a.5, glossary: Graph): the caller's connections as nodes and weighted
/// edges, around a focus at depth 1 or 2, or the whole campaign, filtered by kind and capped at
/// <see cref="MaxNodes"/> nodes. Read from every source the caller can see
/// (<see cref="ConnectionIndex.ForCampaign"/>), so an entry, note, block or combatant they
/// cannot see adds no node, no edge and no weight.
/// </summary>
public class GetConnectionGraph(IDocumentSession session) : Endpoint<GetConnectionGraphRequest, ConnectionGraphResponse>
{
    public const int MaxNodes = 300;

    public const string DepthErrorKey = "depth";
    public const string KindsErrorKey = "kinds";

    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/connections/graph");
    }

    public override async Task HandleAsync(GetConnectionGraphRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        if (req.Depth is { } d && d is not (1 or 2))
        {
            ThrowError(new ValidationFailure(DepthErrorKey, "Depth is 1 or 2."), StatusCodes.Status400BadRequest);
        }
        var kinds = ParseKinds(req.Kinds);
        if (kinds is null)
        {
            ThrowError(new ValidationFailure(KindsErrorKey, $"Kinds are a comma list of {string.Join(", ", Enum.GetNames<EntryKind>())}."), StatusCodes.Status400BadRequest);
        }

        Entry? focus = req.Focus is { } focusId
            ? await this.RequireVisibleEntry(session, req.CampaignId, focusId, member, ct)
            : null;

        var read = await ConnectionIndex.ForCampaign(session, req.CampaignId, member, ct);
        var graph = ConnectionGraph.Build(read.Pairs.Values, read.Entries, focus?.Id, req.Depth ?? 1, kinds, MaxNodes);
        var counts = MentionIndex.CountsFrom(read.Notes, [.. read.Entries.Values], member);

        await SendAsync(new ConnectionGraphResponse
        {
            Nodes = graph.Nodes
                .Select(n => new GraphNodeResponse
                {
                    Entry = EntrySummaryResponse.From(read.Entries[n.EntryId]),
                    MentionCount = counts.GetValueOrDefault(n.EntryId)?.Count ?? 0,
                    Depth = n.Depth,
                })
                .ToArray(),
            Edges = graph.Edges
                .Select(e => new GraphEdgeResponse
                {
                    A = e.Pair.A,
                    B = e.Pair.B,
                    Weight = e.Weight,
                    Notes = e.Notes,
                    Blocks = e.Blocks,
                    Combats = e.Combats,
                })
                .ToArray(),
            Truncated = graph.Truncated,
        }, cancellation: ct);
    }

    /// <summary>The kinds asked for; every kind when none are; null when one is not a kind.</summary>
    public static IReadOnlySet<EntryKind>? ParseKinds(string? kinds)
    {
        if (string.IsNullOrWhiteSpace(kinds))
        {
            return Enum.GetValues<EntryKind>().ToHashSet();
        }
        var set = new HashSet<EntryKind>();
        foreach (var part in kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<EntryKind>(part, ignoreCase: true, out var kind) || !Enum.IsDefined(kind) || int.TryParse(part, out _))
            {
                return null;
            }
            set.Add(kind);
        }
        return set;
    }
}

/// <summary>One node of a <see cref="ConnectionGraph"/>: an entry and its depth from the focus (null without one).</summary>
public record GraphNode(Guid EntryId, int? Depth);

/// <summary>
/// The graph's shape (19a.5), pure. With a focus: the focus (depth 0), its neighbours that pass
/// the kind filter (1) and, at depth 2, their neighbours that pass it (2), so a depth-2 node is
/// reached only through a node that passes. Without one: every entry of a wanted kind with a
/// connection to another. Edges are every connection between two kept nodes, so a triangle is
/// drawn as one. Over the cap, the heaviest nodes (total weight to the other kept nodes) are
/// kept, and the focus always is.
/// </summary>
public static class ConnectionGraph
{
    public record Result(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<Connection> Edges, bool Truncated);

    public static Result Build(
        IEnumerable<Connection> connections, IReadOnlyDictionary<Guid, Entry> entries,
        Guid? focus, int depth, IReadOnlySet<EntryKind> kinds, int maxNodes)
    {
        var all = connections.ToList();
        bool Wanted(Guid id) => id == focus || (entries.TryGetValue(id, out var e) && kinds.Contains(e.Kind));

        var neighbours = new Dictionary<Guid, List<Guid>>();
        foreach (var c in all.Where(c => Wanted(c.Pair.A) && Wanted(c.Pair.B)))
        {
            Add(neighbours, c.Pair.A, c.Pair.B);
            Add(neighbours, c.Pair.B, c.Pair.A);
        }

        var depths = new Dictionary<Guid, int?>();
        if (focus is { } f)
        {
            depths[f] = 0;
            var frontier = new List<Guid> { f };
            for (var level = 1; level <= depth; level++)
            {
                var next = new List<Guid>();
                foreach (var id in frontier)
                {
                    foreach (var n in neighbours.GetValueOrDefault(id) ?? [])
                    {
                        if (depths.TryAdd(n, level))
                        {
                            next.Add(n);
                        }
                    }
                }
                frontier = next;
            }
        }
        else
        {
            foreach (var id in neighbours.Keys)
            {
                depths[id] = null;
            }
        }

        var kept = depths.Keys.ToHashSet();
        var truncated = false;
        if (kept.Count > maxNodes)
        {
            truncated = true;
            var weights = Weights(all, kept);
            kept = kept
                .OrderBy(id => id == focus ? 0 : 1)
                .ThenBy(id => depths[id] ?? 0)
                .ThenByDescending(id => weights.GetValueOrDefault(id))
                .ThenBy(id => id)
                .Take(maxNodes)
                .ToHashSet();
        }

        var edges = all.Where(c => kept.Contains(c.Pair.A) && kept.Contains(c.Pair.B)).ToList();
        var total = Weights(edges, kept);
        var nodes = kept
            .OrderBy(id => id == focus ? 0 : 1)
            .ThenBy(id => depths[id] ?? 0)
            .ThenByDescending(id => total.GetValueOrDefault(id))
            .ThenBy(id => entries.TryGetValue(id, out var e) ? e.Name : "", StringComparer.OrdinalIgnoreCase)
            .Select(id => new GraphNode(id, depths[id]))
            .ToList();
        return new Result(nodes, edges.OrderByDescending(e => e.Weight).ThenBy(e => e.Pair.A).ThenBy(e => e.Pair.B).ToList(), truncated);
    }

    private static void Add(Dictionary<Guid, List<Guid>> map, Guid from, Guid to)
    {
        if (!map.TryGetValue(from, out var list))
        {
            map[from] = list = [];
        }
        list.Add(to);
    }

    private static Dictionary<Guid, int> Weights(IEnumerable<Connection> connections, IReadOnlySet<Guid> among)
    {
        var weights = new Dictionary<Guid, int>();
        foreach (var c in connections.Where(c => among.Contains(c.Pair.A) && among.Contains(c.Pair.B)))
        {
            weights[c.Pair.A] = weights.GetValueOrDefault(c.Pair.A) + c.Weight;
            weights[c.Pair.B] = weights.GetValueOrDefault(c.Pair.B) + c.Weight;
        }
        return weights;
    }
}

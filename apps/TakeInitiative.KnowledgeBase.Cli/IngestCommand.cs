using System.CommandLine;
using System.Globalization;
using System.Net.Sockets;

using Microsoft.Extensions.Configuration;

using Npgsql;

using TakeInitiative.KnowledgeBase.FiveETools;
using TakeInitiative.KnowledgeBase.Schema;
using TakeInitiative.KnowledgeBase.Store;

namespace TakeInitiative.KnowledgeBase.Cli;

/// <summary>
/// <c>ingest</c>: read a 5eTools data folder, parse it, diff it against Postgres and upsert.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is additive.</b> A plain run inserts and updates and has no way to delete. That is the whole
/// safety story of this step: point the tool at one bestiary file instead of thirty and the worst
/// outcome is rows that are out of date, not an entry's link that broke. <c>--prune</c> is opt-in, it
/// never deletes a row an entry links to, and it refuses outright past 20% without <c>--force</c>.
/// The rules themselves live in <see cref="KnowledgeBaseStore" />, so they cannot be bypassed by a
/// second caller and can be tested without a process.
/// </para>
/// <para>
/// <b>Configuration is <c>appsettings.json</c>, then environment, then flags, flags winning</b>, so a
/// plain <c>ingest</c> works on a dev machine: the connection string defaults to the one the API uses
/// and the provider defaults to <c>5etools</c>. Only the source folder has no default, because there
/// is no sensible guess for where an operator keeps 5eTools' data and guessing wrong is exactly the
/// mistake the prune threshold exists to catch.
/// </para>
/// </remarks>
public static class IngestCommand
{
    /// <summary>Where the 5eTools data folder is. <c>KnowledgeBase__Source__Path</c>, <c>--from</c>.</summary>
    public const string SourcePathKey = "KnowledgeBase:Source:Path";

    /// <summary>
    /// The monster floor the parse refuses to go under.
    /// <c>KnowledgeBase__Source__MinMonsters</c>, <c>--min-monsters</c>.
    /// </summary>
    public const string MinMonstersKey = "KnowledgeBase:Source:MinMonsters";

    /// <summary>Where to write. <c>KnowledgeBase__ConnectionString</c>, <c>--connection</c>.</summary>
    public const string ConnectionStringKey = "KnowledgeBase:ConnectionString";

    /// <summary>Which corpus. <c>KnowledgeBase__Provider</c>, <c>--provider</c>.</summary>
    public const string ProviderKey = "KnowledgeBase:Provider";

    /// <summary>Whether to prune. <c>KnowledgeBase__Prune</c>, <c>--prune</c>.</summary>
    public const string PruneKey = "KnowledgeBase:Prune";

    /// <summary>
    /// The Postgres schema the table lives in, which is Marten's <c>public</c> unless the API has been
    /// configured otherwise. There is no flag: it is a property of the deployment, not of a run.
    /// </summary>
    public const string DatabaseSchemaKey = "KnowledgeBase:DatabaseSchema";

    /// <summary>The corpus a run is about when nothing says otherwise.</summary>
    public const string DefaultProvider = "5etools";

    /// <summary>Builds the command and its flags.</summary>
    public static Command Create(TextWriter output, TextWriter error)
    {
        var from = new Option<string?>("--from")
        {
            Description = "The 5eTools checkout, or its data/ folder. Required, with no default.",
        };
        var connection = new Option<string?>("--connection")
        {
            Description = "The Postgres connection string. Defaults to the dev database the API uses.",
        };
        var provider = new Option<string?>("--provider")
        {
            Description = $"Which corpus these rows belong to. Defaults to {DefaultProvider}.",
        };
        var dryRun = new Option<bool>("--dry-run")
        {
            Description = "Report the diff and write nothing at all.",
        };
        var prune = new Option<bool>("--prune")
        {
            Description = "Delete the stored rows this source does not have. Off by default.",
        };
        var force = new Option<bool>("--force")
        {
            Description = "Let --prune remove more than 20% of a provider's rows.",
        };
        // Not in the plan, and needed by the plan's own verification step: FiveEToolsParserOptions
        // floors a build at 1,000 monsters, and the fixture corpus the plan says to run against holds
        // eight. Without a way to lower it, `ingest --from …/Fixture/data` can only ever fail.
        var minMonsters = new Option<int?>("--min-monsters")
        {
            Description =
                "The monster count below which the parse refuses, as a guard against the wrong folder. "
                + $"Defaults to {FiveEToolsParserOptions.DefaultMinMonsters}; lower it only to ingest a "
                + "test corpus.",
        };

        var command = new Command("ingest", "Ingest a 5eTools data folder into the knowledge base.")
        {
            from, connection, provider, dryRun, prune, force, minMonsters,
        };

        command.SetAction((parseResult, cancellationToken) => RunAsync(
            new Flags(
                parseResult.GetValue(from),
                parseResult.GetValue(connection),
                parseResult.GetValue(provider),
                parseResult.GetValue(dryRun),
                parseResult.GetValue(prune),
                parseResult.GetValue(force),
                parseResult.GetValue(minMonsters)),
            output,
            error,
            cancellationToken));

        return command;
    }

    /// <summary>What the flags said, before configuration fills the gaps.</summary>
    private sealed record Flags(
        string? From,
        string? Connection,
        string? Provider,
        bool DryRun,
        bool Prune,
        bool Force,
        int? MinMonsters);

    private static async Task<int> RunAsync(
        Flags flags,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var report = new ReportWriter(output, error);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var provider = First(flags.Provider, configuration[ProviderKey]) ?? DefaultProvider;
        var connectionString = First(flags.Connection, configuration[ConnectionStringKey]);
        var databaseSchema = First(configuration[DatabaseSchemaKey]) ?? KnowledgeBaseSchema.DefaultDatabaseSchema;
        var minMonsters = flags.MinMonsters
            ?? Integer(configuration[MinMonstersKey])
            ?? FiveEToolsParserOptions.DefaultMinMonsters;
        var prune = flags.Prune || configuration.GetValue(PruneKey, false);

        if (First(flags.From, configuration[SourcePathKey]) is not { } source)
        {
            error.WriteLine(
                "  error   · no source folder. Pass --from <a 5etools checkout or its data/ folder>, or "
                + "set KnowledgeBase__Source__Path.");
            error.WriteLine("            Nothing was written.");
            return ExitCode.ParseFailure;
        }

        if (connectionString is null)
        {
            error.WriteLine($"  error   · no connection string. Pass --connection, or set {ConnectionStringKey}.");
            error.WriteLine("            Nothing was written.");
            return ExitCode.ParseFailure;
        }

        // --- the parse ------------------------------------------------------------------------------

        string dataDirectory;
        try
        {
            (dataDirectory, _) = FiveEToolsParser.FindDataDirectory(source);
        }
        catch (FiveEToolsBuildException failure)
        {
            report.ParseFailed(failure.Message);
            return ExitCode.ParseFailure;
        }

        // Before parsing, because the parser's own message for this — "cannot read …/spells/index.json"
        // — reads like a missing optional file when it is in fact the signature of a partial folder.
        // See ReportWriter.PartialFolder.
        var missingIndexes = RequiredIndexes
            .Where(relative => !File.Exists(Path.Combine(dataDirectory, relative)))
            .ToList();
        if (missingIndexes.Count > 0)
        {
            report.PartialFolder(dataDirectory, missingIndexes);
            return ExitCode.ParseFailure;
        }

        report.Header(provider, dataDirectory);

        FiveEToolsBuildResult parsed;
        try
        {
            parsed = FiveEToolsParser.Build(new FiveEToolsParserOptions
            {
                From = source,
                MinMonsters = minMonsters,
                OnFileRead = file => report.File(Path.GetRelativePath(dataDirectory, file)),
            });
        }
        catch (FiveEToolsBuildException failure)
        {
            report.ParseFailed(failure.Message);
            return ExitCode.ParseFailure;
        }

        report.Skipped(parsed.Report);

        var rows = KnowledgeBaseRow.From(provider, parsed.Index);
        var run = new IngestRun
        {
            Provider = provider,
            Rows = rows,
            DryRun = flags.DryRun,
            Prune = prune,
            Force = flags.Force,
        };

        // --- the write ------------------------------------------------------------------------------

        var store = new KnowledgeBaseStore(connectionString, databaseSchema: databaseSchema);

        IngestOutcome outcome;
        try
        {
            outcome = await store.RunAsync(run, cancellationToken);
        }
        catch (KnowledgeBaseSchemaMissingException missing)
        {
            report.SchemaMissing(missing.Message);
            return ExitCode.ParseFailure;
        }
        catch (Exception failure) when (failure is NpgsqlException or SocketException)
        {
            report.ConnectionFailed(failure.Message);
            return ExitCode.ParseFailure;
        }

        report.Diff(outcome.Report, parsed.Index.Counts);

        if (run.DryRun)
        {
            report.DryRun();
            return ExitCode.Ok;
        }

        if (outcome.Prune is { Refused: true } refused)
        {
            report.PruneRefused(refused);
            return ExitCode.PruneRefused;
        }

        report.Written(outcome.Report, run.Batch);

        if (outcome.Prune is { } pruned) report.Pruned(pruned);
        else report.NothingDeleted(outcome.Report);

        return ExitCode.Ok;
    }

    /// <summary>
    /// The two <c>index.json</c> files a 5eTools data folder has to have. Everything else it holds is
    /// optional — see <see cref="ReportWriter.PartialFolder" />.
    /// </summary>
    private static readonly string[] RequiredIndexes =
        [Path.Combine("bestiary", "index.json"), Path.Combine("spells", "index.json")];

    /// <summary>The first value that is actually set. An empty setting is not a value.</summary>
    private static string? First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static int? Integer(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
}

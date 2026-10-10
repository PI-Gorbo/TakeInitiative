using System.CommandLine;

using TakeInitiative.KnowledgeBase.Cli;

// The knowledge-base ingest (roadmap step 26c).
//
//   dotnet run --project apps/TakeInitiative.KnowledgeBase.Cli -- ingest \
//       --from ~/5etools-src/data [--connection "Host=…"] [--dry-run] [--prune] [--force]
//
// Production is the same command with --connection, run from the machine that has the
// 5eTools data over an SSH tunnel.
//
// --download (2026-10-10) fetches the latest release of --repository, ingests it and deletes
// it again. It reverses half of a decision this header used to state absolutely: a flag that
// fetches book content was rejected because it would put that automation into a public repo.
// What survives of that reason is the half that was load-bearing — THERE IS NO COMMITTED
// DEFAULT REPOSITORY. The code knows how to fetch a release archive; nothing committed here
// names whose. See roadmap step 26's decision 5 and its 2026-10-10 amendment.
//
// It writes rows and nothing else. The API owns the schema.
var root = new RootCommand("The TakeInitiative knowledge-base ingest.")
{
    IngestCommand.Create(Console.Out, Console.Error),
};

return await root.Parse(args).InvokeAsync();

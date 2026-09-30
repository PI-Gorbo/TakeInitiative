using System.CommandLine;

using TakeInitiative.KnowledgeBase.Cli;

// The knowledge-base ingest (roadmap step 26c).
//
//   dotnet run --project apps/TakeInitiative.KnowledgeBase.Cli -- ingest \
//       --from ~/5etools-src/data [--connection "Host=…"] [--dry-run] [--prune] [--force]
//
// Production is the same command with --connection, run from the machine that has the
// 5eTools data over an SSH tunnel. Nothing is ever downloaded: the operator supplies the
// folder, because 5eTools' content is not ours to redistribute and a --download flag would
// put automation for fetching book content into a public repository.
//
// It writes rows and nothing else. The API owns the schema.
var root = new RootCommand("The TakeInitiative knowledge-base ingest.")
{
    IngestCommand.Create(Console.Out, Console.Error),
};

return await root.Parse(args).InvokeAsync();

using Alba;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// Starts a test host without racing the others. FastEndpoints keeps its serializer options in a
/// process-wide static (<c>Config.SerOpts.Options</c>): every host start replaces it with a fresh
/// copy and then mutates that copy (<c>IgnoreToHeaderAttributes</c>). xUnit runs test classes in
/// parallel, each with its own host, so two starts can interleave, and a host already serving a
/// request can use the fresh copy before the start has finished with it. System.Text.Json then
/// makes it read-only and the start throws "This JsonSerializerOptions instance is read-only",
/// failing every test in the class on setup.
/// <para>
/// Starts go one at a time, and the rare clash with a running host is retried. The API itself
/// configures the enum converter on the ASP.NET JSON options (Program.cs), so the fresh copy is
/// complete from the moment it is published and a running host never reads half-built options.
/// </para>
/// </summary>
public static class HostStartup
{
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private const int Attempts = 3;

    public static async Task<IAlbaHost> Start(Func<Task<IAlbaHost>> start)
    {
        await OneAtATime.WaitAsync();
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await start();
                }
                catch (InvalidOperationException e) when (attempt < Attempts && e.Message.Contains("read-only"))
                {
                    // Another host serialized with the options between FastEndpoints publishing
                    // them and finishing with them. Start again: it makes a fresh copy.
                }
            }
        }
        finally
        {
            OneAtATime.Release();
        }
    }
}

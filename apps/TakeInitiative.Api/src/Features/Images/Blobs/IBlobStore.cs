namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// Where image bytes live (glossary: Blob store). The only thing in the app that talks to
/// the S3 API. The bucket is private: nothing ever links to it, and every byte a client
/// sees is streamed by the API after a visibility check.
/// </summary>
public interface IBlobStore
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

    /// <summary>The blob, or null when there is no blob with that key.</summary>
    Task<BlobRead?> GetAsync(string key, CancellationToken ct);

    /// <summary>Deletes the blob. A missing key is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>An open blob. Dispose it to release the stream (and the S3 connection behind it).</summary>
public sealed record BlobRead(Stream Content, long Length, string ContentType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

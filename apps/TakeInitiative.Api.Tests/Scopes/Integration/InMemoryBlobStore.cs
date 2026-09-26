using System.Collections.Concurrent;
using TakeInitiative.Api.Features.Images;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// The blob store of the default fixtures, so the ~400 integration tests need no MinIO.
/// <see cref="S3BlobStoreTests"/> runs the real <see cref="S3BlobStore"/> against MinIO.
/// </summary>
public class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _blobs = new();
    private int _failNextDeletes;

    /// <summary>Makes the next <paramref name="count"/> deletes throw, as a blob store outage would.</summary>
    public void FailNextDeletes(int count) => Interlocked.Exchange(ref _failNextDeletes, count);

    public bool Contains(string key) => _blobs.ContainsKey(key);

    public byte[] Bytes(string key) => _blobs[key].Bytes;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        _blobs[key] = (copy.ToArray(), contentType);
    }

    public Task<BlobRead?> GetAsync(string key, CancellationToken ct)
        => Task.FromResult(_blobs.TryGetValue(key, out var blob)
            ? new BlobRead(new MemoryStream(blob.Bytes, writable: false), blob.Bytes.LongLength, blob.ContentType)
            : null);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        if (Interlocked.Decrement(ref _failNextDeletes) >= 0)
        {
            throw new IOException("The blob store is down (InMemoryBlobStore.FailNextDeletes).");
        }
        Interlocked.Exchange(ref _failNextDeletes, 0);
        _blobs.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

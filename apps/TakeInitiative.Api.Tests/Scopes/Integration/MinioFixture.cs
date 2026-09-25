using Testcontainers.Minio;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// A real MinIO for <see cref="Features.Images.S3BlobStoreTests"/>, on the image
/// compose.dev.yml pins, so the S3 client's settings (path style, checksums) are tested
/// against the store dev runs. The other integration tests use <see cref="InMemoryBlobStore"/>.
/// </summary>
public class MinioFixture : IAsyncLifetime
{
    public const string Image = "quay.io/minio/minio:RELEASE.2025-07-18T21-56-31Z";

    public MinioContainer Container { get; } = new MinioBuilder().WithImage(Image).Build();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

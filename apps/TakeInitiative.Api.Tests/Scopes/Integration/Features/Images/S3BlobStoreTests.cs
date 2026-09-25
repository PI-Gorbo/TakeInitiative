using System.Text;
using Amazon.S3.Util;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Features.Images;

namespace TakeInitiative.Api.Tests.Integration.Features.Images;

/// <summary>Step 16a.3: <see cref="S3BlobStore"/> and <see cref="BlobBucketInitializer"/> against a real MinIO.</summary>
public class S3BlobStoreTests(MinioFixture minio) : IClassFixture<MinioFixture>
{
    private BlobOptions Options(string bucket) => new()
    {
        ServiceUrl = minio.Container.GetConnectionString(),
        Region = "us-east-1",
        Bucket = bucket,
        AccessKey = minio.Container.GetAccessKey(),
        SecretKey = minio.Container.GetSecretKey(),
        ForcePathStyle = true,
        CreateBucket = true,
    };

    private async Task<S3BlobStore> StoreWithBucket(string bucket)
    {
        var options = Options(bucket);
        var s3 = S3BlobStore.CreateClient(options);
        await new BlobBucketInitializer(s3, Microsoft.Extensions.Options.Options.Create(options), NullLogger<BlobBucketInitializer>.Instance)
            .StartAsync(CancellationToken.None);
        return new S3BlobStore(s3, Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public async Task TheBucket_IsCreatedOnStart_AndStartingAgainIsFine()
    {
        var options = Options("created-on-start");
        var s3 = S3BlobStore.CreateClient(options);
        (await AmazonS3Util.DoesS3BucketExistV2Async(s3, options.Bucket)).Should().BeFalse();

        var initializer = new BlobBucketInitializer(s3, Microsoft.Extensions.Options.Options.Create(options), NullLogger<BlobBucketInitializer>.Instance)
        {
            Attempts = 1,
        };
        await initializer.StartAsync(CancellationToken.None);
        (await AmazonS3Util.DoesS3BucketExistV2Async(s3, options.Bucket)).Should().BeTrue();

        await initializer.StartAsync(CancellationToken.None);
        (await AmazonS3Util.DoesS3BucketExistV2Async(s3, options.Bucket)).Should().BeTrue();
    }

    [Fact]
    public async Task NoBucketIsCreated_WhenCreateBucketIsOff()
    {
        var options = Options("not-created");
        options.CreateBucket = false;
        var s3 = S3BlobStore.CreateClient(options);

        await new BlobBucketInitializer(s3, Microsoft.Extensions.Options.Options.Create(options), NullLogger<BlobBucketInitializer>.Instance)
            .StartAsync(CancellationToken.None);

        (await AmazonS3Util.DoesS3BucketExistV2Async(s3, options.Bucket)).Should().BeFalse();
    }

    [Fact]
    public async Task PutGetDelete_RoundTrips()
    {
        var store = await StoreWithBucket("round-trip");
        var key = $"campaigns/{Guid.NewGuid()}/images/{Guid.NewGuid()}/display.webp";
        var bytes = ImageFixtures.Bytes(ImageFixtures.Webp);

        await store.PutAsync(key, new MemoryStream(bytes), "image/webp", CancellationToken.None);

        await using (var read = await store.GetAsync(key, CancellationToken.None))
        {
            read.Should().NotBeNull();
            read!.Length.Should().Be(bytes.Length);
            read.ContentType.Should().Be("image/webp");
            using var copy = new MemoryStream();
            await read.Content.CopyToAsync(copy);
            copy.ToArray().Should().Equal(bytes);
        }

        await store.DeleteAsync(key, CancellationToken.None);
        (await store.GetAsync(key, CancellationToken.None)).Should().BeNull();

        // Deleting twice is fine.
        await store.DeleteAsync(key, CancellationToken.None);
    }

    [Fact]
    public async Task AMissingKey_IsNull()
    {
        var store = await StoreWithBucket("missing");

        (await store.GetAsync("campaigns/nothing/here.webp", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task PuttingAKeyAgain_ReplacesIt()
    {
        var store = await StoreWithBucket("replace");
        await store.PutAsync("a.txt", new MemoryStream(Encoding.UTF8.GetBytes("first")), "text/plain", CancellationToken.None);
        await store.PutAsync("a.txt", new MemoryStream(Encoding.UTF8.GetBytes("second")), "text/plain", CancellationToken.None);

        await using var read = await store.GetAsync("a.txt", CancellationToken.None);
        using var reader = new StreamReader(read!.Content);
        (await reader.ReadToEndAsync()).Should().Be("second");
    }
}

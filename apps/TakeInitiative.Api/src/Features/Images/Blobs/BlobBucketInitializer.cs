using Amazon.S3;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// Creates the bucket on start when <see cref="BlobOptions.CreateBucket"/> is set (dev).
/// A hosted service, so <c>--export-openapi</c>, which never starts the host, needs no
/// blob store. MinIO may still be starting when the API does, so it retries for a while,
/// and then logs rather than failing the whole API: only images need the bucket.
/// </summary>
public class BlobBucketInitializer(IAmazonS3 s3, IOptions<BlobOptions> options, ILogger<BlobBucketInitializer> logger) : IHostedService
{
    public int Attempts { get; init; } = 15;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public async Task StartAsync(CancellationToken ct)
    {
        if (!options.Value.CreateBucket)
        {
            return;
        }

        var bucket = options.Value.Bucket;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await EnsureBucket(s3, bucket);
                logger.LogInformation("Blob store bucket {Bucket} is ready at {ServiceUrl}", bucket, options.Value.ServiceUrl);
                return;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                if (attempt >= Attempts)
                {
                    logger.LogError(e, "Could not create the blob store bucket {Bucket} at {ServiceUrl}. Image uploads will fail until it exists.",
                        bucket, options.Value.ServiceUrl);
                    return;
                }
                await Task.Delay(RetryDelay, ct);
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public static async Task EnsureBucket(IAmazonS3 s3, string bucket)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3, bucket))
        {
            await s3.PutBucketAsync(bucket);
        }
    }
}

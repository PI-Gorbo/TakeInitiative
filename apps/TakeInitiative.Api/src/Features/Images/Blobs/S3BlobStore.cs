using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Images;

/// <summary><see cref="IBlobStore"/> over the S3 API, with <c>AWSSDK.S3</c>.</summary>
public class S3BlobStore(IAmazonS3 s3, IOptions<BlobOptions> options) : IBlobStore
{
    private string Bucket => options.Value.Bucket;

    /// <summary>
    /// The client for any S3-compatible store. Checksums are sent and validated only when
    /// an operation requires them: recent SDKs send CRC32 checksums by default, which some
    /// S3-compatible stores reject.
    /// </summary>
    public static IAmazonS3 CreateClient(BlobOptions options)
        => new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region,
                ForcePathStyle = options.ForcePathStyle,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // A signed, seekable body rather than aws-chunked uploads, which not every
            // S3-compatible store supports.
            UseChunkEncoding = false,
        }, ct);
    }

    public async Task<BlobRead?> GetAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await s3.GetObjectAsync(new GetObjectRequest { BucketName = Bucket, Key = key }, ct);
            return new BlobRead(response.ResponseStream, response.ContentLength, response.Headers.ContentType);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        try
        {
            await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Bucket, Key = key }, ct);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // Already gone.
        }
    }
}

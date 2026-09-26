namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// The <c>Blobs</c> configuration section: any S3-compatible bucket. The dev defaults in
/// appsettings.json point at the MinIO of compose.dev.yml.
/// </summary>
public class BlobOptions
{
    public const string SectionKey = "Blobs";

    public string ServiceUrl { get; set; } = "http://localhost:7404";
    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = "takeinitiative";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    /// <summary>Path-style URLs (<c>host/bucket/key</c>). MinIO, Garage and R2 all accept them.</summary>
    public bool ForcePathStyle { get; set; } = true;
    /// <summary>Create the bucket on start. On in dev, off in production, where the bucket is provisioned.</summary>
    public bool CreateBucket { get; set; }
}

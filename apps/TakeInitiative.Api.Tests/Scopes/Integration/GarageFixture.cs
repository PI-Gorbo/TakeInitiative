using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// A real Garage for <see cref="Features.Images.S3BlobStoreTests"/>, on the image
/// compose.dev.yml pins. The other integration tests use <see cref="InMemoryBlobStore"/>.
/// </summary>
/// <remarks>
/// There is no Testcontainers module for Garage, and unlike MinIO it cannot be configured by
/// environment variables: it needs a config file, a cluster layout, and an access key created
/// through its own CLI. <see cref="InitializeAsync"/> does that bootstrap.
/// </remarks>
public class GarageFixture : IAsyncLifetime
{
    public const string Image = "dxflrs/garage:v2.3.0";

    /// <summary>A Garage key ID must be <c>GK</c> followed by 24 hex characters; it rejects anything else.</summary>
    public const string AccessKey = "GKb7c1f39a84e2d0516c9ab427";
    public const string SecretKey = "4f1c9b2e7a35d86014fb59ce2d7a8306e4195bc7fa2063de8471c95ab2e60f3d";

    private const string RpcSecret = "9d2f7b14a6c3e58017fd4b92ce60a73518ebd2c4f7069a3b5d81ce42f9607ab3";

    /// <summary>
    /// `s3_region` has to match <see cref="Api.Features.Images.BlobOptions.Region"/>, or every
    /// request fails SigV4 verification rather than failing as a configuration error.
    /// </summary>
    private static readonly string Config = $"""
        metadata_dir = "/var/lib/garage/meta"
        data_dir = "/var/lib/garage/data"
        db_engine = "sqlite"
        replication_factor = 1
        rpc_bind_addr = "[::]:3901"
        rpc_public_addr = "127.0.0.1:3901"
        rpc_secret = "{RpcSecret}"

        [s3_api]
        s3_region = "us-east-1"
        api_bind_addr = "[::]:3900"
        root_domain = ".s3.garage.localhost"

        [admin]
        api_bind_addr = "[::]:3903"
        admin_token = "test-admin-token"
        """;

    public IContainer Container { get; } = new ContainerBuilder()
        .WithImage(Image)
        .WithResourceMapping(System.Text.Encoding.UTF8.GetBytes(Config), "/etc/garage.toml")
        .WithPortBinding(3900, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilCommandIsCompleted("/garage", "status"))
        .Build();

    public string GetConnectionString() =>
        $"http://{Container.Hostname}:{Container.GetMappedPublicPort(3900)}";

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        var nodeId = await NodeId();
        await Run("/garage", "layout", "assign", "-z", "dc1", "-c", "1G", nodeId);
        await Run("/garage", "layout", "apply", "--version", "1");

        await Run("/garage", "key", "import", AccessKey, SecretKey, "-n", "takeinitiative", "--yes");
        // The tests create a bucket per case through BlobBucketInitializer, which is S3
        // CreateBucket. An imported key cannot do that by default.
        await Run("/garage", "key", "allow", "--create-bucket", AccessKey);
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();

    private async Task<string> NodeId()
    {
        var status = await Run("/garage", "status");
        var line = status.Split('\n').FirstOrDefault(l => l.Contains("NO ROLE ASSIGNED"))
            ?? throw new InvalidOperationException($"Garage reported no unassigned node:\n{status}");
        return line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private async Task<string> Run(params string[] command)
    {
        var result = await Container.ExecAsync(command);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"`{string.Join(' ', command)}` exited {result.ExitCode}: {result.Stderr}{result.Stdout}");
        }
        return result.Stdout;
    }
}

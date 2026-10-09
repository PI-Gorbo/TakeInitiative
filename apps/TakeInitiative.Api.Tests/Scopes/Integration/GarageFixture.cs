using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// A real Garage for <see cref="Features.Images.S3BlobStoreTests"/>, configured exactly as
/// compose.dev.yml and Coolify's one-click template are. The other integration tests use
/// <see cref="InMemoryBlobStore"/>.
/// </summary>
/// <remarks>
/// <c>--single-node</c> assigns the cluster layout and <c>--default-bucket</c> creates the key
/// and bucket, so there is no CLI bootstrap to run. A fresh Garage node without a layout refuses
/// every request, which is what those flags exist to avoid.
/// </remarks>
public class GarageFixture : IAsyncLifetime
{
    public const string Image = "dxflrs/garage:v2.3.0";

    public const string AccessKey = "takeinitiative";
    public const string SecretKey = "takeinitiative-test-secret";
    public const string Bucket = "takeinitiative";

    /// <summary>
    /// Must match <c>s3_region</c> below. Garage rejects a mismatch with
    /// <c>AuthorizationHeaderMalformed</c>, which reads as a credentials problem.
    /// </summary>
    public const string Region = "garage";

    private const string RpcSecret = "9d2f7b14a6c3e58017fd4b92ce60a73518ebd2c4f7069a3b5d81ce42f9607ab3";

    private static readonly string Config = """
        metadata_dir = "/var/lib/garage/meta"
        data_dir = "/var/lib/garage/data"
        db_engine = "lmdb"
        replication_factor = 1
        consistency_mode = "consistent"
        rpc_bind_addr = "[::]:3901"
        rpc_secret_file = "env:GARAGE_RPC_SECRET"
        bootstrap_peers = []

        [s3_api]
        s3_region = "garage"
        api_bind_addr = "[::]:3900"
        root_domain = ".s3.garage.localhost"

        [admin]
        api_bind_addr = "[::]:3903"
        admin_token_file = "env:GARAGE_ADMIN_TOKEN"
        """;

    public IContainer Container { get; } = new ContainerBuilder()
        .WithImage(Image)
        .WithResourceMapping(System.Text.Encoding.UTF8.GetBytes(Config), "/etc/garage.toml")
        .WithCommand("/garage", "server", "--single-node", "--default-bucket")
        .WithEnvironment("GARAGE_RPC_SECRET", RpcSecret)
        .WithEnvironment("GARAGE_ADMIN_TOKEN", "test-admin-token")
        .WithEnvironment("GARAGE_ALLOW_WORLD_READABLE_SECRETS", "true")
        .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", AccessKey)
        .WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", SecretKey)
        .WithEnvironment("GARAGE_DEFAULT_BUCKET", Bucket)
        .WithPortBinding(3900, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("/garage", "status"))
        .Build();

    public string GetConnectionString() =>
        $"http://{Container.Hostname}:{Container.GetMappedPublicPort(3900)}";

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

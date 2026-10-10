using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Bootstrap;

using ApiBootstrap = TakeInitiative.Api.Bootstrap.Bootstrap;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// The cookie key ring (29b). Authentication is cookie-based, so every sign-in ticket is a Data
/// Protection payload. Nothing used to call <c>AddDataProtection</c>, so the ring was written to
/// <c>$HOME/.aspnet/DataProtection-Keys</c> inside the container and thrown away with it: every
/// redeploy signed every user out. <c>DataProtection:KeyPath</c> puts it on a volume instead.
/// <para>
/// The registration order here mirrors <c>Program.cs</c> on purpose:
/// <c>AddDataProtectionKeyRing</c> runs before
/// <c>AddIdentityAuthenticationAndAuthorization</c>, and <c>AddAuthentication</c> calls
/// <c>AddDataProtection()</c> itself. That second call must not undo the first — it would leave the
/// ring back inside the container, which is exactly the bug — so
/// <see cref="TheSetting_Set_SurvivesAddAuthentication"/> pins it.
/// </para>
/// <para>
/// Unset used to no-op everywhere, which is how the bug came back a release later (SAM-27), so it
/// now throws in Production and the facts below name their environment.
/// <see cref="TheSetting_Absent_InProduction_ExportingOpenApi_ConfiguresNothing"/> is the one that
/// keeps `pnpm gen:api` working.
/// </para>
/// </summary>
public class DataProtectionKeyRingTests
{
    private const string Purpose = "TakeInitiativeTests";

    [Fact]
    public void TheSetting_Absent_ConfiguresNothing()
    {
        using var directory = new TempDirectory();

        var services = Build(keyPath: null);

        // Nothing is registered, so ASP.NET Core's own default applies — which is what dev and the
        // Alba fixtures have always had, and what this change must not disturb.
        services.GetService<IDataProtectionProvider>().Should().BeNull();
        Directory.GetFiles(directory.Path).Should().BeEmpty();
    }

    [Fact]
    public void TheSetting_Set_PersistsToThatDirectory()
    {
        using var directory = new TempDirectory();

        var services = Build(directory.Path);

        services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository
            .Should().BeOfType<FileSystemXmlRepository>()
            .Which.Directory.FullName.Should().Be(directory.Path);

        // The application name, not the content root, is the discriminator. The default is the
        // content-root path, so keys written under one WORKDIR could not be read under another and
        // the volume would have preserved a ring nothing could use.
        services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator
            .Should().Be(ApiBootstrap.DataProtectionApplicationName);
    }

    [Fact]
    public void TheSetting_Set_WritesTheKeyRingWhereTheVolumeIsMounted()
    {
        using var directory = new TempDirectory();

        var services = Build(directory.Path);
        services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("ticket");

        Directory.GetFiles(directory.Path, "key-*.xml").Should()
            .HaveCount(1, "the ring is on the volume, not in the container's home directory");
    }

    [Fact]
    public void TheSetting_Set_LetsASecondProcessReadTheFirstsTickets()
    {
        using var directory = new TempDirectory();

        // The whole point of the setting: this is a redeploy. Two processes, nothing shared but the
        // directory, and the ticket the first one issued still decrypts in the second.
        var before = Build(directory.Path).GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose).Protect("session-for-sam");

        var after = Build(directory.Path).GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose).Unprotect(before);

        after.Should().Be("session-for-sam");
    }

    [Fact]
    public void TheSetting_Set_CreatesTheDirectoryItIsGiven()
    {
        using var parent = new TempDirectory();
        var keyPath = Path.Combine(parent.Path, "keys");

        // /keys is mkdir'd in the image, but a bind mount to a path that does not exist yet must not
        // fail the first sign-in either.
        Build(keyPath).GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("ticket");

        Directory.GetFiles(keyPath, "key-*.xml").Should().HaveCount(1);
    }

    [Fact]
    public void TheSetting_Blank_ConfiguresNothing()
    {
        // An environment variable set to an empty string is how a deployment "unsets" a value, and
        // DirectoryInfo("") throws. Treat it as absent.
        Build("   ").GetService<IDataProtectionProvider>().Should().BeNull();
    }

    [Fact]
    public void TheSetting_Absent_InProduction_FailsStartup()
    {
        // The whole of SAM-27. Unset here means an ephemeral ring, which means the next release is a
        // mass sign-out that nothing logs. A failed boot is the cheaper failure, and the message has
        // to be enough to fix it from.
        var start = () => Build(keyPath: null, Environments.Production);

        start.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{ApiBootstrap.DataProtectionKeyPathKey}*")
            .And.Message.Should().Contain("DataProtection__KeyPath=/keys")
            .And.Contain("docs/deploy/coolify.md");
    }

    [Fact]
    public void TheSetting_Blank_InProduction_FailsStartup()
    {
        var start = () => Build("   ", Environments.Production);

        start.Should().Throw<InvalidOperationException>(
            "an environment variable set to an empty string is how a deployment unsets one");
    }

    [Fact]
    public void TheSetting_Absent_InProduction_ExportingOpenApi_ConfiguresNothing()
    {
        // `pnpm gen:api` runs `dotnet run --no-launch-profile -- --export-openapi`, which reads no
        // launch profile and so is Production too. It builds the app and exits without serving, so
        // the key ring it will never use must not fail a CI gate.
        var services = Build(keyPath: null, Environments.Production, willServeRequests: false);

        services.GetService<IDataProtectionProvider>().Should().BeNull();
    }

    [Fact]
    public void TheSetting_Set_InProduction_PersistsToThatDirectory()
    {
        using var directory = new TempDirectory();

        var services = Build(directory.Path, Environments.Production);

        services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository
            .Should().BeOfType<FileSystemXmlRepository>()
            .Which.Directory.FullName.Should().Be(directory.Path);
    }

    [Fact]
    public void TheSetting_Set_SurvivesAddAuthentication()
    {
        using var directory = new TempDirectory();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtectionKeyRing(
            Config(directory.Path), new HostEnvironment(Environments.Development), willServeRequests: true);
        // Program.cs reaches this a few lines later, and it calls AddDataProtection() again.
        services.AddAuthentication();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository
            .Should().BeOfType<FileSystemXmlRepository>()
            .Which.Directory.FullName.Should().Be(directory.Path);
        provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator
            .Should().Be(ApiBootstrap.DataProtectionApplicationName);
    }

    private static ServiceProvider Build(
        string? keyPath,
        // The literal, not Environments.Development, which is static readonly rather than const.
        string environmentName = "Development",
        bool willServeRequests = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtectionKeyRing(
            Config(keyPath), new HostEnvironment(environmentName), willServeRequests);
        return services.BuildServiceProvider();
    }

    private static IConfiguration Config(string? keyPath)
    {
        var settings = new Dictionary<string, string?>();
        if (keyPath is not null)
        {
            settings[ApiBootstrap.DataProtectionKeyPathKey] = keyPath;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    /// <summary>Just enough <see cref="IHostEnvironment"/> to name an environment.</summary>
    private sealed class HostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "TakeInitiative.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() => Directory.CreateDirectory(Path);

        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ti-keys-" + Guid.NewGuid().ToString("n"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}

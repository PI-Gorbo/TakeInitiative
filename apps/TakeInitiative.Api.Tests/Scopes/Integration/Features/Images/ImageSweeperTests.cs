using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Images;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Images;

/// <summary>Step 16a.7: images on no note go after a day, and a failed blob delete is retried.</summary>
public class ImageSweeperTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private ImageSweeper Sweeper => fixture.AlbaHost.Services.GetRequiredService<ImageSweeper>();

    private async Task<Image?> Load(Guid imageId)
    {
        await using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return await session.LoadAsync<Image>(imageId);
    }

    private bool HasBlobs(Guid campaignId, Guid imageId)
        => fixture.Blobs.Contains(Image.BlobKey(campaignId, imageId, "display"))
            || fixture.Blobs.Contains(Image.BlobKey(campaignId, imageId, "thumb"));

    [Fact]
    public async Task AnUnpostedImage_IsKeptAt23Hours_AndGoneAt25()
    {
        var campaign = await TestCampaign.Create(fixture, "Sweep unposted");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        using (fixture.Clock.Advance(TimeSpan.FromHours(23)))
        {
            await Sweeper.SweepOnce(fixture.Clock.GetUtcNow());
        }
        (await Load(image.Id)).Should().NotBeNull();
        HasBlobs(campaign.Id, image.Id).Should().BeTrue();
        (await fixture.GetImageVariant(campaign.Id, image.Id, "thumb")).Context.Response.StatusCode.Should().Be(200);

        using (fixture.Clock.Advance(TimeSpan.FromHours(25)))
        {
            var result = await Sweeper.SweepOnce(fixture.Clock.GetUtcNow());
            result.Marked.Should().BeGreaterThanOrEqualTo(1);
        }
        (await Load(image.Id)).Should().BeNull();
        fixture.Blobs.Contains(Image.BlobKey(campaign.Id, image.Id, "display")).Should().BeFalse();
        fixture.Blobs.Contains(Image.BlobKey(campaign.Id, image.Id, "thumb")).Should().BeFalse();
        (await fixture.GetImageVariant(campaign.Id, image.Id, "thumb")).Context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ADeletedImagesBlobs_AreRetried_WhenTheFirstDeleteThrew()
    {
        var campaign = await TestCampaign.Create(fixture, "Sweep retry");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        // The blob store is down when the uploader removes the image: the remove still
        // answers, and the image is gone for every reader at once.
        fixture.Blobs.FailNextDeletes(1);
        (await fixture.DeleteImage(campaign.Id, image.Id)).Should().Be(204);
        var marked = await Load(image.Id);
        marked.Should().NotBeNull();
        marked!.DeletedAt.Should().NotBeNull();
        HasBlobs(campaign.Id, image.Id).Should().BeTrue();
        (await fixture.GetImageVariant(campaign.Id, image.Id, "thumb")).Context.Response.StatusCode.Should().Be(404);

        // The next sweep fails once more, then the one after succeeds.
        fixture.Blobs.FailNextDeletes(1);
        (await Sweeper.SweepOnce(fixture.Clock.GetUtcNow())).Failed.Should().BeGreaterThanOrEqualTo(1);
        (await Load(image.Id)).Should().NotBeNull();

        await Sweeper.SweepOnce(fixture.Clock.GetUtcNow());
        (await Load(image.Id)).Should().BeNull();
        HasBlobs(campaign.Id, image.Id).Should().BeFalse();
    }

    [Fact]
    public async Task ASweep_LeavesRecentUploadsAlone()
    {
        var campaign = await TestCampaign.Create(fixture, "Sweep recent");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        await Sweeper.SweepOnce(fixture.Clock.GetUtcNow());

        (await Load(image.Id))!.DeletedAt.Should().BeNull();
        HasBlobs(campaign.Id, image.Id).Should().BeTrue();
    }
}

using System.Text;
using FluentAssertions;
using TakeInitiative.Api.Features.Images;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Images;

/// <summary>
/// Step 16a.6: upload, serve and delete. The uploader is <see cref="Users.Player"/>; the DM
/// is <see cref="Users.DM"/> and the other player <see cref="Users.Outsider"/>, joined by code.
/// </summary>
public class ImageUploadTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    [Fact]
    public async Task AnUpload_IsStoredAsTwoVariants_AndServedToItsUploader()
    {
        var campaign = await TestCampaign.Create(fixture, "Upload");
        fixture.LoginAsUser(Users.Player);

        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.RotatedExif6Gps);

        (image.Width, image.Height).Should().Be((200, 400));
        image.UploadedAt.Should().BeCloseTo(fixture.Clock.GetUtcNow(), TimeSpan.FromMinutes(1));
        foreach (var variant in new[] { "display", "thumb" })
        {
            fixture.Blobs.Contains(Image.BlobKey(campaign.Id, image.Id, variant)).Should().BeTrue();

            var served = await fixture.GetImageVariant(campaign.Id, image.Id, variant);
            var response = served.Context.Response;
            response.StatusCode.Should().Be(200);
            response.ContentType.Should().Be("image/webp");
            response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
            response.Headers.ContentDisposition.ToString().Should().Be("inline");
            response.Headers.CacheControl.ToString().Should().Be("private, no-cache");
            response.Headers.ETag.ToString().Should().Be($"\"{image.Id}-{variant}\"");
            var bytes = Encoding.ASCII.GetString(await ReadBytes(served), 0, 12);
            bytes.Should().StartWith("RIFF").And.EndWith("WEBP");
        }
    }

    [Fact]
    public async Task IfNoneMatch_GivesNotModified_ToTheUploader()
    {
        var campaign = await TestCampaign.Create(fixture, "Not modified");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var etag = $"\"{image.Id}-thumb\"";

        var cached = await fixture.GetImageVariant(campaign.Id, image.Id, "thumb", etag);
        cached.Context.Response.StatusCode.Should().Be(304);
        cached.Context.Response.Headers.ETag.ToString().Should().Be(etag);
        (await ReadBytes(cached)).Should().BeEmpty();

        // Another variant's ETag does not match.
        var other = await fixture.GetImageVariant(campaign.Id, image.Id, "thumb", $"\"{image.Id}-display\"");
        other.Context.Response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData(Users.DM)]
    [InlineData(Users.Outsider)]
    public async Task AnUnpostedImage_IsA404_ForEveryoneElse(Users viewer)
    {
        var campaign = await TestCampaign.Create(fixture, $"Unposted {viewer}");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        fixture.LoginAsUser(viewer);
        foreach (var variant in new[] { "display", "thumb" })
        {
            (await fixture.GetImageVariant(campaign.Id, image.Id, variant)).Context.Response.StatusCode.Should().Be(404);
            // Not even with the ETag the uploader got: the check runs before "not modified".
            (await fixture.GetImageVariant(campaign.Id, image.Id, variant, $"\"{image.Id}-{variant}\""))
                .Context.Response.StatusCode.Should().Be(404);
        }
        (await fixture.DeleteImage(campaign.Id, image.Id)).Should().Be(404);
        fixture.Blobs.Contains(Image.BlobKey(campaign.Id, image.Id, "display")).Should().BeTrue();
    }

    [Fact]
    public async Task AnyOtherVariantName_IsA404()
    {
        var campaign = await TestCampaign.Create(fixture, "Variant names");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        foreach (var variant in new[] { "original", "Display", "display.webp", "full" })
        {
            (await fixture.GetImageVariant(campaign.Id, image.Id, variant)).Context.Response.StatusCode.Should().Be(404);
        }
        (await fixture.GetImageVariant(campaign.Id, Guid.NewGuid(), "thumb")).Context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task AnotherCampaignsImage_IsA404_EvenToItsUploader()
    {
        var first = await TestCampaign.Create(fixture, "First");
        var second = await TestCampaign.Create(fixture, "Second");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(first.Id, ImageFixtures.Webp);

        (await fixture.GetImageVariant(second.Id, image.Id, "thumb")).Context.Response.StatusCode.Should().Be(404);
        (await fixture.DeleteImage(second.Id, image.Id)).Should().Be(404);
    }

    [Fact]
    public async Task ANonMember_Gets403_OnTheCampaignFirst()
    {
        var campaign = await TestCampaign.Create(fixture, "Members only", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        fixture.LoginAsUser(Users.Outsider);
        (await fixture.GetImageVariant(campaign.Id, image.Id, "thumb")).Context.Response.StatusCode.Should().Be(403);
        (await fixture.UploadImage(campaign.Id, ImageFixtures.Bytes(ImageFixtures.Webp))).Status.Should().Be(403);
        (await fixture.DeleteImage(campaign.Id, image.Id)).Should().Be(403);
    }

    [Fact]
    public async Task OverTwentyMegabytes_IsA413()
    {
        var campaign = await TestCampaign.Create(fixture, "Too big");
        fixture.LoginAsUser(Users.Player);
        var big = new byte[20 * 1024 * 1024 + 1];
        ImageFixtures.Bytes(ImageFixtures.Webp).CopyTo(big, 0);

        var (status, body, _) = await fixture.UploadImage(campaign.Id, big, "big.webp", "image/webp");

        status.Should().Be(413);
        body.Should().Contain("20 MB");
    }

    [Theory]
    [InlineData(ImageFixtures.Svg, "image/svg+xml")]
    [InlineData(ImageFixtures.NotAnImage, "image/jpeg")]
    [InlineData(ImageFixtures.Heic, "image/heic")]
    public async Task UnsupportedTypes_Are415_WhateverTheirName(string fixture_, string contentType)
    {
        var campaign = await TestCampaign.Create(fixture, $"Unsupported {fixture_}");
        fixture.LoginAsUser(Users.Player);

        var (status, body, _) = await fixture.UploadImage(campaign.Id, ImageFixtures.Bytes(fixture_), fixture_, contentType);

        status.Should().Be(415);
        body.Should().Contain("This image type is not supported. Try JPEG or PNG.");
    }

    [Fact]
    public async Task TooManyPixels_AndDamagedFiles_Are400()
    {
        var campaign = await TestCampaign.Create(fixture, "Bad images");
        fixture.LoginAsUser(Users.Player);

        (await fixture.UploadImage(campaign.Id, ImageFixtures.Bytes(ImageFixtures.Bomb), "bomb.png", "image/png")).Status.Should().Be(400);
        (await fixture.UploadImage(campaign.Id, ImageFixtures.Bytes(ImageFixtures.Truncated))).Status.Should().Be(400);
    }

    [Fact]
    public async Task ARequestWithNoFile_IsA400()
    {
        var campaign = await TestCampaign.Create(fixture, "No file");
        fixture.LoginAsUser(Users.Player);
        using var content = new MultipartFormDataContent { { new StringContent("hello"), "caption" } };

        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Post.MultipartFormData(content).ToUrl(WebAppClientExtensions.ImagesUrl(campaign.Id));
            _.StatusCodeShouldBe(400);
        });
        (await result.ReadAsTextAsync()).Should().Contain("file");
    }

    [Fact]
    public async Task TheTwentyFirstUnpostedImage_IsA409_UntilOneIsRemoved()
    {
        var campaign = await TestCampaign.Create(fixture, "Twenty");
        fixture.LoginAsUser(Users.Player);
        var images = new List<ImageResponse>();
        for (var i = 0; i < 20; i++)
        {
            images.Add(await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp));
        }

        var (status, body, _) = await fixture.UploadImage(campaign.Id, ImageFixtures.Bytes(ImageFixtures.Webp));
        status.Should().Be(409);
        body.Should().Contain("Post or remove some images first.");

        // The limit is per member: the DM can still upload.
        fixture.LoginAsUser(Users.DM);
        await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteImage(campaign.Id, images[0].Id)).Should().Be(204);
        await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
    }

    [Fact]
    public async Task TheUploader_DeletesAnUnpostedImage_BytesAndAll()
    {
        var campaign = await TestCampaign.Create(fixture, "Delete");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        fixture.LoginAsUser(Users.DM);
        (await fixture.DeleteImage(campaign.Id, image.Id)).Should().Be(404);

        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteImage(campaign.Id, image.Id)).Should().Be(204);

        fixture.Blobs.Contains(Image.BlobKey(campaign.Id, image.Id, "display")).Should().BeFalse();
        fixture.Blobs.Contains(Image.BlobKey(campaign.Id, image.Id, "thumb")).Should().BeFalse();
        (await fixture.GetImageVariant(campaign.Id, image.Id, "thumb")).Context.Response.StatusCode.Should().Be(404);
        (await fixture.DeleteImage(campaign.Id, image.Id)).Should().Be(404);
    }

    private static async Task<byte[]> ReadBytes(Alba.IScenarioResult result)
    {
        using var copy = new MemoryStream();
        if (result.Context.Response.Body.CanSeek) result.Context.Response.Body.Position = 0;
        await result.Context.Response.Body.CopyToAsync(copy);
        return copy.ToArray();
    }
}

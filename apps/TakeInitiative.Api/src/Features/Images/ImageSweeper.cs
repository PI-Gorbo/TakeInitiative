using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// Deletes images for real. Hourly, on the gap prompt's clock: it marks every image still
/// on no note a day after its upload, and then deletes the blobs of every marked image and
/// then its document. A blob delete that fails leaves the document marked, so the next
/// sweep retries it.
/// </summary>
public class ImageSweeper(
    IDocumentStore store,
    IBlobStore blobs,
    [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock,
    IOptions<ImageOptions> options,
    ILogger<ImageSweeper> logger) : BackgroundService
{
    public record SweepResult(int Marked, int Purged, int Failed);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(options.Value.SweepStartDelay, stoppingToken);
            using var timer = new PeriodicTimer(options.Value.SweepInterval);
            do
            {
                try
                {
                    var result = await SweepOnce(clock.GetUtcNow(), stoppingToken);
                    if (result.Marked + result.Purged + result.Failed > 0)
                    {
                        logger.LogInformation("Image sweep: {Marked} unposted marked, {Purged} deleted, {Failed} to retry",
                            result.Marked, result.Purged, result.Failed);
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogError(e, "The image sweep failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    public async Task<SweepResult> SweepOnce(DateTimeOffset now, CancellationToken ct = default)
    {
        var marked = await MarkUnposted(now, ct);

        List<Image> deleted;
        await using (var query = store.QuerySession())
        {
            deleted = [.. await query.Query<Image>().Where(i => i.DeletedAt != null).ToListAsync(ct)];
        }

        int purged = 0, failed = 0;
        foreach (var image in deleted)
        {
            try
            {
                await Purge(image, ct);
                purged++;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failed++;
                logger.LogWarning(e, "Could not delete image {ImageId}; the next sweep retries", image.Id);
            }
        }
        return new SweepResult(marked, purged, failed);
    }

    /// <summary>Marks every image still on no note <see cref="ImageOptions.UnpostedLifetime"/> after its upload.</summary>
    private async Task<int> MarkUnposted(DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now - options.Value.UnpostedLifetime;
        await using var session = store.LightweightSession();
        var stale = await session.Query<Image>()
            .Where(i => i.DeletedAt == null && i.NoteId == null && i.UploadedAt < cutoff)
            .ToListAsync(ct);

        var marked = 0;
        foreach (var image in stale)
        {
            // One save per image, so an image attached to a note in the meantime (16b)
            // loses nothing but its own mark.
            await using var one = store.LightweightSession();
            image.DeletedAt = Microseconds.Truncate(now);
            one.Update(image);
            try
            {
                await one.SaveChangesAsync(ct);
                marked++;
            }
            catch (ConcurrencyException)
            {
                logger.LogInformation("Image {ImageId} changed while being swept; skipped", image.Id);
            }
        }
        return marked;
    }

    /// <summary>Deletes a marked image's blobs and then its document.</summary>
    public async Task Purge(Image image, CancellationToken ct)
    {
        foreach (var variant in image.Variants.All())
        {
            await blobs.DeleteAsync(variant.Key, ct);
        }
        await using var session = store.LightweightSession();
        session.Delete(image);
        await session.SaveChangesAsync(ct);
    }
}

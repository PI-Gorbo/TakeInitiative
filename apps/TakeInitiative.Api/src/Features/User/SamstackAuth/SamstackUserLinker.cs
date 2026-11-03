using CSharpFunctionalExtensions;
using Marten;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace TakeInitiative.Api.Features.Users;

/// <summary>
/// Finds the <see cref="ApplicationUser"/> a SamStack user is, so every claim, campaign and member
/// keeps the app's own user id (SamStack spec §6.3):
/// <list type="number">
/// <item>Already linked: the user whose <see cref="ApplicationUser.AuthUserId"/> is theirs.</item>
/// <item>First sign-in of an existing player: the unlinked user with the same email, ignoring case,
/// is linked, once and for good. SamStack only issues sessions for confirmed emails, which is what
/// makes the match safe; TakeInitiative never required confirmation itself.</item>
/// <item>Nobody: a new player (TakeInitiative's access model is public), created already linked.</item>
/// </list>
/// A user whose email matches someone already linked to a different SamStack account is refused
/// rather than merged; that needs a person to look at it.
/// </summary>
public class SamstackUserLinker(IDocumentStore store, ILookupNormalizer normalizer, ILogger<SamstackUserLinker> logger)
{
    public async Task<Result<Guid>> Resolve(SamstackUser user, CancellationToken ct)
    {
        try
        {
            return await Attempt(user, ct);
        }
        catch (Exception e) when (IsUniqueViolation(e))
        {
            // Two first requests raced to create or link the same player, and the other one won.
            // Its write is now there to be found, so a second attempt is all it takes.
            return await Attempt(user, ct);
        }
    }

    private async Task<Result<Guid>> Attempt(SamstackUser user, CancellationToken ct)
    {
        await using var session = store.LightweightSession();

        var linked = await session.Query<ApplicationUser>()
            .FirstOrDefaultAsync(x => x.AuthUserId == user.Id, ct);
        if (linked is not null)
        {
            return linked.Id;
        }

        var normalizedEmail = normalizer.NormalizeEmail(user.Email);
        var sameEmail = await session.Query<ApplicationUser>()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);

        if (sameEmail is { AuthUserId: not null })
        {
            logger.LogWarning(
                "Refused SamStack user {AuthUserId} ({Email}): user {UserId} has that email and is linked to SamStack user {LinkedAuthUserId}",
                user.Id, user.Email, sameEmail.Id, sameEmail.AuthUserId);
            return Result.Failure<Guid>("This email belongs to a user linked to another SamStack account.");
        }

        if (sameEmail is not null)
        {
            sameEmail.AuthUserId = user.Id;
            session.Store(sameEmail);
            await session.SaveChangesAsync(ct);
            logger.LogInformation(
                "Linked user {UserId} to SamStack user {AuthUserId} by email {Email} at {LinkedAt}",
                sameEmail.Id, user.Id, user.Email, DateTimeOffset.UtcNow);
            return sameEmail.Id;
        }

        var created = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            AuthUserId = user.Id,
            Email = user.Email,
            NormalizedEmail = normalizedEmail,
            // SamStack's name, which can hold spaces Identity's user name validator would refuse;
            // TakeInitiative only ever displays it.
            UserName = user.Name,
            NormalizedUserName = normalizer.NormalizeName(user.Name),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        session.Store(created);
        await session.SaveChangesAsync(ct);
        logger.LogInformation(
            "Created user {UserId} for SamStack user {AuthUserId} ({Email}) at {LinkedAt}",
            created.Id, user.Id, user.Email, DateTimeOffset.UtcNow);
        return created.Id;
    }

    // Marten wraps the Postgres error, and how deep depends on the call, so look down the chain.
    private static bool IsUniqueViolation(Exception? e)
        => e is not null
            && (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } || IsUniqueViolation(e.InnerException));
}

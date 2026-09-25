using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// An entry to create with a note: picked as "Create …" in the composer. The web makes
/// <see cref="Id"/> (<c>crypto.randomUUID()</c>) and has already written it into the note's
/// text as <c>@[name](entry:&lt;id&gt;)</c>, so the text is never rewritten.
/// </summary>
public record NewEntryRequest
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required EntryKind Kind { get; init; }
}

/// <summary>Used through <see cref="NewEntries.NewEntriesList{T}"/>, not discovered as an endpoint validator.</summary>
public class NewEntryRequestValidator : AbstractValidator<NewEntryRequest>
{
    public NewEntryRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).EntryName();
        RuleFor(x => x.Kind).IsInEnum();
    }
}

/// <summary>
/// Creating entries together with a session note (<c>POST notes</c> and <c>PUT notes/{id}</c>).
/// Each new entry is an <see cref="EntryCreated"/> appended in the same
/// <c>SaveChangesAsync</c> as the note, so they share a transaction and a correlation id. Its
/// creator is the note's author, its visibility the note's, and its
/// <see cref="EntryCreated.CreatedFromNoteId"/> the note.
/// <list type="bullet">
/// <item>An id the text does not mention is a 400 (<c>errors.newEntries</c>).</item>
/// <item>An id that already has a stream is a 409 with the id in
/// <c>errors.alreadyCreatedEntryId</c>. A retry after a timeout reuses the same ids, so the
/// web shows "Already created" and reloads.</item>
/// <item>A name that is the name or an alias of an entry the author can see is 15a's 409,
/// with <c>errors.existingEntryId</c> (that entry) and <c>errors.newEntryId</c> (the clashing
/// new one).</item>
/// </list>
/// </summary>
public static class NewEntries
{
    public const int MaxPerNote = 10;
    public const string ErrorKey = "newEntries";
    public const string AlreadyCreatedEntryIdKey = "alreadyCreatedEntryId";
    public const string NewEntryIdKey = "newEntryId";

    /// <summary>At most <see cref="MaxPerNote"/>, each valid, with distinct ids and names.</summary>
    public static IRuleBuilderOptions<T, IEnumerable<NewEntryRequest>> NewEntriesList<T>(this IRuleBuilder<T, NewEntryRequest[]?> rule, string what = "A session note")
        => rule
            .Must(list => list is null || list.Length <= MaxPerNote)
            .WithMessage($"{what} can create at most {MaxPerNote} entries.")
            .Must(list => list is null || list.Select(e => e.Id).Distinct().Count() == list.Length)
            .WithMessage("Each new entry needs its own id.")
            .Must(list => list is null || list.Select(e => (e.Name ?? "").Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == list.Length)
            .WithMessage("Two new entries cannot have the same name.")
            .ForEach(each => each.SetValidator(new NewEntryRequestValidator()));

    /// <summary>
    /// The visibility an entry created from <paramref name="note"/> gets: the note's, except
    /// that a hidden <c>Everyone</c> note gives <c>DM</c>, the audience it actually has
    /// (<see cref="SessionNoteAudience"/>), so creating an entry reveals nothing.
    /// </summary>
    public static Visibility VisibilityFrom(SessionNote note)
        => note.IsHidden && note.Visibility == Visibility.Everyone ? Visibility.DM : note.Visibility;

    /// <summary>
    /// Checks <paramref name="newEntries"/> and appends an <see cref="EntryCreated"/> for each.
    /// The caller saves them with the note's events. Returns the new ids, for
    /// <see cref="NotifyCreated"/> after the save.
    /// </summary>
    public static Task<IReadOnlyList<Guid>> AppendNewEntries<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IDocumentSession session, Guid campaignId, Member author,
        Guid noteId, string text, Visibility visibility, IReadOnlyList<NewEntryRequest>? newEntries, CancellationToken ct)
        where TRequest : notnull
        => endpoint.AppendNewEntries(session, campaignId, author, text, "the note's text", newEntries, _ => visibility, noteId, ct);

    /// <summary>
    /// The general form, shared by notes and articles (15e): each new entry's visibility comes
    /// from <paramref name="visibilityOf"/>, and <paramref name="where"/> names the text in the
    /// 400's message. The checks and error keys are the same for both.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> AppendNewEntries<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IDocumentSession session, Guid campaignId, Member author,
        string text, string where, IReadOnlyList<NewEntryRequest>? newEntries, Func<NewEntryRequest, Visibility> visibilityOf,
        Guid? createdFromNoteId, CancellationToken ct)
        where TRequest : notnull
    {
        if (newEntries is null || newEntries.Count == 0)
        {
            return [];
        }

        var mentioned = MentionParser.EntryIds(text).ToHashSet();
        var unmentioned = newEntries.FirstOrDefault(e => !mentioned.Contains(e.Id));
        if (unmentioned is not null)
        {
            endpoint.ThrowError(new ValidationFailure(ErrorKey,
                $"The new entry \"{unmentioned.Name.Trim()}\" is not mentioned in {where}."), StatusCodes.Status400BadRequest);
        }

        foreach (var entry in newEntries)
        {
            if (await session.Events.FetchStreamStateAsync(entry.Id, ct) is not null)
            {
                endpoint.AddError(new ValidationFailure(ErrorKey, $"The entry \"{entry.Name.Trim()}\" has already been created."));
                endpoint.AddError(new ValidationFailure(AlreadyCreatedEntryIdKey, entry.Id.ToString()));
                endpoint.ThrowIfAnyErrors(StatusCodes.Status409Conflict);
            }
        }

        var visible = await session.Query<Entry>()
            .Listed(campaignId, author)
            .ToListAsync(ct);
        foreach (var entry in newEntries)
        {
            var existing = visible.FirstOrDefault(e => EntryNameRules.IsCalled(e, entry.Name.Trim()));
            if (existing is not null)
            {
                endpoint.AddError(new ValidationFailure(ErrorKey, $"There is already an entry called \"{existing.Name}\"."));
                endpoint.AddError(new ValidationFailure(PostEntry.ExistingEntryIdKey, existing.Id.ToString()));
                endpoint.AddError(new ValidationFailure(NewEntryIdKey, entry.Id.ToString()));
                endpoint.ThrowIfAnyErrors(StatusCodes.Status409Conflict);
            }
        }

        foreach (var entry in newEntries)
        {
            session.Events.StartStream<Entry>(entry.Id, new EntryCreated(
                Actor: Actor.Member(author.MemberId),
                CampaignId: campaignId,
                CreatorMemberId: author.MemberId,
                Name: entry.Name.Trim(),
                Kind: entry.Kind,
                Visibility: visibilityOf(entry),
                CreatedFromNoteId: createdFromNoteId));
        }
        return newEntries.Select(e => e.Id).ToList();
    }

    /// <summary>Pushes each created entry to its audience with <c>entryUpserted</c>. Call it after the note's push.</summary>
    public static async Task NotifyCreated(this IHubContext<CampaignHub> hub, IQuerySession session, IReadOnlyList<Guid> entryIds, CancellationToken ct)
    {
        if (entryIds.Count == 0)
        {
            return;
        }
        var entries = await session.LoadManyAsync<Entry>(ct, entryIds);
        foreach (var id in entryIds)
        {
            var entry = entries.FirstOrDefault(e => e.Id == id);
            if (entry is not null)
            {
                await hub.NotifyEntryUpserted(entry);
            }
        }
    }
}

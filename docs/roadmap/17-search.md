# 17 — ⌘K search

## Goal

**⌘K search** finds anything in a campaign that the viewer can see. ⌘K / Ctrl+K on
desktop and the 🔍 button everywhere open the search sheet from 13d, which is full
screen on a phone. Results come in **search sections**: **Entries** (names and
aliases, typo-tolerant, plus article text), **Notes**, **Images** (image notes by
caption) and **Sessions** (by number or title). **Actions** come after them: create an
entry, start the next session, post a note about an entry, and go to a tab or a
filter. `@` searches entries only, and `>` searches actions only. The server matches
with Postgres full-text search (`tsvector`) plus `pg_trgm`. It applies visibility
**inside the SQL**, down to secret blocks, so a hidden note, entry or block adds no
hit, no snippet text, no count and no change of rank for anyone outside its audience
(invariant 5). Entry matching is one reusable service, the **entry matcher**, so loose
ends (19) and suggestions (23) use the same trigram search. §11a requires this.

The step ships as three PRs, stacked with `gh stack` on top of this file's docs PR,
which sits on 16e (#219). Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 17a | `v2/17a-search-api` | Search index and query API | 16's app unchanged in the browser. `GET search` answers sections with snippets, per viewer, and the leak tests pass | [x] |
| 17b | `v2/17b-search-sheet` | The search sheet | ⌘K and 🔍 search for real: sections, highlighted snippets, keyboard navigation, tappable rows on a phone, and every hit opens where it lives | [x] |
| 17c | `v2/17c-search-actions` | Actions | The Actions section and `>`: create an entry, start the next session, post a note about X, go to. The step's Verify passes | [x] |

Combats (the COMBATS section and "⚔ Start combat") are step 18. Loose ends, as an
action and as the entry matcher's second user, are step 19. The REFERENCE section is
steps 20 and 21 (§11). This step leaves a seam for each (Notes) and builds none of
them.

## Depends on

**15**: entries, aliases, articles, secret blocks, merges and `MentionIndex`. **16** is
also needed, for the Images section (`HasImages`, thumbnails through `ImageAccess`).
These parts of [12-v2-design-session.md](12-v2-design-session.md) are binding:

- the glossary (§1);
- ⌘K on mobile (§3a);
- ⌘K search (§7);
- the read models (§9);
- the invariants (§10, especially 5, 10 and 11);
- the `ISearchProvider` seam (§11);
- "the same trigram search as ⌘K" (§11a).

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.

**This PR (docs)**
- `docs/roadmap/12-v2-design-session.md` §1: the new nouns (step 0 below)
- `docs/roadmap/README.md`: link step 17, `in progress`

**17a**
- API add `src/Features/Search/{SearchQuery,SearchDoc,Snippet,SearchService,SearchDrift,ISearchProvider}.cs`
- API add `src/Features/Search/Sql/{SearchSql,SearchSchema,SearchVisibilitySql,SearchConnection}.cs`
- API add `src/Features/Search/Providers/{WikiSearchProvider,SessionSearchProvider}.cs`
- API add `src/Features/Search/Matching/{EntryMatcher,EntryMatch}.cs`
- API add `src/Features/Search/Api/GetSearch/{GetSearch,SearchResponse}.cs`
- API modify `src/boostrap/Bootstrap.cs` (the two extensions, the note index and the article vector column), `Program.cs` (`AddSearch`), `GlobalUsings.cs`
- API modify `src/Features/Entries/Mentions/MentionIndex.cs` (`CountsForEntries`)
- Tests add `Scopes/Unit/{SearchQueryTests,SnippetTests}.cs`
- Tests add `Scopes/Integration/Features/Search/{SearchSeed,SearchTests,SearchLeakTests,SearchVisibilityParityTests,SearchConsistencyTests,EntryMatcherTests,SearchSchemaTests,SearchPerfTests}.cs`
- Web `utils/api/schema.d.ts`: regenerated

**17b**
- Web add `utils/search.ts` (input parsing, rows, cursor, snippet segments, destinations, recents), `utils/api/search/getSearchRequest.ts`, `utils/queries/search.ts`
- Web add `components/Search/{SearchResults,SearchHitRow,SearchSnippet}.vue`
- Web modify `components/SearchSheet.vue`, `layouts/campaign.vue` (the campaign id, focus, and ⌘K beside the composer's), `composables/useApi.ts`, `utils/api/types.ts`
- Web modify `pages/app/campaigns/[campaignId]/index.vue` and `components/Session/SessionStream.vue` (`?session=`), `pages/app/campaigns/[campaignId]/wiki/[entryId].vue` and `components/Wiki/{Article,ArticleBlock}.vue` (`?block=`)
- Web add `tests/unit/search.test.ts`

**17c**
- Web add `utils/searchActions.ts`, `components/Search/{SearchActionRow,CreateEntryRow}.vue`
- Web modify `components/SearchSheet.vue`, `components/Search/SearchResults.vue` (an `action` slot), `utils/search.ts` (`action` rows, `actionRows`)
- Web modify `components/Composer/Composer.vue` and `pages/app/campaigns/[campaignId]/index.vue` (`?compose=`)
- Web add `tests/unit/searchActions.test.ts`

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/16e-share-target
gh stack add v2/17-step-file
```

Add each sub-step on top with `gh stack add v2/17a-search-api` and so on.

**Glossary check.** Reference (§1) and `SearchDoc` (§9) exist already. This PR adds the
nouns the step puts into code and UI:

- **⌘K search** (code: `Search`): campaign-wide search, opened with ⌘K / Ctrl+K or 🔍.
  It shows results in sections, then actions, and finds only what the viewer can see.
- **Search section**: one heading of ⌘K results: `Entries`, `Notes`, `Images`,
  `Sessions` or `Actions` (later `Combats` and `Reference`).
- **Snippet**: the words of a matching session note or block shown under a result, with
  the match highlighted. It is cut from exactly one note or block that the viewer can
  see.
- **Action**: something ⌘K does, where a hit is something ⌘K finds: create an entry,
  start the next session, post a note about an entry, or go to a tab. `>` searches
  actions only.
- **Search provider** (code: `ISearchProvider`): a source of ⌘K results. Each fills one
  or more sections.
- **Search doc** (code: `SearchDoc`): one searchable unit with exactly one audience:
  an entry's names, one article block, one session note or one session. It is the
  row shape of the search query, built from the projected documents and never stored.
- **Entry matcher** (code: `EntryMatcher`): trigram matching of a piece of text
  against entry names and aliases, under the viewer's visibility. ⌘K, loose ends (19)
  and suggestions (23) share it.

### 17a. Search index and query API

1. **Extensions.** `pg_trgm` (similarity) and `unaccent` (accent folding for names).
   Add them as Weasel `Extension` schema objects in `opts.Storage.ExtendedSchemaObjects`,
   so `ApplyAllDatabaseChangesOnStartup` creates them.
   - Both come with `postgres:16-alpine`, the image that dev (`compose.dev.yml`) and
     both Testcontainers fixtures use. `pg_available_extensions` on `takedb` lists
     `pg_trgm` 1.6 and `unaccent` 1.1, and neither is installed yet.
   - Both are **trusted** extensions from PG 13, so a database owner that is not a
     superuser can create them.
   - Nothing is paid, and nothing new runs (invariant 10).
2. **Search docs** (`SearchDoc`, §9). Each searchable unit has **exactly one audience**,
   so the SQL filter is one predicate per row, and a unit's text is either wholly
   visible to the viewer or not read at all.

   | Unit | Source row | Text searched | Audience (read rule it mirrors) |
   |---|---|---|---|
   | Entry names | `mt_doc_entry`, not merged | `Name`, each of `Aliases` | `EntryVisibility.VisibleTo` |
   | Article block | one element of the entry's `Article.Blocks` | the block's `Text` | `EntryVisibility.CanSeeBlock`: the entry's audience **and** the block's |
   | Session note | `mt_doc_sessionnote` | `Text` (for an image note, the caption) | `SessionNoteVisibility.VisibleTo`, with hiding |
   | Session | `mt_doc_session` | `Number`, `Title` | every member |

   ```csharp
   record SearchDoc(SearchDocKind Kind, Guid SourceId, Guid? BlockId, double Rank, int Category, string? Headline);
   ```

   `SearchDocKind` has three cases — `ArticleBlock`, `SessionNote` and `Session` — and
   **no `EntryName`**. A name or alias hit never becomes a `SearchDoc`: it is the entry
   matcher's answer (step 9), and it comes back as an `EntryMatch`, which carries the
   name that matched and whether it was an alias.

   The rows come from SQL over the inline-projected documents. There is **no search
   table** (Notes, "Why no stored search table"). Every source is an inline projection
   that is updated in the same transaction as its event (§9), and Postgres maintains the
   note index and the article column in that same transaction — a stored generated column is
   computed by the statement that writes the row. So a search sees each post, edit, hide,
   visibility change, article edit, merge and delete **as soon as it commits**, with no
   lag and no rebuild. There is nothing to backfill, because the indexes are built over
   existing rows when 17a first starts.
3. **Indexes** (step 1's objects, plus what is added to the document mappings). Each
   vector is a constant in `SearchSql`, and the query uses the same constant: for the
   note index, because that is what makes Postgres recognise the expression and use the
   index; for the article column, because the column is the thing the query names.

   | Index | On | Vector | Serves |
   |---|---|---|---|
   | `mt_doc_sessionnote_idx_search` | `mt_doc_sessionnote` (GIN) | the expression `to_tsvector('simple', PlainText(data ->> 'Text'))` (step 5) | Notes and Images |
   | `mt_doc_entry_idx_search` | `mt_doc_entry` (GIN) | the **stored generated column** `search_vector`, `to_tsvector('simple'::regconfig, jsonb_path_query_array(data, '$."Article"."Blocks"[*]."Text"'::jsonpath))` | article prefilter |

   - The note index is an expression index: the expression uses only built-in immutable
     functions, so it depends on nothing step 1 creates, whatever order Marten applies
     schema objects in.
   - The article vector is a **column**, not an expression, because the planner does not
     choose a GIN index for a prefix `tsquery` at a campaign's size: a partial-match scan
     of the index costs more than the sequential scan it has to do anyway, so an
     expression index left Postgres computing
     `to_tsvector(jsonb_path_query_array(…))` for every entry in the table on every
     search — 30 ms of the 33 ms the article query took (step 12). Stored, Postgres
     computes it once, in the same statement that writes the entry document, and the
     query names the column, so the filter is one column read per row whichever plan is
     chosen and the GIN index on it is a plain column index.
   - Weasel drops a column it does not know about, so the column is declared on the
     document table Marten builds from the mapping (`SearchSchema`), not beside it, and
     the `GENERATED ALWAYS AS (…) STORED` clause rides on the column as a Weasel
     `ColumnCheck`. A check is not part of a column's identity, and Weasel reads only a
     name and a type back from `information_schema`, so the diff sees
     `search_vector tsvector` either way and there is no churn.
   - `mt_doc_entry` also gets `toast_tuple_target = 8160` (`EntryRowFitsInline`, a schema
     object of its own, ordered after the table). Without it the 1.2 KB vector plus the
     1.4 KB document is over the default 2 KB target and Postgres moves `data` out of
     line, which costs every name match more than the prefilter wins: 4.9 ms became
     17.4 ms, 200 buffers became 9,179. It applies to rows written after it, so on a
     database that already has entries — the `ADD COLUMN` rewrites the table just before
     it runs — each entry settles on its next edit, and `VACUUM FULL public.mt_doc_entry`
     settles them all at once.
   - The article vector covers **every** block, secret ones too. It is only a
     **prefilter** that narrows the candidate entries. Each candidate is then re-checked
     block by block, on the blocks the viewer can see (step 6). A hit on a secret block
     alone therefore yields nothing.
   - Names, aliases and session titles have no index. They are scanned per campaign
     through the existing `(CampaignId, Kind)` index and `Session`'s unique
     `(CampaignId, Number)`. That is at most a few thousand short strings.
4. **The query** (`SearchQuery.Parse(raw)`, pure and unit tested).
   - Trimmed, and 1–100 characters after the prefix. Otherwise a 400, "Search for 1 to
     100 characters.".
   - **Prefixes.** A leading `@` is scope `Entries`. A leading `>` never reaches the
     server: actions are client-side (17c).
   - **Tokens.** The text is split on anything that is not a letter or a digit
     (`\p{L}\p{N}`) and lowercased, keeping the first 8 tokens. The tsquery is built
     from those tokens only: `'tok1' & 'tok2':*`. The last token is a prefix, so typing
     "gund" finds "Gundren". The whole string goes to `to_tsquery('simple', @q)` as a
     **parameter**, so quotes, `&`, `|`, `!`, `:` and `*` are plain text. There are no
     search operators (Notes).
   - **Session numbers.** `12`, `s12`, `S 12` and `session 12` also parse as number 12.
   - **Length rule.** One character searches Entries (names only, prefix) and Sessions
     (number) only. Two or more search everything.
5. **Text for matching and for snippets** (`SearchSql.PlainText`). This expression
   turns stored text into what a reader sees. There are four steps, and the order
   matters:
   - backslash escapes are removed **first**, so an escaped `\(entry:…\)` cannot hide
     from the two steps that follow;
   - `@[text](entry:<id>)` becomes `text`, so an entry id never appears in a snippet or
     matches a query;
   - any bare `entry:<36 characters>` still there is taken out **unconditionally**, with
     its brackets or without them. Note and block text is length-validated and nothing
     more, so a member can type a destination by hand, or leave the tail of a malformed
     or unclosed mention behind, and it would otherwise reach a snippet;
   - the two private-use characters that snippets use as markers (U+E000, U+E001) are
     deleted from the source, so no member can forge a highlight.

   Markdown markers (`*`, `_`, `` ` ``, and a leading `#` or `>`) are dropped in C# while
   the snippet is converted (step 7).
6. **Visibility inside the SQL** (`SearchVisibilitySql`). This builds one parameterised
   fragment per unit kind from the viewer (`MemberId`, `Role`). Each fragment is the SQL
   twin of the C# rule named in step 2's table:

   ```sql
   -- note, DM viewer
   (d.data ->> 'AuthorMemberId' = @me OR d.data ->> 'Visibility' IN ('Everyone', 'DM'))
   -- note, Player viewer
   (d.data ->> 'AuthorMemberId' = @me OR (d.data ->> 'Visibility' = 'Everyone'
       AND NOT coalesce((d.data ->> 'IsHidden')::boolean, false)))
   -- entry (plus d.data ->> 'MergedIntoId' IS NULL), DM / Player
   (d.data ->> 'CreatorMemberId' = @me OR d.data ->> 'Visibility' IN ('Everyone', 'DM'))
   (d.data ->> 'CreatorMemberId' = @me OR d.data ->> 'Visibility' = 'Everyone')
   -- block b (from jsonb_array_elements(d.data -> 'Article' -> 'Blocks')), inside the entry's fragment
   (b ->> 'OwnerMemberId' = @me OR b ->> 'Visibility' = 'Everyone'
       OR (@isDm AND b ->> 'Visibility' = 'DM'))
   ```

   - **Order of work.** Every query filters by campaign and visibility in the same
     `WHERE` as the match. It computes ranks and headlines only on rows that passed,
     and snippets only after `LIMIT`.
   - **Parity.** `SearchVisibilityParityTests` checks each fragment against its C# rule
     for every case (step 12), the way 14b's `SessionNoteAudienceTests` did for pushes.
   - **Belt and braces.** The providers load the returned documents (at most `take` + 1
     per section) to build responses. They re-run `SessionNoteVisibility.CanSee` or
     `EntryVisibility.CanSeeBlock` on each one. A row that fails is logged as an error
     **and thrown** (`SearchDrift`), so the endpoint 500s. It is not dropped: the drop
     would happen after Postgres had already applied `LIMIT take + 1`, so it would turn
     a would-be leak into a lost visible hit and a `hasMore` that counted it — a wrong
     answer, quietly. A 500 says the SQL and C# rules have drifted, which is a bug to
     fix and not a case to degrade through. `SearchVisibilityParityTests` is what keeps
     the throw unreachable.
7. **Snippets** (`Snippet`). How they avoid leaking (Notes, "Why snippets cannot
   leak"):
   - A snippet is `ts_headline('simple', PlainText(text), query, 'StartSel=U+E000,
     StopSel=U+E001, MaxWords=24, MinWords=10, ShortWord=2, MaxFragments=1')`.
   - It is computed over **one** returned unit: one note's text, or one block's text,
     never an entry's whole article.
   - C# turns the markers into `highlights: { start, length }[]` over a plain `text`,
     and drops Markdown markers while doing so. The API never sends HTML, and the web
     draws highlights as text nodes inside `<mark>` (no `v-html`).
   - An article hit takes its snippet from the best-ranked **visible** matching block.
     A name or alias hit has no snippet, and shows "aka Rockseeker" when an alias
     matched.
   - A snippet may legitimately have **no** highlights, and that is not drift: a row is
     matched on an indexed vector and its headline is cut from what a reader sees, and the
     two can differ (step 5). A session title matched down the trigram ladder has no
     `tsquery` lexeme for `ts_headline` to mark at all. The hit is real either way, so it
     is shown with the words and no highlights, and it never costs the viewer the hit.
8. **Ranking**, per section. No section has a total, only `hasMore` (from `take` + 1
   visible rows).
   - **Entries.** The category comes first, and it is the web's `matchRank` (15d),
     extended:
     - 0: exact;
     - 1: prefix;
     - 2: word prefix;
     - 3: substring;
     - 4: fuzzy (`word_similarity` ≥ `EntryMatchOptions.MinSimilarity`, 0.5 by
       default, for queries of 3 or more characters);
     - 5: article only.

     Names and aliases are compared folded (`lower(unaccent(…))`, like `foldForMatch`).
     Within a category, entries are ordered by the viewer's mention count, then the
     block's `ts_rank_cd` for article hits, then the name A–Z. SQL returns up to 50
     candidates by category and name **for names and aliases**; the article query returns
     at most `take` + 1, and it excludes the entries the names have already matched,
     because a name match always wins and an article hit is the last rung of the ladder.
     `MentionIndex.CountsForEntries` (new: the `CountsFor` rule, restricted to those ids
     through the GIN `?|` fragment) counts them for this viewer, and C# sorts them and
     takes `take`.
   - **Notes / Images.** `ts_rank_cd(vector, query)` descending, then `PostedAt`
     descending. Images is `HasImages`, and Notes is the rest, so an image note appears
     once.
   - **Sessions.** A number match first, then the title down the same ladder names use
     (exact, prefix, word prefix, substring, fuzzy). `word_similarity` is the tie-break
     **within** a rung of that ladder — without it two fuzzy title matches would come
     back in session order rather than best first — and `Number` descending breaks the
     tie after it.
   - `ts_rank_cd` and `similarity` read only the row they score. Postgres keeps no
     corpus statistics for them, so adding a hidden row cannot move a visible one.
9. **Entry matcher** (`EntryMatcher`, registered a **singleton**, §11a). It holds
   nothing between calls — the session, the campaign and the viewer are all arguments — so
   there is nothing for a scope to own.

   ```csharp
   Task<IReadOnlyList<IReadOnlyList<EntryMatch>>> MatchAsync(
       IQuerySession session, Guid campaignId, Member viewer,
       IReadOnlyList<string> spans, EntryMatchOptions options, CancellationToken ct,
       SearchConnection? connection = null);
   record EntryMatch(Guid EntryId, string MatchedName, bool IsAlias, int Category, double Similarity);
   record EntryMatchOptions(int Take = 5, double MinSimilarity = 0.5, bool FuzzyOnly = false);
   ```

   - One SQL round trip matches every span (`unnest(@spans) WITH ORDINALITY`) against
     the campaign's listed entries that the viewer can see.
   - Merged entries are left out. Their names are their target's aliases (15g), so they
     resolve to the target.
   - The Entries section calls it with one span. Loose ends (19: "this note says
     Gundren, link it?") and suggestions (23: "match before creating") call it with
     many.
   - The trailing `connection` is the search's own (step 10). A caller outside a search —
     loose ends (19), suggestions (23), a test — passes nothing and gets a connection for
     that one statement.
10. **Providers** (§11).

    ```csharp
    interface ISearchProvider
    {
        IReadOnlyList<SearchSectionKey> Sections { get; }
        Task<IReadOnlyList<SearchSection>> SearchAsync(SearchQuery query, SearchContext context, CancellationToken ct);
    }
    record SearchContext(IQuerySession Session, Guid CampaignId, Member Viewer, IReadOnlySet<SearchSectionKey> Wanted, int Take)
    {
        public SearchConnection? Connection { get; init; }
    }
    ```

    - `WikiSearchProvider` fills Entries, through `EntryMatcher` plus the article query.
      Wiki results come first (§11).
    - `SessionSearchProvider` fills Notes, Images and Sessions.
    - `SearchService` runs the providers in registration order, one after another on
      one session (a Marten session is not thread-safe). It keeps the wanted sections,
      in the order Entries, Notes, Images, Sessions, and leaves empty sections out.
    - It also opens **one** pooled connection for the whole search (`SearchConnection`,
      the context's init-only `Connection`) and disposes it at the end, rather than one
      per statement: the four or five statements run one after another anyway, and a
      connection each is a pool round trip for nothing. It is opened lazily, so a search
      that asks Postgres nothing opens nothing.
    - Step 18 adds a combat provider, and 20 and 21 add reference providers, as more
      registrations.
11. **Endpoint.** It takes `{campaignId}` and resolves the caller's member with
    `RequireMember` first.

    | Endpoint | Who | Does |
    |---|---|---|
    | `GET /api/campaigns/{campaignId}/search?q=&sections=&take=` | members | Runs the providers for the viewer and answers `SearchResponse`. `sections` is a comma list of `entries,notes,images,sessions` (default all, and only `entries` for an `@` query). `take` is 1–20, default 5. A non-member gets 403, as on every campaign route |

    ```
    SearchResponse { query: string, sections: SearchSection[] }
    SearchSection  { key: "Entries" | "Notes" | "Images" | "Sessions", hasMore: bool, hits: SearchHit[] }
    SearchHit      { kind: "Entry" | "Note" | "Session",
                     entry?:   { entry: EntrySummaryResponse, mentionCount, matchedOn: "Name" | "Alias" | "Article",
                                 alias?, blockId?, snippet?: Snippet },
                     note?:    { id, sessionId, sessionNumber, authorMemberId, postedAt, visibility, isRecap,
                                 images: NoteImageResponse[] /* first 4, Images only */, snippet: Snippet },
                     session?: { session: SessionResponse, snippet?: Snippet } }
    Snippet        { text: string, highlights: { start: int, length: int }[] }
    ```

    - **The enum values are PascalCase**, as every enum in this API is: the response goes
      through `JsonStringEnumConverter<T>`, which takes no naming policy. The `?sections=`
      query string is parsed case-insensitively, so `entries` and `Entries` both work.
    - Unknown section names, `take` outside 1–20, and an empty or overlong `q` are
      400s with `errors.q`, `errors.sections` and `errors.take`. An unknown name is a 400
      **including a numeric one**: `sections=0` is not a way to say `entries`, and it is
      rejected rather than silently read as the first section.
    - A note hit carries what a result row needs, not the whole note. The viewer can
      open the note anyway.
    - The query string is never stored. The web does not put it in the URL.
    - Step 20's REFERENCE adds `kind: "Reference"` and `reference?`, without changing
      the others.
12. **Performance budgets.**

    | What | Budget | As built | How it is held |
    |---|---|---|---|
    | `GET search`, all sections, `take=5`, on the large seed (below) | p50 ≤ 40 ms, p95 ≤ 100 ms at the API | **p50 17.6 ms, p95 62.4 ms** | GIN on note text; the article prefilter, stored (step 3); names scanned per campaign; headlines and counts only for returned rows |
    | A one-character query | p95 ≤ 60 ms | p50 10.8 ms, **p95 13.6 ms** | Entries by prefix and Sessions by number only: no article query and no `tsquery` at all |
    | Response size, `take=5` | ≤ 15 KB | largest **11.4 KB**, mean 5.7 KB over 120 queries | trimmed note hits, snippets of at most 24 words |
    | Postgres plans | the note query's expression is the one its index was built on, and the article query names the stored column | both | `SearchSchemaTests` checks with `EXPLAIN` under `SET LOCAL enable_seqscan = off`. That proves the query's expression **matches the stored index expression**, so a mismatch fails CI. It does **not** prove the planner chooses the index on real data — for a prefix `tsquery` at this size it does not, which is why step 3's article vector is a column: the filter is a column read whichever plan is chosen |

    Measured on an Apple Silicon Mac against `postgres:15-alpine` in Docker, 2026-09.
    Every budget holds. Before the article vector was stored, on the same machine and
    seed, all sections came to p50 51.9 ms / p95 101.6 ms — over both — and one character
    to p50 20.2 ms / p95 24.2 ms. The article query was 33 ms of that p50, 30 ms of it the
    prefilter; it is 0.3 ms now, and the entry matcher's scan of 1,000 entry documents
    came down with it (4.9 ms to 2.0 ms), because keeping the row inline (step 3) leaves
    `data` uncompressed and a name match no longer decompresses every row it reads.

    The cost is **linear in the number of entries**: names and aliases are scanned per
    campaign, by design (step 3), so the budget holds with room at 1,000 entries and
    would be reached again somewhere well above it. Keeping the row inline costs disk —
    `mt_doc_entry` is 4.0 MB for 1,000 entries where it was 1.6 MB compressed.

    The **large seed** (`SearchSeed`, appending events straight to streams, so the real
    projections run) is one campaign with 3 DMs and 5 players, 150 sessions, 5,000 notes
    of about 300 characters (10% `DM`, 5% `Me`, 2% hidden, 15% with images) and 1,000
    entries. Each entry has 2 aliases and an article of 5 blocks, one in five of them
    secret. `SearchPerfTests` runs 200 mixed queries and reports p50 and p95. It runs
    only when `TI_PERF=1` is set (it returns early otherwise), so CI times nothing.
    The web's budgets are in 17b.
13. **Web.** Regenerate `schema.d.ts`. No UI change.
14. **Tests.** A **leak test per visibility case** is the core of this step. Each
    case plants a unique token (for example `zanthor`) in the unit under test. It then
    searches as every relevant viewer (author or creator, another DM, a player, a
    member of another campaign), across all sections, and asserts on hits, snippets,
    `hasMore`, and ordering.
    - `SearchLeakTests` (one test each):
      1. a `DM` note: the author and DMs find it, and a player does not;
      2. a `Me` note: only its author, not even a DM;
      3. a hidden `Everyone` note: the author and DMs, not another player;
      4. a `DM` image note's caption: absent from a player's Images;
      5. a `DM` entry's name, and one of its aliases: no entry hit for a player;
      6. a `Me` entry: only its creator;
      7. a 🔒 DM block in an `Everyone` entry: a player gets no entry hit from it, and
         the DM gets the entry with a snippet containing the token;
      8. a 🔒 Me block: only its owner, not another DM;
      9. a quote promoted from a `DM` note: absent for a player;
      10. an ordinary block in a `DM` entry: absent for a player (the entry's audience
          applies too);
      11. **Mixed article.** A word in both a visible block and a secret block: the
          player's snippet is a substring of the visible block's text, and contains
          nothing from the secret block;
      12. **Ranking.** Two `Everyone` notes match. The player's order and `hasMore`
          (at `take=1`) are identical before and after adding a `DM` note that
          repeats the token twenty times;
      13. **Counts.** An entry hit's `mentionCount` equals the viewer's count in
          `GET entries`, with `DM` notes mentioning it present;
      14. **Merged entries.** A merged entry never appears as itself. Its old name finds
          its target as an alias;
      15. **Markup.** No snippet contains `entry:` or a Guid;
      16. **Campaigns.** Another campaign's member finds nothing from this one, and a
          non-member gets 403.
    - `SearchConsistencyTests`: search right after each write, with no wait:
      - post, edit (the token appears, then disappears), delete;
      - narrow to `DM`, hide, unhide;
      - entry visibility change, rename, alias add and remove;
      - article edit that moves the token into a 🔒 block;
      - merge.
    - `SearchVisibilityParityTests`: for notes (`Everyone`, `DM` or `Me`, hidden or not
      × author, another DM, a player), entries (3 visibilities × creator, DM, player) and
      blocks (entry visibility × block visibility × owner, DM, player), the ids that
      each SQL fragment accepts equal those the C# rule accepts.
    - `SearchTests`:
      - entries: exact before prefix before word prefix before fuzzy (`gundrn`, `rockseker`
        and `phandlin` match, and `goblin` does not match `Glasstaff`); alias hits say
        which alias matched; `gundren` finds `Gündren`;
      - scope and splitting: `@` returns only Entries; image notes are in Images and not
        Notes;
      - sessions: `12`, `s12`, `session 12` and a title word;
      - validation: the 400s; strings like `'`, `a & b | !c`, `:*` and `\` are a 200 and
        are searched as text;
      - `take` and `hasMore`.
    - `EntryMatcherTests`: several spans in one call, the threshold, visibility, and
      merged entries.
    - `SearchSchemaTests`:
      - both extensions exist;
      - `AssertDatabaseMatchesConfigurationAsync()` passes after startup **and after the
        configuration is applied a second time**, as a restart would, so neither the note
        index expression nor the article vector column causes schema churn — a column
        Weasel did not know about would be dropped on every start;
      - the article vector is a stored generated column of exactly the declared
        expression (`information_schema.columns`), its index is a GIN index on the column
        (`pg_get_indexdef`), and `mt_doc_entry` has the `toast_tuple_target` that keeps an
        entry's row in its page;
      - the `EXPLAIN` checks from step 12.
    - Unit tests:
      - `SearchQueryTests`: prefixes, tokens, the built tsquery, and session numbers;
      - `SnippetTests`: markers to ranges, Markdown and escapes dropped with the offsets
        still right, and marker characters in user text removed.

### 17b. The search sheet

1. **Querying** (`utils/queries/search.ts`).
   - The input is debounced by 120 ms (`refDebounced`). The query key is `["search",
     campaignId, scope, text]`, with `placeholderData: keepPreviousData`, `staleTime:
     0` and `gcTime: 60_000`.
   - TanStack Query's `signal` is passed to axios, so the next keystroke cancels a
     request still in flight.
   - Results are not pushed live: a search is a moment, and reopening searches again.
2. **Input** (`parseSearchInput`, pure). `@…` has scope `entries`, and `>…` has scope
   `actions`, with no request (17c). Anything else has scope `all`. An empty input
   sends no request. The text is cut at the server's 100 characters. An `entries`
   search sends the text without the `@` and `sections=entries` (`searchParams`).
3. **Rows** (`searchRows(response)`, pure) flattens the sections into rows under sticky
   headers (ENTRIES, NOTES, IMAGES, SESSIONS, in the server's order). A section with
   `hasMore` ends in a "Show more notes" row, which refetches that section alone with
   `take=20` and replaces it in place. A section shown in full has no second
   "Show more", since 20 is the server's largest `take`.

   | Hit | Row |
   |---|---|
   | Entry | kind icon, name, "aka Rockseeker" when an alias matched, "Character · 7 mentions", 🔒 when not `Everyone`; an article hit adds its snippet |
   | Note | the snippet, "Sam · S12 · Sat 20 Sep", 📜 for a recap, 🔒 DM / 🔒 Me |
   | Image | up to four `thumb` tiles (`imageUrl`, 16c), the caption snippet, "Priya · S13" |
   | Session | "Session 12 · Sat 20 Sep · The Triboar Trail". When the title matched, the line stops at the date and the title's snippet sits under it |

   `SearchSnippet` draws `highlights` as `<mark>` around text nodes, never as HTML.
4. **Where a hit goes** (`hitTarget`, pure). Choosing a row closes the sheet and
   navigates:

   | Hit | Goes to |
   |---|---|
   | Entry | `/app/campaigns/{cid}/wiki/{entryId}`, plus `?block={blockId}` for an article hit |
   | Note or image | `/app/campaigns/{cid}?note={noteId}` (14's note link). The filter is dropped, so the note is never hidden by one |
   | Session | `/app/campaigns/{cid}?session={number}` |

   Two new deep links follow `?note=`: each is consumed once and then dropped.
   - `?session=` opens the stream at that session's divider, walking older pages with
     `noteLinkProgress`, as `goToNote` does.
   - `?block=` scrolls to that block on the entry page (`id="block-{id}"`) and
     highlights it. A block the viewer cannot see is simply not there.

   No hit opens a note for **editing**: a note or image hit reads it in the stream. So
   ⌘K never touches the composer's edit mode (`useComposerEdit`, #226), and a draft or
   an edit in progress is left as it was.
5. **States.**
   - **Empty input.** Up to five **recent entries** under "Recent entries" (opened
     from ⌘K, kept as ids in `localStorage` under `ti:recentEntries:{campaignId}`, and
     read through the entry directory, so an entry now hidden or merged drops out). They
     are ordinary rows, so the arrows and Enter reach them. Under them, the hint "Search
     entries, notes, images and sessions. @ for entries, > for actions." 17c adds
     the default actions under the recents.
   - **Loading.** A thin bar under the input. The previous results stay, so the sheet
     never flashes empty.
   - **No results.** "Nothing found for "zanthor"."
   - **Error.** "Search failed." with Retry.
6. **Keyboard** (`moveCursor`, pure, and the combobox pattern).
   - The input is `role="combobox"` with `aria-controls` and `aria-activedescendant`.
     The results are a `role="listbox"`, and each section is a `role="group"` labelled
     by its header.
   - ↑ and ↓ move through rows, skipping headers and wrapping at the ends. Enter
     opens the row. The cursor returns to the first row whenever a new answer
     arrives; after "Show more" it stays where that row was, on the first new hit.
     Hovering moves the cursor. Esc closes the sheet (Reka's dialog), and ⌘K toggles
     it (13d).
   - **⌘K in the composer** is its `@` picker (#225). The editor takes the key first
     and prevents its default, and the layout's ⌘K listener ignores a key that is
     already handled, so ⌘K there opens the picker and not the sheet.
   - Closing puts focus back where it was: the 🔍 button, or whatever had it when ⌘K
     was pressed.
   - Tab is left alone in 17b. In 17c it cycles the kind on the Create row, as in the
     composer (§3).
7. **Mobile** (§3a, invariant 11).
   - Full screen, as since 13d, with safe-area insets.
   - Rows are at least 44 px. The input has `enterkeyhint="search"`, and the keyboard's
     Search key opens the highlighted row (the first by default).
   - The results list has the keyboard's height as bottom padding (`useKeyboardInset`),
     so no row is ever under the keyboard. A touch scroll in the results blurs the
     input, so the keyboard drops and more rows show.
   - 🔍 focuses the input within the tap: iOS only raises the keyboard for a focus made
     during a user gesture. The sheet's input mounts after the tap, so the tap first
     focuses a hidden input in the layout, which holds the keyboard up until the
     sheet's input takes the focus from it.
8. **Budgets (web).**
   - The sheet is open with the input focused within 100 ms of ⌘K.
   - Results show within 300 ms (p95) of the last keystroke against a local API on
     the large seed.
   - The sheet adds no dependency. It uses Reka and VueUse, which are already there.
9. **Tests** (`tests/unit/search.test.ts`):
   - `parseSearchInput`: prefixes and whitespace;
   - `searchRows`: order, headers, and "Show more";
   - `moveCursor`: wrapping, skipping headers, and an empty list;
   - snippet segments: ranges at the start, the end, next to each other, and past the
     end (clamped);
   - `hitTarget` for each kind;
   - recents: capped at five, most recent first, with hidden and merged entries dropped
     through the directory.

### 17c. Actions

1. **The registry** (`utils/searchActions.ts`, pure). Each action has an `id`, an
   `icon`, a `label(context)`, `keywords(context)`, `available(context)` and
   `run(context)`. `run` does nothing itself: it answers what to do (`navigate` to a
   target, `startSession` with a number, or `createEntry` with a name), and
   `SearchSheet` does it. The context holds the campaign, the viewer's role, the next
   session's number (current + 1 from `GET sessions`, or null until it loads), the
   scope and the query text, the entry directory, and the entry hit.

   | Action | Shown when | Does |
   |---|---|---|
   | ＋ Create entry "X" | the query has text that is not the exact name or alias (ignoring case) of an entry in the viewer's directory | 2 below |
   | ✎ Post a note about Gundren | an entry hit is in the results: the last one the cursor was on, else the top one | `/app/campaigns/{cid}?about={entryId}` (15c) |
   | ✎ New note mentioning "X" | the query has text | `?compose=@X` (4 below): the composer opens with `@X`, the caret at the end and the `@` picker showing matches or Create (§7's "New note mentioning 'gund'") |
   | ▶ Start Session N+1 | the sessions are loaded (any member may start one, §3) | 3 below |
   | → Go to Campaign / Wiki / Combat | always | the tab |
   | Wiki: Characters (…Places, Factions, Items, Events, Other) | always | `wiki?kind=` (15c) |
   | Show recaps / images / my notes | always | `?filter=recaps`, `images` or `mine` (14e) |

   - The first three are the query's own (`fromQuery`): they are built from the text,
     so they are not matched against it, and they are offered in the `all` and `@`
     scopes only.
   - **Matching.** The others' labels and keywords are matched with `matchRank` and
     `foldForMatch` (15d), best rank first and registry order among equals, so "start",
     "new session", "s14" and "session 14" all find ▶ Start Session 14.
   - **Placement.** Actions is always the last section (`actionRows`, under an
     ACTIONS header).
     - `all`, empty input: Start Session N+1 and the three Go to actions, under the
       recent entries. No Create row.
     - `all` with text: Create, Post a note about, New note mentioning, then the
       matched actions, **at most 4 rows** (§7's sketch). They show as soon as the
       text is typed, before the hits arrive, and the cursor moves to the first hit when
       they do.
     - `@`: only the query's own actions (at most 3). The other actions are not about
       entries.
     - `>`: every available action except the query's own, matched by the rest of the
       input, with no cap. None matching says "No actions match "x"."
   - **Choosing.** A navigating action closes the sheet and goes there. The two ✎
     actions hand the focus to the composer, so the sheet does not put it back on 🔍
     as it closes.
2. **Create entry** (`CreateEntryRow`).
   - The row shows the name, a kind chip and a visibility chip. The kind defaults to
     Character, and the visibility to `Everyone`, as in the composer; both reset each
     time the sheet opens. With the cursor on the row, Tab cycles the kind and
     Shift+Tab the visibility (`cycleEntryKind`, `cycleEntryVisibility`), and Enter
     creates. Desktop shows that as a hint under the row.
   - A tap or a click only opens the row: 15d's `KindChips`, three visibility chips
     and **Create** under it. Nothing is created by a single tap.
   - Enter pressed while the search is still loading does nothing on the Create row, so
     typing fast and pressing Enter never creates an entry that was not seen offered.
   - It sends 15a's `POST entries { name, kind, visibility }`. The mutation adds the
     entry to the directory cache, the entry goes into the recents, and its page opens.
   - 15a's 409 ("There is already an entry called …") names an entry the viewer can
     see. The row shows the message with "Open {name}" linking to that entry (when the
     directory has it). A hidden entry of the same name does not block creation, and
     15g's merge handles that later, so the action reveals nothing.
3. **Start Session N+1.** It sends 14's `POST sessions { number }` with the current
   number + 1 (1 in a campaign with none), closes the sheet, goes to the Campaign tab
   and toasts "Session 14 started". A 409 (another member was first) keeps the sheet
   open and toasts the server's message; the mutation refetches the sessions, so the
   row then offers the next number. The gap prompt (14c) is unchanged.
4. **`?compose=`** (on the Campaign page; `composeFromQuery`, `composeFits`). The
   composer consumes it once, as it does `about` (15c) and `share` (16e), and the page
   drops the parameter. With an **empty** composer (no text, no images, no note being
   edited) the text becomes the draft (at most 200 characters), and the TipTap editor
   (`ComposerEditor`) is focused at the end. The focus is a transaction with the caret
   right after `@X`, so TipTap's suggestion plugin opens the `@` picker on it, as if
   `@X` had been typed: the matches, or Create "X". Otherwise the value is dropped with
   a toast ("Your draft was kept." or "Finish editing the note first."). With no
   session yet the composer is only its call to action, so the text waits in the
   draft.
5. **Mobile.** Actions are ordinary rows of at least 44 px. The Create row's chips
   wrap, and the whole row stays above the keyboard (17b's padding). iOS raises the
   keyboard only for a focus made in the tap itself, so ✎ focuses the composer but the
   keyboard may stay down until the text box is tapped.
6. **Tests** (`tests/unit/searchActions.test.ts`):
   - which actions are available for each query, scope and state;
   - Create hidden on an exact name or alias match, and shown on a partial one;
   - the Start Session number, and none while the sessions are not loaded (hidden);
   - `>` scope listing and matching ("start", "new session", "s14", "session 14");
   - the ordering and the four-row cap;
   - the targets of Post a note about, New note mentioning, the tabs, kinds and
     filters;
   - the `?compose=` rule: an empty composer only, 200 characters;
   - kind and visibility cycling on the Create row.

## Verify

1. `dotnet test` and `pnpm build` pass, `vitest` passes, `schema.d.ts` is fresh, and
   CI is green on every PR in the stack. Testcontainers' `postgres:16-alpine` has both
   extensions, so no workflow changes.
2. `pnpm dev` on an existing dev database starts cleanly. The API creates `pg_trgm`,
   `unaccent` and the two indexes, and a second start changes nothing
   (`\dx` and `\di *search*` in `psql` show them).
3. `TI_PERF=1 dotnet test --filter SearchPerfTests` reports p50 ≤ 40 ms and p95 ≤
   100 ms on this Mac. Record the numbers in the Notes as built.
4. Three browser profiles at 390 × 844 and one at 1280 × 800: A (the owner, DM), with B
   and C joined as Players.
   1. B posts "We met @Gundren Rockseeker on the road". A posts a 🔒 DM note, "Gundren
      is working for zanthor". A adds a 🔒 DM block, "zanthor pays him", to Gundren's
      article.
   2. On desktop, A presses ⌘K and types `gund`. Entries shows Gundren Rockseeker ·
      Character · n mentions, and Notes shows both notes with "Gundren" highlighted.
      ↓ and Enter open the entry.
   3. C types `zanthor`: "Nothing found". C types `gundren`. Gundren is there, and
      Notes has B's note only. C's Gundren row has no snippet and a smaller mention
      count than A's.
   4. `gundrn` finds Gundren. `rockseeker` finds it through the name. After B adds the
      alias "Rockseeker", `rocks` shows "aka Rockseeker".
   5. On the phone profile, 🔍 opens the full-screen sheet with the keyboard up. The
      results scroll above the keyboard, and scrolling drops the keyboard. Tapping a
      note opens the stream at that note, highlighted.
   6. `12` (or `s1` in a new campaign) lists the session, and tapping it opens the
      stream at its divider. An image caption word shows the note under Images with
      its thumbnails.
   7. `>start` then Enter starts the next session for everyone. `>wiki` goes to the
      Wiki tab.
   8. `Glasstaff`: Create entry "Glasstaff". Tab to Place, then Enter: the entry page
      opens. ⌘K `Glasstaff` now lists it, and there is no Create row.
   9. `@glass` shows Entries only. ✎ Post a note about Glasstaff opens the composer
      with the mention. ✎ New note mentioning "Klarg" opens the composer with `@Klarg`
      and the Create suggestion.
   10. A hides B's note, and C searches `gundren` again: the note is gone at once. A
       unhides it, and it is back.
5. In Postgres, `EXPLAIN` of the note query on the large seed shows a bitmap index scan
   on `mt_doc_sessionnote_idx_search`.

## Notes / gotchas

- **Commit scopes:** `api`, `web` and `docs`. Every PR that changes an API contract
  regenerates `schema.d.ts` (`pnpm gen:api`) and commits it.
- **Why no stored search table** (the index design). §9 lists a `SearchDoc` read
  model. It is the row shape of the search query, over the inline projections, rather
  than a table of its own:
  - **Consistency for free.** Every source (`Entry`, `SessionNote`, `Session`) is an
    inline projection saved with its event. Expression indexes are updated in that
    same transaction. A search right after a hide, a narrowing or a secret-block edit
    already reflects it. `SearchConsistencyTests` checks each write.
  - **No copied visibility.** A search table would copy each unit's audience and have
    to follow every hide, visibility change, block edit and merge. 15 made the same
    choice for `MentionIndex`, and 16 for `Image`: reading the source's own fields
    cannot drift.
  - **Marten 7.31 has no inline fan-out.** Its `RaiseSideEffects` runs only for async
    projections in continuous mode (its XML docs say so), and async lag would let a
    just-hidden note be found for a moment (invariant 5). The other ways to keep
    per-block rows in step are hand-written writes in every endpoint that touches an
    entry, a note or a session, or triggers on `mt_doc_*` tables. Marten's schema
    management does not own such triggers, and a projection rebuild's `TRUNCATE`
    bypasses row triggers.
  - **Scale.** A campaign has thousands of rows, not millions. Long text (notes,
    articles) has GIN indexes. Names are scanned per campaign.

  If connections (19) or reference search (20, 21) ever need a stored table, it can be
  built from the same events. 15's Notes left the same door open for the mention
  index.
- **Why snippets cannot leak.** There are five layers, and each would be enough alone
  for the part it covers:
  1. **One audience per unit.** A snippet is cut from one note or one block. There is
     no "entry document" that mixes blocks, so there is no fragment boundary where
     secret text could slip in.
  2. **Filter before text work.** The visibility predicate is in the same `WHERE` as
     the match. `ts_rank_cd` and `ts_headline` only ever run on rows that passed.
  3. **The prefilter is a superset.** The article vector includes secret blocks, but a
     hit must be re-matched on a visible block, so a secret block can add no hit and no
     snippet. For notes and images the result set equals the one without the index: the
     index expression is `PlainText` of the note's text, exactly what the query matches
     on. For the article prefilter it does **not**: the vector holds each block's **raw**
     text, because `to_tsvector(regconfig, jsonb)` vectorises the document's string values
     and gives no hook to transform each one, while a block is matched on `PlainText` of
     it. Every lexeme of the raw text is therefore in the vector, except where `PlainText`
     **joins two raw lexemes into one** by taking a separator out of the middle of a word
     (`wo\rd` reads as `word`, and so do a deleted marker and a rewritten mention). A
     block whose only match is such a joined lexeme is missed. That is the known
     false-negative class: it cannot be closed on the index side, and it is never a leak,
     because the prefilter only ever removes candidates. Storing the vector in a column
     rather than computing it per row does not change it — it is the same expression.
  4. **Rank without the corpus.** `ts_rank_cd`, `similarity` and `word_similarity`
     score one row alone, and Postgres keeps no IDF. Mention counts are per viewer
     (`CountsForEntries`), and there are no totals, only `hasMore` over visible rows.
  5. **The C# re-check.** A drifted SQL fragment drops rows. It does not leak them, and
     it logs an error.
- **Timing.** The prefilter can do slightly more work when secret blocks match. That
  is microseconds on a campaign's scale, inside network jitter, and nobody outside the
  campaign can query it. It is noted and accepted.
- **`simple`, not `english`.** Fantasy names dominate the text. The English stemmer
  turns "Rockseeker" into `rockseek`, which breaks prefix-as-you-type, and removes
  stop words people do type ("The Triboar Trail"). `simple` lowercases and splits,
  and the last token is a prefix. Word forms ("fought" and "fight") do not match each
  other. That is accepted for 17.
- **Accents.** Names and aliases fold accents (`unaccent`), so `gundren` finds
  `Gündren` in Entries, as the `@` picker does. Note and block text are **not**
  folded: `unaccent` is not immutable, so it cannot be in an index expression without
  a wrapper function. That wrapper would bring back the ordering problem of 17a step
  3. If a table ever needs it, add an immutable wrapper created before the indexes.
- **No search operators.** Quotes, `OR` and `-` are plain text: every token must
  match. `websearch_to_tsquery` has no prefix matching, which as-you-type needs.
- **A mention's display text is searched, not the entry it links.** A note that says
  `@[the old dwarf](Gundren)` is found by "old dwarf", not by "Gundren". The entry's
  timeline lists it. Joining mentions into note search would need the mentioned
  entry's visibility as well, and is not in 17.
- **Expression indexes on Marten tables.** They are added to the document mapping
  (`DocumentMapping.Indexes`, with a Weasel `IndexDefinition` whose column is the
  expression), not through `Schema.For<T>().Index(x => …)`, which only takes members.
  Postgres rewrites expressions when it stores them (casts, spacing). If Weasel's
  diff sees churn on every start, write the constant in Postgres's canonical form
  (`pg_get_indexdef`). `SearchSchemaTests` catches it. The note index is one of these.
  The article vector is a **column** instead (17a.3), which has to go on the document
  table Marten builds from the mapping: Weasel drops a column it does not know about, so
  a column added beside Marten leaves the table permanently "Update" in the diff.
- **Production Postgres.** `pg_trgm` and `unaccent` are contrib modules shipped by every
  common host. They are trusted, so the app's database owner creates them. A host
  that refuses fails startup loudly on `CREATE EXTENSION`, not at the first search.
- **Seams for later steps.**
  - **Combat (18):** a `CombatSearchProvider` for a COMBATS section, and "⚔ Start
    combat" in the registry (DMs only, §8).
  - **Loose ends (19):** `EntryMatcher.MatchAsync` with a note's text split into
    spans suggests links for "a session note with no mentions". A "Loose ends (n)"
    action joins the registry.
  - **Reference (20, 21):** providers registered after the wiki's, a `Reference`
    hit kind, and "+ wiki" as a row action (§11).
  - **Suggestions (23):** the model's spans go through `EntryMatcher` before a new
    entry is proposed (§11a). They can go in one batch call.
- **Not in 17:** searching combats (18), member names, dates and edit history;
  saved searches; server-side recents; highlighting inside an opened note; search
  operators; a stored search table.

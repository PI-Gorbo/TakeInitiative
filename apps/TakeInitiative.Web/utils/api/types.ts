// Named aliases for the generated API types. `schema.d.ts` is generated from the
// API's OpenAPI document (`pnpm --filter @ti/api gen:openapi && pnpm --filter @ti/web gen:api`);
// never edit it by hand.
import type { components, operations } from "./schema";

export type Schemas = components["schemas"];

/** The JSON body an operation sends on success. */
export type ApiResponse<Op extends keyof operations> = operations[Op]["responses"] extends {
    200: { content: { "application/json": infer T } };
}
    ? T
    : void;

/** The JSON body an operation accepts. */
export type ApiRequestBody<Op extends keyof operations> = operations[Op] extends {
    requestBody: { content: { "application/json": infer T } };
}
    ? T
    : never;

/** An operation's route parameters. */
export type ApiPathParams<Op extends keyof operations> = operations[Op]["parameters"] extends {
    path: infer T;
}
    ? T
    : never;

export type Role = Schemas["Role"];
export type Campaign = Schemas["CampaignResponse"];
export type CampaignMember = Schemas["CampaignMemberResponse"];
export type CampaignSummary = Schemas["CampaignSummary"];
export type User = Schemas["GetUserResponse"];
export type MaintenanceConfig = Schemas["MaintenanceConfig"];
export type Visibility = Schemas["Visibility"];
export type Session = Schemas["SessionResponse"];
export type SessionNote = Schemas["SessionNoteResponse"];
export type SessionStream = Schemas["SessionStreamResponse"];
export type SessionStreamSession = Schemas["SessionStreamSession"];
export type SessionStreamFilter = Schemas["SessionStreamFilter"];
export type SessionList = Schemas["GetSessionsResponse"];
export type SessionNoteVersion = Schemas["SessionNoteVersion"];
export type EntryKind = Schemas["EntryKind"];
export type EditAccess = Schemas["EditAccess"];
export type Entry = Schemas["EntryResponse"];
export type EntrySummary = Schemas["EntrySummaryResponse"];
/** One row of the wiki list: an entry plus the viewer's own mention count (15b). */
export type EntryListItem = Schemas["EntryListItemResponse"];
export type EntryList = Schemas["GetEntriesResponse"];
export type TimelineItem = Schemas["EntryTimelineItem"];
export type EntryTimeline = Schemas["EntryTimelineResponse"];
/** An entry's article, as the viewer sees it: only the blocks they can see (15e). */
export type Article = Schemas["ArticleResponse"];
export type ArticleBlock = Schemas["ArticleBlockResponse"];
/** Where a quote came from: its session note (15e). */
export type Quote = Schemas["QuoteResponse"];
export type EntryQuote = Schemas["EntryQuoteResponse"];
/** An article that mentions an entry, on that entry's timeline (15e). */
export type ArticleMention = Schemas["EntryArticleMention"];
/** A Character's optional stat line (15g). */
export type Stats = Schemas["StatsResponse"];
/**
 * One link on an entry, as the viewer may read it (27b): a knowledge-base row resolved against the
 * corpus, or a url the member typed. `stale` means the row has gone from the knowledge base.
 */
export type EntryLink = Schemas["EntryLinkResponse"];
export type EntryLinkKind = Schemas["EntryLinkKind"];
/**
 * The knowledge-base rows an entry might be (28b), best first and at most three. A row is a
 * `KnowledgeBaseItem` — the same shape the Knowledge base page lists — so the prompt draws it with
 * 26f's own row helpers and "Link it" has everything `POST links` needs. There is no score on it: a
 * trigram match is not a probability, and the row's detail line is what lets the member judge.
 */
export type EntryKnowledgeBaseSuggestions = Schemas["EntryKnowledgeBaseSuggestionsResponse"];
/** A dismissed suggestion as history names it (28b): the row, resolved as the reader sees it now. */
export type EntrySuggestion = Schemas["EntrySuggestionResponse"];
/** An entry's history, redacted for the viewer (15g). */
export type EntryHistory = Schemas["EntryHistoryResponse"];
export type EntryHistoryItem = Schemas["EntryHistoryItem"];
export type EntryChange = Schemas["EntryChange"];
export type EntryChangeType = Schemas["EntryChangeType"];
/** An image on a session note (16b): its id and the display variant's size. */
export type NoteImage = Schemas["NoteImageResponse"];
/** An uploaded image, on no note yet (16a). */
export type UploadedImage = Schemas["ImageResponse"];
/** A page of a gallery (16d): image notes, oldest first, each with its session number. */
export type Gallery = Schemas["GalleryResponse"];
export type GalleryItem = Schemas["GalleryItem"];
/** ⌘K search (17a): sections of hits, each with an optional snippet. */
export type SearchResponse = Schemas["SearchResponse"];
export type SearchSection = Schemas["SearchSection"];
export type SearchSectionKey = Schemas["SearchSectionKey"];
export type SearchHit = Schemas["SearchHit"];
export type Snippet = Schemas["Snippet"];
/** Combat v2 (18a): one combat, redacted for the viewer, and its combatants. */
export type Combat = Schemas["CombatResponse"];
export type Combatant = Schemas["CombatantResponse"];
export type CombatStatus = Schemas["CombatStatus"];
export type CombatSummary = Schemas["CombatSummaryResponse"];
export type CombatList = Schemas["GetCombatsResponse"];
/** What players see of a combatant's HP: `Exact`, `Band` or `Nothing`. */
export type PlayersSee = Schemas["PlayersSee"];
export type HpBand = Schemas["HpBand"];
export type CombatCondition = Schemas["Condition"];
/** One combatant, or several copies of an entry, for `POST combatants`. */
export type CombatantRequest = Schemas["CombatantRequest"];
export type CombatHistory = Schemas["CombatHistoryResponse"];
export type CombatHistoryItem = Schemas["CombatHistoryItem"];
/** A combat drawn in its session in the stream (18e), and one line of it (`4× @Goblin`). */
export type CombatCard = Schemas["CombatCard"];
export type CombatCardCombatant = Schemas["CombatCardCombatant"];
/** An entry's COMBATS (18e.4): cards with their session numbers, newest first. */
export type EntryCombats = Schemas["EntryCombatsResponse"];
export type EntryCombat = Schemas["EntryCombat"];
/** Connections (19a): an entry's connections, and the evidence for one pair. */
export type EntryConnections = Schemas["EntryConnectionsResponse"];
export type EntryConnection = Schemas["EntryConnectionResponse"];
export type ConnectionEvidence = Schemas["ConnectionEvidenceResponse"];
export type Evidence = Schemas["EvidenceResponse"];
export type EvidenceKind = Schemas["EvidenceKind"];
export type BlockEvidence = Schemas["BlockEvidence"];
export type NoteEvidence = Schemas["NoteEvidence"];
export type CombatEvidence = Schemas["CombatEvidence"];
/** The graph (19a, 19d): nodes around an optional focus, and the edges among them. */
export type ConnectionGraph = Schemas["ConnectionGraphResponse"];
export type GraphNode = Schemas["GraphNodeResponse"];
export type GraphEdge = Schemas["GraphEdgeResponse"];
/** Loose ends (19b, 19e): the viewer's own to-dos, and their counts per session. */
export type LooseEnds = Schemas["LooseEndsResponse"];
export type LooseEnd = Schemas["LooseEndResponse"];
export type LooseEndKind = Schemas["LooseEndKind"];
export type LooseEndCounts = Schemas["LooseEndCountsResponse"];
export type LinkSuggestion = Schemas["LinkSuggestionResponse"];
/** Suggestions (23c, 23d): a model span matched against the wiki, and an accepted one's provenance on `PUT notes/{id}`. */
export type SuggestionMatch = Schemas["SuggestionMatchResponse"];
export type SuggestionSpanRequest = Schemas["SuggestionSpanRequest"];
export type SuggestionRequest = Schemas["SuggestionRequest"];
/** Accepted suggestions (23e): per model version, in the caller's own notes; revert's answer. */
export type SuggestionModelUsage = Schemas["SuggestionModelResponse"];
export type SuggestionRevertResult = Schemas["PostSuggestionRevertResponse"];
export type NewEntryRequest = Schemas["NewEntryRequest"];
/** Reference (20b, 20c): a ⌘K reference hit, and an item with its stat block and attribution. */
export type SearchReferenceHit = Schemas["SearchReferenceHit"];
export type ReferenceItem = Schemas["ReferenceItemResponse"];
export type ReferenceSummary = Schemas["ReferenceSummaryResponse"];
export type ReferenceAttribution = Schemas["ReferenceAttributionResponse"];
export type ReferenceCategory = Schemas["ReferenceCategory"];
/** The Knowledge base's browse list (26e, 26f): one page of the corpus and its facet counts. */
export type KnowledgeBase = Schemas["KnowledgeBaseResponse"];
export type KnowledgeBaseItem = Schemas["KnowledgeBaseItemResponse"];
export type KnowledgeBaseFacets = Schemas["KnowledgeBaseFacetsResponse"];
export type KnowledgeBaseCategoryFacet = Schemas["KnowledgeBaseCategoryFacetResponse"];
export type KnowledgeBaseBookFacet = Schemas["KnowledgeBaseBookFacetResponse"];
export type StatBlock = Schemas["StatBlock"];
/** An entry's source (20b, 20d): left out of the JSON when the viewer cannot read it. */
export type EntrySource = Schemas["EntrySourceResponse"];
export type StatBlockSpeed = Schemas["StatBlockSpeed"];
export type StatBlockTrait = Schemas["StatBlockTrait"];
export type StatBlockAction = Schemas["StatBlockAction"];

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
/** An entry's history, redacted for the viewer (15g). */
export type EntryHistory = Schemas["EntryHistoryResponse"];
export type EntryHistoryItem = Schemas["EntryHistoryItem"];
export type EntryChange = Schemas["EntryChange"];
export type EntryChangeType = Schemas["EntryChangeType"];

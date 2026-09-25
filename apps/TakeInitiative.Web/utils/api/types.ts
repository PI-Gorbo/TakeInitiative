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

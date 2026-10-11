import { describe, expect, it } from "vitest";
import type { Campaign, CampaignMember } from "~/utils/api/types";
import { NAME_MAX_LENGTH, canManageCampaign, currentMember } from "~/utils/campaign";

// ── Fixtures ─────────────────────────────────────────────────────────────────

const member = (extra: Partial<CampaignMember> = {}): CampaignMember => ({
    memberId: "m-owner",
    userId: "u-owner",
    username: "Sam",
    role: "DM",
    joinedAt: "2026-10-10T00:00:00+00:00",
    isOwner: true,
    ...extra,
});

const campaign = (extra: Partial<Campaign> = {}): Campaign => ({
    id: "c-1",
    name: "The Sunless Citadel",
    joinCode: "K7M2PQR4",
    ownerMemberId: "m-owner",
    createdAt: "2026-10-10T00:00:00+00:00",
    currentMemberId: "m-owner",
    members: [member()],
    ...extra,
});

// ── The caller's own member (13b) ────────────────────────────────────────────

describe("the caller's own member", () => {
    it("is the member matching currentMemberId", () => {
        const player = member({ memberId: "m-player", userId: "u-player", username: "Alex", role: "Player", isOwner: false });
        const found = currentMember(campaign({ currentMemberId: "m-player", members: [member(), player] }));
        expect(found).toEqual(player);
    });

    it("is undefined without a campaign", () => {
        expect(currentMember(null)).toBeUndefined();
        expect(currentMember(undefined)).toBeUndefined();
    });

    it("is undefined when the caller is not in the members list", () => {
        expect(currentMember(campaign({ currentMemberId: "m-missing" }))).toBeUndefined();
    });
});

// ── Who may manage the campaign (SAM-22, design §1) ──────────────────────────

describe("managing the campaign is a DM's", () => {
    it("lets the owner manage it", () => {
        expect(canManageCampaign(campaign())).toBe(true);
    });

    it("lets a DM who is not the owner manage it", () => {
        const dm = member({ memberId: "m-dm", userId: "u-dm", username: "Alex", role: "DM", isOwner: false });
        expect(canManageCampaign(campaign({ currentMemberId: "m-dm", members: [member(), dm] }))).toBe(true);
    });

    it("does not let a Player manage it", () => {
        const player = member({ memberId: "m-player", userId: "u-player", username: "Alex", role: "Player", isOwner: false });
        expect(canManageCampaign(campaign({ currentMemberId: "m-player", members: [member(), player] }))).toBe(false);
    });

    it("does not let a non-member or an unloaded campaign manage it", () => {
        expect(canManageCampaign(campaign({ currentMemberId: "m-missing" }))).toBe(false);
        expect(canManageCampaign(null)).toBe(false);
        expect(canManageCampaign(undefined)).toBe(false);
    });
});

// ── The name bound the input enforces ────────────────────────────────────────

describe("the campaign name length", () => {
    it("matches the API's Campaign.NameMaxLength", () => {
        expect(NAME_MAX_LENGTH).toBe(100);
    });
});

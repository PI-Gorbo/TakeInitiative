import { describe, expect, it } from "vitest";
import {
    accessPeekLabel,
    aliasPreview,
    combatsPeekLabel,
    galleryPeekLabel,
    initiativeModifier,
    statsPeekLabel,
    visibilityText,
} from "~/utils/entrySections";

// 25e's peeks: what a collapsed section says on one line, and the header's aliases.

const SAM = "sam";
const JO = "jo";

describe("aliasPreview", () => {
    it("shows the first three and counts the rest", () => {
        expect(aliasPreview(["Vex", "The Grey Fox", "Thorn", "Fox", "V"])).toEqual({
            label: 'aka "Vex", "The Grey Fox", "Thorn"',
            hidden: 2,
        });
    });

    it("shows everything when expanded or short", () => {
        expect(aliasPreview(["Vex", "Fox", "Thorn", "V"], true)).toEqual({
            label: 'aka "Vex", "Fox", "Thorn", "V"',
            hidden: 0,
        });
        expect(aliasPreview(["Vex"])).toEqual({ label: 'aka "Vex"', hidden: 0 });
        expect(aliasPreview([])).toEqual({ label: "", hidden: 0 });
    });
});

describe("statsPeekLabel", () => {
    it("reads AC, HP, then the initiative modifier", () => {
        expect(statsPeekLabel({ ac: 15, maxHp: "44", initiativeRoll: "1d20+3" })).toBe("AC 15 · HP 44 · Init +3");
        expect(statsPeekLabel({ maxHp: "2d8+2" })).toBe("HP 2d8+2");
        expect(statsPeekLabel({ ac: 0 })).toBe("AC 0");
    });

    it("is empty with no stats", () => {
        expect(statsPeekLabel(null)).toBe("");
        expect(statsPeekLabel(undefined)).toBe("");
        expect(statsPeekLabel({})).toBe("");
    });

    it("turns a plain d20 roll into its modifier and keeps anything else", () => {
        expect(initiativeModifier("1d20")).toBe("+0");
        expect(initiativeModifier("d20 - 1")).toBe("-1");
        expect(initiativeModifier("1D20+12")).toBe("+12");
        expect(initiativeModifier("2d20kh1+2")).toBe("2d20kh1+2");
        expect(initiativeModifier(" 14 ")).toBe("14");
    });
});

describe("accessPeekLabel and visibilityText", () => {
    const entry = (visibility: "Everyone" | "DM" | "Me", editAccess: "Anyone" | "OnlyMe") => ({
        visibility,
        editAccess,
        creatorMemberId: SAM,
    });

    it("says who can see and who can edit", () => {
        expect(accessPeekLabel(entry("Everyone", "Anyone"), JO, "Sam")).toBe("Everyone can see · Anyone can edit");
        expect(accessPeekLabel(entry("DM", "OnlyMe"), JO, "Sam")).toBe("🔒 DMs can see · Only Sam can edit");
        expect(accessPeekLabel(entry("Me", "OnlyMe"), JO, "Sam")).toBe("🔒 Only Sam can see · Only Sam can edit");
    });

    it("calls the creator 'you' when they are the viewer", () => {
        expect(accessPeekLabel(entry("Me", "OnlyMe"), SAM, "Sam")).toBe("🔒 Only you can see · Only you can edit");
        expect(visibilityText(entry("DM", "Anyone"), SAM, "Sam")).toBe("🔒 The DMs and you");
        expect(visibilityText(entry("DM", "Anyone"), JO, "Sam")).toBe("🔒 The DMs and Sam");
        expect(visibilityText(entry("Everyone", "Anyone"), JO, "Sam")).toBe("Everyone");
    });
});

describe("gallery and combats peeks", () => {
    it("counts, and is empty while loading", () => {
        expect(galleryPeekLabel(undefined)).toBe("");
        expect(galleryPeekLabel(0)).toBe("No images");
        expect(galleryPeekLabel(1)).toBe("1 image");
        expect(galleryPeekLabel(4)).toBe("4 images");
        expect(combatsPeekLabel(undefined)).toBe("");
        expect(combatsPeekLabel(0)).toBe("No combats");
        expect(combatsPeekLabel(1)).toBe("In 1 combat");
        expect(combatsPeekLabel(2)).toBe("In 2 combats");
    });
});

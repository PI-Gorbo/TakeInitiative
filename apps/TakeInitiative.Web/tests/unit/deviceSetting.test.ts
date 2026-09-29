import { describe, expect, it } from "vitest";
import {
    SUGGESTIONS_SETTING_KEY,
    decideLoad,
    isMetered,
    readSuggestionsSetting,
    writeSuggestionsSetting,
} from "~/utils/extraction/deviceSetting";

const storage = () => {
    const map = new Map<string, string>();
    return { getItem: (k: string) => map.get(k) ?? null, setItem: (k: string, v: string) => void map.set(k, v), removeItem: (k: string) => void map.delete(k) };
};

describe("the setting", () => {
    it("defaults to Ask and round-trips", () => {
        const s = storage();
        expect(readSuggestionsSetting(s)).toBe("ask");
        writeSuggestionsSetting(s, "off");
        expect(s.getItem(SUGGESTIONS_SETTING_KEY)).toBe("off");
        expect(readSuggestionsSetting(s)).toBe("off");
        s.setItem(SUGGESTIONS_SETTING_KEY, "nonsense");
        expect(readSuggestionsSetting(s)).toBe("ask");
        expect(readSuggestionsSetting(null)).toBe("ask");
    });
});

describe("isMetered", () => {
    it("without the Network Information API, a phone is metered and a desktop is not", () => {
        expect(isMetered(undefined, true)).toBe(true);
        expect(isMetered(undefined, false)).toBe(false);
    });

    it("with it: Save-Data, cellular or a slow connection", () => {
        expect(isMetered({ saveData: true, effectiveType: "4g" }, false)).toBe(true);
        expect(isMetered({ type: "cellular", effectiveType: "4g" }, false)).toBe(true);
        for (const t of ["slow-2g", "2g", "3g"]) expect(isMetered({ effectiveType: t }, false)).toBe(true);
        expect(isMetered({ type: "wifi", effectiveType: "4g" }, true)).toBe(false);
        expect(isMetered({}, true)).toBe(false);
    });
});

describe("decideLoad", () => {
    const cases: [Parameters<typeof decideLoad>[0], boolean, boolean, boolean, string][] = [
        // setting, cached, metered, consent → decision
        ["off", true, false, true, "off"],
        ["off", false, false, false, "off"],
        ["ask", false, false, false, "needsConsent"],
        ["ask", false, false, true, "load"],
        ["ask", true, false, false, "load"],
        ["ask", true, true, false, "needsConsent"],
        ["ask", false, true, true, "load"],
        ["automatic", false, false, false, "load"],
        ["automatic", true, false, false, "load"],
        ["automatic", false, true, false, "needsConsent"],
        ["automatic", true, true, false, "needsConsent"],
        ["automatic", false, true, true, "load"],
    ];
    it.each(cases)("%s, cached %s, metered %s, tap %s → %s", (setting, cached, metered, consent, expected) => {
        expect(decideLoad(setting, { cached, metered, consent })).toBe(expected);
    });

    it("never starts a first download on a metered connection without a tap, in any setting", () => {
        for (const setting of ["ask", "automatic"] as const) {
            expect(decideLoad(setting, { cached: false, metered: true, consent: false })).toBe("needsConsent");
        }
    });
});

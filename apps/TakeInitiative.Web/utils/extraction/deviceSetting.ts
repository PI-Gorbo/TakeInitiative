// Step 23b: "Suggestions on this device" (Off / Ask / Automatic), kept in localStorage
// (`ti.suggestions`) because it is about this device's data plan and storage, not the account.
// Pure: the storage and the connection are passed in.

import type { StorageLike } from "./modelCache";

export type SuggestionsSetting = "off" | "ask" | "automatic";
export const SUGGESTIONS_SETTING_KEY = "ti.suggestions";
export const DEFAULT_SUGGESTIONS_SETTING: SuggestionsSetting = "ask";
const SETTINGS: readonly SuggestionsSetting[] = ["off", "ask", "automatic"];

export function readSuggestionsSetting(storage: StorageLike | null): SuggestionsSetting {
    try {
        const v = storage?.getItem(SUGGESTIONS_SETTING_KEY);
        return SETTINGS.includes(v as SuggestionsSetting) ? (v as SuggestionsSetting) : DEFAULT_SUGGESTIONS_SETTING;
    } catch {
        return DEFAULT_SUGGESTIONS_SETTING;
    }
}

export function writeSuggestionsSetting(storage: StorageLike | null, value: SuggestionsSetting): void {
    try {
        storage?.setItem(SUGGESTIONS_SETTING_KEY, value);
    } catch {
        // Blocked storage: the setting lasts for this tab only.
    }
}

/** The parts of the Network Information API used (missing in Safari and Firefox). */
export interface ConnectionLike {
    saveData?: boolean;
    type?: string;
    effectiveType?: string;
}

/**
 * Metered: Save-Data, a cellular connection, or a slow one. Without the Network Information API
 * (Safari, so every iPhone) a phone counts as metered and a desktop does not.
 */
export function isMetered(connection: ConnectionLike | null | undefined, isMobile: boolean): boolean {
    if (!connection) return isMobile;
    if (connection.saveData) return true;
    if (connection.type === "cellular") return true;
    return ["slow-2g", "2g", "3g"].includes(connection.effectiveType ?? "");
}

/** What may happen now: nothing, wait for a tap, or load (from the cache or the network). */
export type LoadDecision = "off" | "needsConsent" | "load";

/**
 * Whether the model may load without a tap. `consent` is an explicit tap ("Download", "Find
 * suggestions" after the prompt). The first download on a metered connection always needs one.
 * - Off: never.
 * - Ask: the first download needs a tap; once cached it loads by itself, except when metered.
 * - Automatic: downloads and loads by itself, except when metered.
 */
export function decideLoad(setting: SuggestionsSetting, { cached, metered, consent }: { cached: boolean; metered: boolean; consent: boolean }): LoadDecision {
    if (setting === "off") return "off";
    if (consent) return "load";
    if (metered) return "needsConsent";
    if (setting === "automatic") return "load";
    return cached ? "load" : "needsConsent";
}

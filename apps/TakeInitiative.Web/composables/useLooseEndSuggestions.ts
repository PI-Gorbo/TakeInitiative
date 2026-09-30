// Step 23d: model suggestions for the loose-ends page. Loads the model only as the device
// setting allows (Off: nothing; Ask: a tap before the first download; Automatic: by itself,
// never on mobile data without a tap), extracts every listed note in the worker, newest first,
// then matches all their spans with one `POST suggestions/match` per 50. The rows read their
// suggestions through `provide`/`inject`. Nothing is written until the author taps.

import type { InjectionKey } from "vue";
import type { LooseEnd } from "~/utils/api/types";
import { isNoteLooseEnd } from "~/utils/looseEnds";
import { matchSuggestionSpans } from "~/utils/queries/suggestions";
import {
    dismissKey,
    isLinkableSpan,
    mergeSuggestions,
    readDismissed,
    toModelSuggestions,
    withDismissed,
    writeDismissed,
    type ModelSuggestion,
    type NoteSuggestions,
    type SuggestionModel,
} from "~/utils/suggestions";

export type LooseEndSuggestionsPhase =
    "idle" | "extracting" | "matching" | "done" | "error";

export interface LooseEndSuggestions {
    /** Whether ✨ shows at all on this device (the setting is not Off). */
    enabled: Readonly<Ref<boolean>>;
    /** Whether the model has looked at the listed notes (the rows' "· 2 suggestions"). */
    done: Readonly<Ref<boolean>>;
    model: SuggestionModel;
    /** One row's chips; empty until the model has looked. */
    forItem: (item: LooseEnd) => NoteSuggestions | null;
    /** ✕: hide a suggestion on this device. */
    dismiss: (s: ModelSuggestion) => void;
}

export const LOOSE_END_SUGGESTIONS: InjectionKey<LooseEndSuggestions> = Symbol(
    "looseEndSuggestions"
);

const storage = () =>
    typeof localStorage === "undefined" ? null : localStorage;

export function useLooseEndSuggestions(
    campaignId: Ref<string>,
    items: Ref<readonly LooseEnd[]>
) {
    const extractor = useExtractor();
    const api = useApi();
    const model: SuggestionModel = {
        name: extractor.model.value.name,
        version: extractor.model.value.version,
    };

    const phase = ref<LooseEndSuggestionsPhase>("idle");
    /** Whether the model has looked once: later looks (after a row resolves) keep the chips shown. */
    const looked = ref(false);
    const byNote = shallowRef(new Map<string, ModelSuggestion[]>());
    const dismissed = ref<string[]>([]);
    const dismissedSet = computed(() => new Set(dismissed.value));

    /** The notes the model reads: the listed note rows that have text (a captioned image counts). */
    const notes = computed(() =>
        items.value.flatMap((i) =>
            isNoteLooseEnd(i.kind) && i.note?.text
                ? [{ id: i.note.id, text: i.note.text }]
                : []
        )
    );
    const notesKey = computed(() =>
        notes.value.map((n) => `${n.id}:${n.text}`).join("\n")
    );

    const enabled = computed(() => extractor.state.value !== "off");

    let run = 0;
    async function look() {
        const token = ++run;
        const list = notes.value;
        if (list.length === 0) {
            byNote.value = new Map();
            phase.value = "done";
            return;
        }
        phase.value = "extracting";
        try {
            const found: {
                noteId: string;
                spans: Awaited<ReturnType<typeof extractor.extract>>;
            }[] = [];
            for (const note of list) {
                const spans = await extractor.extract(note.id, note.text);
                if (token !== run) return;
                found.push({
                    noteId: note.id,
                    spans: spans.filter((s) => isLinkableSpan(s.text)),
                });
            }
            phase.value = "matching";
            const all = found.flatMap((f) =>
                f.spans.map((s) => ({ text: s.text, kind: s.kind }))
            );
            const matches = await matchSuggestionSpans(
                api,
                campaignId.value,
                all
            );
            if (token !== run) return;
            const next = new Map<string, ModelSuggestion[]>();
            let at = 0;
            for (const f of found) {
                next.set(
                    f.noteId,
                    toModelSuggestions(
                        f.noteId,
                        f.spans,
                        matches.slice(at, at + f.spans.length)
                    )
                );
                at += f.spans.length;
            }
            byNote.value = next;
            looked.value = true;
            phase.value = "done";
        } catch {
            if (token === run) phase.value = "error";
        }
    }

    // Look again whenever the model becomes ready or the listed notes change (a row resolved).
    watch(
        [() => extractor.state.value, notesKey],
        ([state]) => {
            if (state === "ready") void look();
            else {
                run++;
                looked.value = false;
                phase.value = "idle";
            }
        },
        { immediate: true }
    );

    onMounted(() => {
        dismissed.value = readDismissed(storage());
        // Once there is a note to look at, load by itself only where the setting allows it
        // without a tap (23b's `decideLoad`: Ask with the model on this device, or Automatic,
        // and never a download on mobile data).
        let started = false;
        watch(
            () => notes.value.length > 0,
            (has) => {
                if (!has || started) return;
                started = true;
                if (extractor.state.value === "idle") void extractor.ensure();
            },
            { immediate: true }
        );
    });
    onBeforeUnmount(() => run++);

    const provided: LooseEndSuggestions = {
        enabled,
        done: computed(() => looked.value),
        model,
        forItem(item) {
            if (!enabled.value || !looked.value || !item.note) return null;
            const own = byNote.value.get(item.note.id) ?? [];
            return mergeSuggestions(item.suggestions, own, (s) =>
                dismissedSet.value.has(
                    dismissKey(s.noteId, s.text, model.version)
                )
            );
        },
        dismiss(s) {
            dismissed.value = withDismissed(
                dismissed.value,
                dismissKey(s.noteId, s.text, model.version)
            );
            writeDismissed(storage(), dismissed.value);
        },
    };
    provide(LOOSE_END_SUGGESTIONS, provided);

    /** The ✨ total over the listed rows, for the button's "✨ 5 suggestions". */
    const total = computed(() =>
        items.value.reduce(
            (sum, i) => sum + (provided.forItem(i)?.count ?? 0),
            0
        )
    );

    return {
        extractor,
        /** "done" once looked, also while looking again after a row resolved. */
        phase: computed<LooseEndSuggestionsPhase>(() =>
            phase.value === "error"
                ? "error"
                : looked.value
                  ? "done"
                  : phase.value
        ),
        noteCount: computed(() => notes.value.length),
        total,
        retry: () => void look(),
    };
}

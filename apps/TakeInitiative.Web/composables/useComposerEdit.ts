import type { SessionNote } from "~/utils/api/types";
import type { NoteEditTarget } from "~/utils/noteEdit";

/**
 * Which note the composer is editing (step 17), shared by the note cards, which start
 * an edit and show "Editing", and the composer, which runs it. One per campaign.
 *
 * - `start(note)` brings the note up in the composer. Starting another note while one
 *   is being edited switches to it, and the first one's unsaved changes are dropped:
 *   the card being edited is marked, so this is a deliberate tap, and a confirm here
 *   would only be in the way.
 * - `end()` goes back to the draft (Cancel, Save, or the composer going away).
 * - `locked` is set by the composer while a save is in flight, so the edit it is
 *   saving (and its new images) is not swapped out from under it.
 */
export function useComposerEdit(campaignId: string) {
    const target = useState<NoteEditTarget | null>(`composerEdit:${campaignId}`, () => null);
    const locked = useState<boolean>(`composerEditLocked:${campaignId}`, () => false);

    function start(note: SessionNote, sessionNumber: number | null = null) {
        if (locked.value || target.value?.note.id === note.id) return;
        target.value = { note, sessionNumber };
    }

    function end() {
        target.value = null;
    }

    const isEditing = (noteId: string) => target.value?.note.id === noteId;

    return { target: computed(() => target.value), locked, start, end, isEditing };
}

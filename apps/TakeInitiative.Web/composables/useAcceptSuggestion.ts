// Accepting a model suggestion that matched an entry (23d, 23e): one `PUT notes/{id}` by the
// author with the span turned into a mention and the model on the edit (23c). Shared by the
// loose-ends rows and the stream's suggestions sheet. Nothing happens without the author's tap.

import { toast } from "vue-sonner";
import type { SessionNote } from "~/utils/api/types";
import { apiErrorMessage } from "~/utils/apiErrorParser";
import { putNoteMutation } from "~/utils/queries/sessions";
import {
    acceptBody,
    type ModelSuggestion,
    type SuggestionModel,
} from "~/utils/suggestions";

export function useAcceptSuggestion(campaignId: MaybeRefOrGetter<string>) {
    const putNote = putNoteMutation();

    /** Links `s` to `entryId` in `note`. True once saved; a failure is toasted. */
    async function accept(
        note: Pick<SessionNote, "id" | "text" | "isRecap">,
        s: Pick<ModelSuggestion, "start" | "text" | "confidence">,
        entryId: string,
        model: SuggestionModel
    ): Promise<boolean> {
        if (putNote.isPending.value) return false;
        const body = acceptBody(note, s, entryId, model);
        if (!body) {
            toast.error(
                `“${s.text}” is no longer in the note. Edit it to link it.`
            );
            return false;
        }
        try {
            await putNote.mutateAsync({
                campaignId: toValue(campaignId),
                noteId: note.id,
                ...body,
            });
            return true;
        } catch (error) {
            toast.error(apiErrorMessage(error, "Could not link the note."));
            return false;
        }
    }

    return { accept, putNote, busy: computed(() => putNote.isPending.value) };
}

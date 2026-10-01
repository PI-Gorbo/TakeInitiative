<template>
    <!-- The D&D Beyond sheet field (27e, step 22a's half that matters): on a claimed Character,
         in Details, under the claim line. It is a **preset, not a kind** — what it creates is an
         ordinary external link labelled "D&D Beyond sheet", and nothing in the model or the API
         knows about D&D Beyond. The link itself renders in the Links section like any other, which
         on a phone is high up, under the stats line: the sheet is a high-frequency lookup
         (access pattern 2 in step 25).

         What goes in is **only an id**. Whatever is pasted — a sheet url with a slug, a profile
         url, a `ddb.ac` short link — is reduced to `https://www.dndbeyond.com/characters/{id}`,
         which is what is stored. Nothing is fetched: step 22's unofficial refresh stays post-MVP
         and off by default, so this is a link and hand-entered Stats.

         Only whoever may write the entry's links sees the field (the claimer and the DMs, 27d's
         `canAddLinks`); everyone else reads the link in Links. -->
    <div
        v-if="shown"
        class="flex flex-col gap-1.5">
        <div class="flex items-center gap-2">
            <span class="flex-1 text-muted-foreground">D&D Beyond sheet</span>
            <Button
                v-if="!editing"
                variant="ghost"
                size="sm"
                class="h-11 md:h-7"
                @click="start">
                {{ sheet ? "Edit" : "Add" }}
            </Button>
        </div>

        <p
            v-if="!editing"
            class="min-w-0 truncate text-sm">
            <a
                v-if="sheet && anchor"
                v-bind="anchor"
                class="text-gold hover:underline">
                {{ sheet.url }}
            </a>
            <span
                v-else
                class="text-muted-foreground">
                Not linked.
            </span>
        </p>

        <form
            v-else
            class="flex flex-col gap-2"
            @submit.prevent="save">
            <Input
                v-model="text"
                type="url"
                inputmode="url"
                autocomplete="off"
                autocapitalize="off"
                spellcheck="false"
                enterkeyhint="done"
                :maxlength="DND_BEYOND_INPUT_MAX"
                placeholder="https://www.dndbeyond.com/characters/12345678"
                :aria-invalid="!!error || undefined"
                :aria-describedby="`${id}-hint`"
                class="h-11 text-base md:h-9 md:text-sm" />
            <p
                v-if="error"
                class="text-xs text-destructive-tint"
                role="alert">
                {{ error }}
            </p>
            <p
                :id="`${id}-hint`"
                class="text-xs text-muted-foreground">
                Paste the address of the character's sheet. Everyone who can see {{ entry.name }}
                will see the link.
            </p>
            <div class="flex flex-wrap justify-end gap-2">
                <Button
                    v-if="sheet"
                    type="button"
                    variant="ghost"
                    class="h-11 text-destructive-tint md:h-8"
                    @click="confirmOpen = true">
                    Remove link
                </Button>
                <Button
                    type="button"
                    variant="ghost"
                    class="h-11 md:h-8"
                    @click="editing = false">
                    Cancel
                </Button>
                <Button
                    type="submit"
                    class="h-11 md:h-8"
                    :disabled="busy">
                    <LoaderCircle
                        v-if="busy"
                        class="animate-spin"
                        aria-hidden="true" />
                    Save
                </Button>
            </div>
        </form>

        <!-- Removing a link is an event on the entry's stream, so it confirms, exactly as the
             `⋯` menu's Remove does. -->
        <Dialog
            v-if="sheet"
            v-model:open="confirmOpen">
            <DialogContent class="max-w-sm">
                <DialogHeader>
                    <DialogTitle>Remove the D&D Beyond sheet?</DialogTitle>
                    <DialogDescription>
                        The link comes off {{ entry.name }}. It stays in the entry's history, and
                        you can paste it again.
                    </DialogDescription>
                </DialogHeader>
                <DialogFooter class="gap-2">
                    <Button
                        variant="ghost"
                        class="h-11 md:h-9"
                        @click="confirmOpen = false">
                        Cancel
                    </Button>
                    <Button
                        variant="destructive"
                        class="h-11 md:h-9"
                        :disabled="busy"
                        @click="removeSheet">
                        Remove
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    </div>
</template>

<script setup lang="ts">
    import { LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { Entry } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import { claimerOf, type EntryViewer } from "~/utils/entries";
    import {
        DND_BEYOND_INPUT_MAX,
        DND_BEYOND_LABEL,
        canAddLinks,
        dndBeyondLink,
        dndBeyondSheetUrl,
        dndBeyondUrlError,
        linkAnchor,
        pendingExternalLink,
    } from "~/utils/links";
    import { deleteEntryLinkMutation, postEntryLinkMutation } from "~/utils/queries/entries";

    const props = defineProps<{ campaignId: string; entry: Entry; viewer: EntryViewer }>();

    const id = useId();
    const shown = computed(
        () => props.entry.kind === "Character" && !!claimerOf(props.entry) && canAddLinks(props.entry, props.viewer)
    );
    const sheet = computed(() => dndBeyondLink(props.entry.links));
    const anchor = computed(() => (sheet.value ? linkAnchor(sheet.value) : null));

    const editing = ref(false);
    const text = ref("");
    const error = ref<string | null>(null);
    const confirmOpen = ref(false);

    function start() {
        text.value = sheet.value?.url ?? "";
        error.value = null;
        editing.value = true;
    }
    watch([shown, () => props.entry.id], () => {
        editing.value = false;
        confirmOpen.value = false;
    });

    const add = postEntryLinkMutation();
    const remove = deleteEntryLinkMutation();
    const busy = computed(() => add.isPending.value || remove.isPending.value);

    const fieldError = (error_: unknown, key: string): string | null =>
        (error_ as { response?: { data?: { errors?: Record<string, string[]> } } })?.response?.data?.errors?.[key]?.[0] ??
        null;

    /**
     * Replacing a sheet is **add then remove**, in that order: the plan's "editing a label is a
     * remove and an add" with the two steps the safe way round. If the add is refused — the entry
     * is at its 20-link cap, say — the old link is still there, which it would not be the other
     * way round. The same canonical url twice is a no-op rather than a 409 on itself.
     */
    async function save() {
        if (busy.value) return;
        error.value = dndBeyondUrlError(text.value);
        if (error.value) return;
        const canonical = dndBeyondSheetUrl(text.value);
        if (!canonical) return;
        const old = sheet.value;
        if (old && old.url === canonical) {
            editing.value = false;
            return;
        }
        try {
            await add.mutateAsync({
                campaignId: props.campaignId,
                entryId: props.entry.id,
                kind: "External",
                url: canonical,
                label: DND_BEYOND_LABEL,
                optimistic: pendingExternalLink(canonical, DND_BEYOND_LABEL, props.viewer.memberId),
            });
            if (old) await remove.mutateAsync({ campaignId: props.campaignId, entryId: props.entry.id, linkId: old.id });
            editing.value = false;
        } catch (failure) {
            error.value =
                fieldError(failure, "url") ??
                fieldError(failure, "links") ??
                apiErrorMessage(failure, "Could not save the sheet link.");
        }
    }

    async function removeSheet() {
        const link = sheet.value;
        if (!link || busy.value) return;
        confirmOpen.value = false;
        try {
            await remove.mutateAsync({ campaignId: props.campaignId, entryId: props.entry.id, linkId: link.id });
            editing.value = false;
        } catch (failure) {
            toast.error(apiErrorMessage(failure, "Could not remove the sheet link."));
        }
    }
</script>

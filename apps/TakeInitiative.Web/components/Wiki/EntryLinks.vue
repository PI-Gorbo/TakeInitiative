<template>
    <!-- An entry's Links (27d, glossary: Link). Two kinds in one list: a **knowledge base** row
         from step 26, resolved on every read so its name and detail come from the corpus, and an
         **external** url the member typed with their own label.

         Who reads them is `EntryLinks.CanRead`, which is `Source`'s rule and not the entry's
         visibility: on an unclaimed Character — an NPC — only the DMs do, because "The Eye ↗
         Beholder" gives the monster away exactly as its stat block would (invariant 8). The API
         redacts, so `links` simply arrives empty; `canReadLinks` is only how the section decides
         whether to be here at all.

         Where it sits (25's access patterns): on a claimed Character it is **high** on a phone,
         under the stats line, because checking your own character's sheet is access pattern 2.
         Everything else keeps it inside "More about X" ▸ Details. At `lg` it is a block in the
         sticky right-hand panel, under Details, forced open (25f) — `panel`.

         Remove **confirms**, because removing a link is an event on the entry's stream: it shows
         in the history with who and when, and there is no undo. -->
    <section
        v-if="readable && (links.length > 0 || canAdd)"
        :aria-labelledby="`${id}-title`"
        :class="panel ? 'flex flex-col' : 'flex flex-col gap-1'">
        <div
            class="flex items-center gap-2"
            :class="panel ? 'min-h-[52px] px-4' : ''">
            <h3
                :id="`${id}-title`"
                class="flex-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Links
            </h3>
            <button
                v-if="canAdd"
                type="button"
                aria-label="Add a link"
                class="-mr-2 flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:size-9"
                @click="addOpen = true">
                <Plus
                    class="size-4"
                    aria-hidden="true" />
            </button>
        </div>

        <ul
            v-if="links.length > 0"
            class="flex flex-col"
            :class="panel ? 'px-4 pb-4' : ''">
            <WikiEntryLinkRow
                v-for="link in links"
                :key="link.id"
                :link="link"
                :canWrite="writable"
                @remove="ask(link)" />
        </ul>
        <p
            v-else
            class="text-sm text-muted-foreground"
            :class="panel ? 'px-4 pb-4' : ''">
            No links yet.
        </p>

        <WikiAddLinkSheet
            v-if="canAdd"
            v-model:open="addOpen"
            :campaignId="campaignId"
            :entry="entry"
            :viewerMemberId="viewer.memberId" />

        <Dialog
            v-if="asked"
            v-model:open="confirmOpen">
            <DialogContent class="max-w-sm">
                <DialogHeader>
                    <DialogTitle>Remove this link?</DialogTitle>
                    <DialogDescription>
                        "{{ askedTitle }}" comes off {{ entry.name }}. It stays in the entry's
                        history, and you can add it again.
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
                        :disabled="remove.isPending.value"
                        @click="confirm">
                        <LoaderCircle
                            v-if="remove.isPending.value"
                            class="animate-spin"
                            aria-hidden="true" />
                        Remove
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    </section>
</template>

<script setup lang="ts">
    import { LoaderCircle, Plus } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { Entry, EntryLink } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { EntryViewer } from "~/utils/entries";
    import { canAddLinks, canReadLinks, canWriteLinks, linkTitle } from "~/utils/links";
    import { deleteEntryLinkMutation } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        viewer: EntryViewer;
        /** The desktop's right-hand panel (25f): a forced-open section with its own padding. */
        panel?: boolean;
    }>();

    const id = useId();
    const links = computed(() => props.entry.links);
    const readable = computed(() => canReadLinks(props.entry, props.viewer));
    const writable = computed(() => canWriteLinks(props.entry, props.viewer));
    const canAdd = computed(() => canAddLinks(props.entry, props.viewer));

    const addOpen = ref(false);
    watch(canAdd, (value) => {
        if (!value) addOpen.value = false;
    });

    // Remove's confirm. The link is held rather than its id, so the dialog can name it even
    // after the optimistic removal has taken the row off the list.
    const asked = ref<EntryLink | null>(null);
    const confirmOpen = ref(false);
    const askedTitle = computed(() => (asked.value ? linkTitle(asked.value) : ""));
    function ask(link: EntryLink) {
        asked.value = link;
        confirmOpen.value = true;
    }
    watch(
        () => props.entry.id,
        () => {
            confirmOpen.value = false;
            addOpen.value = false;
        }
    );

    const remove = deleteEntryLinkMutation();
    async function confirm() {
        const link = asked.value;
        if (!link || remove.isPending.value) return;
        confirmOpen.value = false;
        try {
            await remove.mutateAsync({ campaignId: props.campaignId, entryId: props.entry.id, linkId: link.id });
        } catch (error) {
            // The row is already back: the mutation rolls its optimistic removal back.
            toast.error(apiErrorMessage(error, "Could not remove the link."));
        }
    }
</script>

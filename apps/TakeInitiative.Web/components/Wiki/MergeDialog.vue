<template>
    <!-- Merge (15g, glossary: Merge, design §4): this entry folds into another. Its name
         becomes an alias, its mentions resolve to the other (no text is rewritten), its
         article is appended under "Merged from …", and its id redirects. There is no
         undo. A merge that would show the entry to someone who cannot see it now is
         refused: "Change visibility first". -->
    <Dialog v-model:open="open">
        <DialogContent
            class="max-h-[90dvh] max-w-md overflow-y-auto max-md:top-4 max-md:translate-y-0"
            :style="phone ? { maxHeight: `calc(100dvh - ${inset}px - 2rem)` } : undefined">
            <DialogHeader>
                <DialogTitle>Merge {{ entry.name }}</DialogTitle>
                <DialogDescription> Fold {{ entry.name }} into another entry. This can't be undone. </DialogDescription>
            </DialogHeader>

            <form
                class="flex flex-col gap-3"
                @submit.prevent="merge">
                <WikiEntryPicker
                    v-model="target"
                    :campaignId="campaignId"
                    :viewer="viewer"
                    :excludeId="entry.id"
                    noCreate />

                <template v-if="into">
                    <ul class="flex flex-col gap-1 rounded-md bg-muted px-3 py-2 text-sm">
                        <li>
                            "{{ entry.name }}"<template v-if="entry.aliases.length > 0">
                                and {{ entry.aliases.length === 1 ? "its alias" : `its ${entry.aliases.length} aliases` }}</template
                            >
                            become aliases of {{ into.name }}.
                        </li>
                        <li>
                            Its article is added to {{ into.name }}'s under "Merged from {{ entry.name }}"<template
                                v-if="entry.article.blocks.length === 0">
                                (it has nothing you can see)</template
                            >. Secret blocks stay secret.
                        </li>
                        <li>Notes that mention {{ entry.name }} show on {{ into.name }}'s timeline. Their text stays.</li>
                        <li v-if="entry.claimedByMemberId">
                            It stays {{ nameOf(entry.claimedByMemberId) }}'s player character.
                        </li>
                    </ul>
                    <p
                        v-if="problem"
                        role="alert"
                        class="rounded-md border border-destructive/50 px-3 py-2 text-sm text-destructive-tint">
                        {{ problem }}
                    </p>
                    <p
                        v-else-if="losers.length > 0"
                        class="rounded-md bg-gold/10 px-3 py-2 text-sm">
                        🔒 {{ losersLabel }} will no longer see {{ entry.name }}.
                    </p>
                </template>

                <DialogFooter class="gap-2">
                    <Button
                        type="button"
                        variant="ghost"
                        class="h-11 md:h-9"
                        @click="open = false">
                        Cancel
                    </Button>
                    <Button
                        type="submit"
                        class="h-11 md:h-9"
                        :disabled="!into || !!problem || busy">
                        <LoaderCircle
                            v-if="busy"
                            class="animate-spin"
                            aria-hidden="true" />
                        Merge
                    </Button>
                </DialogFooter>
            </form>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { useMediaQuery } from "@vueuse/core";
    import { LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { CampaignMember, Entry } from "~/utils/api/types";
    import { entryHref } from "~/utils/article";
    import { mergeLosers, mergeProblem, type EntryViewer } from "~/utils/entries";
    import type { PromoteTarget } from "~/utils/promote";
    import { mergeEntryMutation } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        /** The entry being merged: the one that goes away. */
        entry: Entry;
        viewer: EntryViewer;
        members: readonly CampaignMember[];
        nameOf: (memberId: string) => string;
    }>();
    const open = defineModel<boolean>("open", { required: true });

    const phone = useMediaQuery("(max-width: 767.98px)");
    const inset = useKeyboardInset();

    const target = ref<PromoteTarget | null>(null);
    watch(open, (value) => {
        if (value) target.value = null;
    });
    const into = computed(() => (target.value?.kind === "entry" ? target.value.entry : null));

    const problem = computed(() => (into.value ? mergeProblem(props.entry, into.value, props.members) : null));
    const losers = computed(() => (into.value ? mergeLosers(props.entry, into.value, props.members) : []));
    const losersLabel = computed(() =>
        losers.value.every((m) => m.role === "Player") && losers.value.length > 1
            ? "Players"
            : losers.value.map((m) => (m.memberId === props.viewer.memberId ? "You" : m.username)).join(", ")
    );

    const mutation = mergeEntryMutation();
    const busy = computed(() => mutation.isPending.value);

    async function merge() {
        if (!into.value || problem.value || busy.value) return;
        try {
            const merged = await mutation.mutateAsync({
                campaignId: props.campaignId,
                entryId: props.entry.id,
                intoEntryId: into.value.id,
            });
            open.value = false;
            toast.success(`Merged ${props.entry.name} into ${merged.name}.`);
            await navigateTo(entryHref(props.campaignId, merged.id), { replace: true });
        } catch (error) {
            toast.error(apiErrorMessage(error, "Could not merge the entries."));
        }
    }
</script>

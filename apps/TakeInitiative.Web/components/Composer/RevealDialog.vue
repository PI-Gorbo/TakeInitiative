<template>
    <!-- The reveal warning (15d, design §3, invariant 5): a note mentions entries that
         some of its readers cannot see. Nothing changes visibility without the tap on
         "Reveal and post". The caller awaits `confirm`: true means post. -->
    <Dialog
        :open="open"
        @update:open="(value) => !value && settle(false)">
        <DialogContent class="max-w-md">
            <DialogHeader>
                <DialogTitle>{{ title }}</DialogTitle>
                <DialogDescription>
                    Readers who cannot see {{ items.length === 1 ? "it" : "them" }} get the text without a link.
                </DialogDescription>
            </DialogHeader>
            <p
                v-if="locked.length > 0 && revealable.length > 0"
                class="text-sm text-muted-foreground">
                Only its creator or a DM can reveal {{ locked.map((i) => i.entry.name).join(", ") }}. It stays hidden.
            </p>
            <DialogFooter class="gap-2">
                <Button
                    variant="ghost"
                    class="h-11 md:h-9"
                    :disabled="revealing"
                    @click="settle(false)">
                    Cancel
                </Button>
                <Button
                    variant="outline"
                    class="h-11 md:h-9"
                    :disabled="revealing"
                    @click="settle(true)">
                    Post without revealing
                </Button>
                <Button
                    v-if="revealable.length > 0"
                    class="h-11 md:h-9"
                    :disabled="revealing"
                    @click="reveal">
                    <LoaderCircle
                        v-if="revealing"
                        class="animate-spin"
                        aria-hidden="true" />
                    Reveal and post
                </Button>
            </DialogFooter>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { Visibility } from "~/utils/api/types";
    import { revealMessage, type RevealItem } from "~/utils/mentions";
    import { putEntryVisibilityMutation } from "~/utils/queries/entries";

    const props = defineProps<{ campaignId: string }>();

    const open = ref(false);
    const items = ref<RevealItem[]>([]);
    const noteVisibility = ref<Visibility>("Everyone");
    const revealable = computed(() => items.value.filter((i) => i.canReveal));
    const locked = computed(() => items.value.filter((i) => !i.canReveal));
    const title = computed(
        () => `${revealMessage(items.value, noteVisibility.value)} Reveal ${items.value.length === 1 ? "it" : "them"}?`
    );

    let resolve: ((post: boolean) => void) | undefined;
    function settle(post: boolean) {
        open.value = false;
        resolve?.(post);
        resolve = undefined;
    }

    /**
     * Asks before a post or a save whose note mentions entries hidden from some of its
     * readers (`revealCheck`). Resolves true to post (after revealing, when chosen),
     * false to cancel. With nothing to warn about it resolves true at once.
     */
    function confirm(visibility: Visibility, warn: RevealItem[]): Promise<boolean> {
        if (warn.length === 0) return Promise.resolve(true);
        resolve?.(false);
        items.value = warn;
        noteVisibility.value = visibility;
        open.value = true;
        return new Promise((r) => (resolve = r));
    }

    const putVisibility = putEntryVisibilityMutation();
    const revealing = ref(false);
    async function reveal() {
        revealing.value = true;
        try {
            for (const { entry } of revealable.value) {
                await putVisibility.mutateAsync({
                    campaignId: props.campaignId,
                    entryId: entry.id,
                    visibility: noteVisibility.value,
                });
            }
            settle(true);
        } catch (error) {
            toast.error(apiErrorMessage(error, "Could not reveal the entry. Nothing was posted."));
            settle(false);
        } finally {
            revealing.value = false;
        }
    }

    onBeforeUnmount(() => resolve?.(false));
    defineExpose({ confirm });
</script>

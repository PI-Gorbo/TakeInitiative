<template>
    <!-- + Wiki (20d, glossary "+ Wiki"): an entry from a reference item, with its Source and,
         for a DM, its Stats in the same save. The name is editable ("Goblin Warrior" →
         "Goblin"); the kind is the item's and only shown. A name that is taken shows the
         entry that has it and never overwrites it. -->
    <Dialog v-model:open="open">
        <!-- On a phone it sits at the top and stops above the keyboard, like Promote. -->
        <DialogContent
            class="max-h-[90dvh] max-w-md grid-cols-[minmax(0,1fr)] overflow-y-auto max-md:top-4 max-md:translate-y-0 [&>*]:min-w-0"
            :style="phone ? { maxHeight: `calc(100dvh - ${inset}px - 2rem)` } : undefined">
            <DialogHeader>
                <DialogTitle>Add to the wiki</DialogTitle>
                <DialogDescription>
                    A new entry from {{ item?.providerLabel ?? "the reference" }}, with an empty article.
                </DialogDescription>
            </DialogHeader>
            <form
                v-if="item"
                class="flex min-w-0 flex-col gap-4"
                @submit.prevent="submit">
                <div class="flex flex-col gap-1.5">
                    <Label :for="`${id}-name`">Name</Label>
                    <Input
                        :id="`${id}-name`"
                        v-model="name"
                        maxlength="100"
                        autocomplete="off"
                        enterkeyhint="done"
                        class="h-11 text-base md:h-9 md:text-sm"
                        :aria-invalid="!!conflictText || undefined"
                        :aria-describedby="conflictText ? `${id}-conflict` : undefined" />
                    <p
                        v-if="conflictText"
                        :id="`${id}-conflict`"
                        class="text-sm text-destructive-tint"
                        role="alert">
                        {{ conflictText }}
                        <NuxtLink
                            v-if="conflict?.existingId"
                            :to="entryHref(campaignId, conflict.existingId)"
                            class="inline-flex min-h-11 items-center font-medium text-gold underline underline-offset-2 md:min-h-0"
                            @click="open = false">
                            Open it
                        </NuxtLink>
                        <span class="block text-muted-foreground">Or rename it to add another.</span>
                    </p>
                </div>
                <div class="flex flex-col gap-1.5">
                    <span class="text-sm font-medium">Kind</span>
                    <span class="text-sm">
                        <span aria-hidden="true">{{ ENTRY_KIND_ICONS[item.suggestedKind] }}</span>
                        {{ item.suggestedKind }}
                    </span>
                </div>
                <div class="flex flex-col gap-1.5">
                    <span class="text-sm font-medium">Visible to</span>
                    <WikiChoiceChips
                        v-model="visibility"
                        label="Visible to"
                        :options="ENTRY_VISIBILITY_OPTIONS" />
                </div>
                <p
                    v-if="statsText"
                    class="rounded-md bg-muted px-3 py-2 text-sm">
                    {{ statsText }}
                </p>
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
                        :disabled="!name.trim() || !!conflictText || create.isPending.value">
                        <LoaderCircle
                            v-if="create.isPending.value"
                            class="animate-spin"
                            aria-hidden="true" />
                        Add
                    </Button>
                </DialogFooter>
            </form>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { useMediaQuery } from "@vueuse/core";
    import { LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { Visibility } from "~/utils/api/types";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import { entryHref } from "~/utils/article";
    import { currentMember } from "~/utils/campaign";
    import { ENTRY_KIND_ICONS, ENTRY_VISIBILITY_OPTIONS, existingEntryIdFrom } from "~/utils/entries";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { createEntryFromReferenceMutation } from "~/utils/queries/entries";
    import { getReferenceItemQuery } from "~/utils/queries/reference";
    import {
        addToWikiConflict,
        addToWikiStatsLine,
        conflictMessage,
        defaultAddVisibility,
        type AddToWikiConflict,
        type AddToWikiItem,
    } from "~/utils/reference";

    const props = defineProps<{ campaignId: string; item: AddToWikiItem | null }>();
    const open = defineModel<boolean>("open", { required: true });

    const id = useId();
    const phone = useMediaQuery("(max-width: 767.98px)");
    const inset = useKeyboardInset();

    const campaignQuery = useQuery(getCampaignQuery(() => props.campaignId));
    const isDm = computed(() => currentMember(campaignQuery.data.value)?.role === "DM");

    const name = ref("");
    const visibility = ref<Visibility>("Everyone");
    const conflict = ref<AddToWikiConflict | null>(null);
    const conflictText = computed(() => conflictMessage(conflict.value, name.value));

    // Each opening starts from the item and the viewer's role.
    watch(
        [open, () => props.item],
        ([isOpen]) => {
            if (!isOpen) return;
            name.value = props.item?.name ?? "";
            visibility.value = defaultAddVisibility(isDm.value);
            conflict.value = null;
        },
        { immediate: true }
    );
    // The role can arrive after the dialog opened (a cold ⌘K); the default follows it
    // until the viewer picks.
    let picked = false;
    watch(open, () => (picked = false));
    watch(visibility, (value) => {
        if (value !== defaultAddVisibility(isDm.value)) picked = true;
    });
    watch(isDm, (dm) => {
        if (open.value && !picked) visibility.value = defaultAddVisibility(dm);
    });

    // A DM sees the Stats the entry will get. A ⌘K hit has none, so the card's item is read
    // (it is cached for good once read).
    const needsItem = computed(
        () => open.value && isDm.value && !!props.item?.hasStatBlock && props.item.stats === undefined
    );
    const itemQuery = useQuery(
        getReferenceItemQuery(
            () => (needsItem.value ? (props.item?.provider ?? "") : ""),
            () => (needsItem.value ? (props.item?.id ?? "") : "")
        )
    );
    const stats = computed(() =>
        props.item?.stats !== undefined ? props.item.stats : itemQuery.data.value?.summary.stats
    );
    const statsText = computed(() =>
        props.item ? addToWikiStatsLine(props.item.suggestedKind, stats.value, isDm.value) : null
    );

    const create = createEntryFromReferenceMutation();
    async function submit() {
        const item = props.item;
        const wanted = name.value.trim();
        if (!item || !wanted || conflictText.value || create.isPending.value) return;
        try {
            const entry = await create.mutateAsync({
                campaignId: props.campaignId,
                provider: item.provider,
                itemId: item.id,
                name: wanted,
                visibility: visibility.value,
            });
            open.value = false;
            toast.success(`${entry.name} added to the wiki`, {
                duration: 6000,
                action: {
                    label: "Open",
                    onClick: () => void navigateTo(entryHref(props.campaignId, entry.id)),
                },
            });
        } catch (error) {
            const status = apiErrorStatus(error);
            conflict.value = addToWikiConflict(status, wanted, status === 409 ? existingEntryIdFrom(error) : null);
            if (!conflict.value) {
                toast.error(
                    status === 404
                        ? "That isn't in the reference any more."
                        : apiErrorMessage(error, "Could not add it to the wiki.")
                );
            }
        }
    }
</script>

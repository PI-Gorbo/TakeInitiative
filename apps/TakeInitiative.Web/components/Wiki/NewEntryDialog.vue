<template>
    <!-- "New entry" on the wiki home (15c): name, kind and visibility. The caller is
         its creator. -->
    <Dialog v-model:open="open">
        <DialogContent class="max-h-[90dvh] max-w-md overflow-y-auto">
            <DialogHeader>
                <DialogTitle>New entry</DialogTitle>
                <DialogDescription>A character, place, faction, item or event in the wiki.</DialogDescription>
            </DialogHeader>
            <form
                class="flex flex-col gap-4"
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
                        :aria-invalid="!!error || undefined"
                        :aria-describedby="error ? `${id}-error` : undefined" />
                    <p
                        v-if="error"
                        :id="`${id}-error`"
                        class="text-sm text-destructive-tint">
                        {{ error }}
                        <NuxtLink
                            v-if="existingId"
                            :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki/${encodeURIComponent(existingId)}`"
                            class="font-medium text-gold underline underline-offset-2"
                            @click="open = false">
                            Open it
                        </NuxtLink>
                    </p>
                </div>
                <div class="flex flex-col gap-1.5">
                    <span class="text-sm font-medium">Kind</span>
                    <WikiChoiceChips
                        v-model="kind"
                        label="Kind"
                        :options="ENTRY_KINDS" />
                </div>
                <div class="flex flex-col gap-1.5">
                    <span class="text-sm font-medium">Visible to</span>
                    <WikiChoiceChips
                        v-model="visibility"
                        label="Visible to"
                        :options="ENTRY_VISIBILITY_OPTIONS" />
                </div>
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
                        :disabled="!name.trim() || create.isPending.value">
                        Create
                    </Button>
                </DialogFooter>
            </form>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import type { EntryKind, Visibility } from "~/utils/api/types";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import { ENTRY_KINDS, ENTRY_VISIBILITY_OPTIONS, existingEntryIdFrom } from "~/utils/entries";
    import { createEntryMutation } from "~/utils/queries/entries";

    const props = defineProps<{ campaignId: string; initialName?: string }>();
    const open = defineModel<boolean>("open", { required: true });

    const id = useId();
    const name = ref("");
    const kind = ref<EntryKind>("Character");
    const visibility = ref<Visibility>("Everyone");
    const error = ref<string | null>(null);
    const existingId = ref<string | null>(null);

    watch(
        open,
        (isOpen) => {
            if (!isOpen) return;
            name.value = props.initialName?.trim() ?? "";
            kind.value = "Character";
            visibility.value = "Everyone";
            error.value = null;
            existingId.value = null;
        },
        { immediate: true }
    );
    watch(name, () => {
        error.value = null;
        existingId.value = null;
    });

    const create = createEntryMutation();
    async function submit() {
        if (!name.value.trim()) return;
        try {
            const entry = await create.mutateAsync({
                campaignId: props.campaignId,
                name: name.value.trim(),
                kind: kind.value,
                visibility: visibility.value,
            });
            open.value = false;
            await navigateTo(`/app/campaigns/${encodeURIComponent(props.campaignId)}/wiki/${encodeURIComponent(entry.id)}`);
        } catch (err) {
            existingId.value = apiErrorStatus(err) === 409 ? existingEntryIdFrom(err) : null;
            error.value = apiErrorMessage(err, "Could not create the entry.");
        }
    }
</script>

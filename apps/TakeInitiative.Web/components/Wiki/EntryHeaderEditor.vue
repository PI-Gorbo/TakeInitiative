<template>
    <!-- Editing an entry's header (15c). Each field shows only to those who can change
         it: name, kind and aliases follow edit access; visibility and edit access are
         the creator's and the DMs'. Save sends only what changed. -->
    <form
        class="flex flex-col gap-4 rounded-md border p-3 md:p-4"
        aria-label="Edit entry"
        @submit.prevent="save">
        <template v-if="canEdit">
            <div class="flex flex-col gap-1.5">
                <Label :for="`${id}-name`">Name</Label>
                <Input
                    :id="`${id}-name`"
                    v-model="name"
                    maxlength="100"
                    autocomplete="off"
                    class="h-11 text-base md:h-9 md:text-sm" />
            </div>
            <div class="flex flex-col gap-1.5">
                <span class="text-sm font-medium">Kind</span>
                <WikiChoiceChips
                    v-model="kind"
                    label="Kind"
                    :options="ENTRY_KINDS" />
            </div>
            <div class="flex flex-col gap-1.5">
                <Label :for="`${id}-alias`">Aliases</Label>
                <WikiAliasEditor
                    v-model="aliases"
                    :name="name"
                    :inputId="`${id}-alias`" />
            </div>
        </template>
        <template v-if="canChangeAccess">
            <div class="flex flex-col gap-1.5">
                <span class="text-sm font-medium">Visible to</span>
                <WikiChoiceChips
                    v-model="visibility"
                    label="Visible to"
                    :options="ENTRY_VISIBILITY_OPTIONS" />
                <p
                    v-if="visibility !== entry.visibility && narrows"
                    class="text-xs text-muted-foreground">
                    Members outside the new visibility lose the entry, and its mentions become plain text for them.
                </p>
            </div>
            <div class="flex flex-col gap-1.5">
                <span class="text-sm font-medium">Edit access</span>
                <WikiChoiceChips
                    v-model="editAccess"
                    label="Edit access"
                    :options="EDIT_ACCESS_OPTIONS" />
            </div>
        </template>
        <div class="flex justify-end gap-2">
            <Button
                type="button"
                variant="ghost"
                class="h-11 md:h-9"
                @click="emit('done')">
                Cancel
            </Button>
            <Button
                type="submit"
                class="h-11 md:h-9"
                :disabled="saving || (canEdit && !name.trim())">
                Save
            </Button>
        </div>
    </form>
</template>

<script setup lang="ts">
    import { toast } from "vue-sonner";
    import type { EditAccess, Entry, EntryKind, Visibility } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import { EDIT_ACCESS_OPTIONS, ENTRY_KINDS, ENTRY_VISIBILITY_OPTIONS } from "~/utils/entries";
    import {
        putEntryAliasesMutation,
        putEntryEditAccessMutation,
        putEntryKindMutation,
        putEntryNameMutation,
        putEntryVisibilityMutation,
    } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        canEdit: boolean;
        canChangeAccess: boolean;
    }>();
    const emit = defineEmits<{ done: [] }>();

    const id = useId();
    const name = ref(props.entry.name);
    const kind = ref<EntryKind>(props.entry.kind);
    const aliases = ref<string[]>([...props.entry.aliases]);
    const visibility = ref<Visibility>(props.entry.visibility);
    const editAccess = ref<EditAccess>(props.entry.editAccess);

    const REACH: Record<Visibility, number> = { Everyone: 2, DM: 1, Me: 0 };
    const narrows = computed(() => REACH[visibility.value] < REACH[props.entry.visibility]);

    const putName = putEntryNameMutation();
    const putKind = putEntryKindMutation();
    const putAliases = putEntryAliasesMutation();
    const putVisibility = putEntryVisibilityMutation();
    const putEditAccess = putEntryEditAccessMutation();
    const saving = ref(false);

    const sameList = (a: readonly string[], b: readonly string[]) => a.length === b.length && a.every((x, i) => x === b[i]);

    async function save() {
        const ids = { campaignId: props.campaignId, entryId: props.entry.id };
        saving.value = true;
        try {
            // One request per changed field, in this order: the name first, so an
            // alias equal to the new name is dropped by the server as it is saved.
            if (props.canEdit) {
                if (name.value.trim() !== props.entry.name) await putName.mutateAsync({ ...ids, name: name.value.trim() });
                if (kind.value !== props.entry.kind) await putKind.mutateAsync({ ...ids, kind: kind.value });
                if (!sameList(aliases.value, props.entry.aliases))
                    await putAliases.mutateAsync({ ...ids, aliases: aliases.value });
            }
            if (props.canChangeAccess) {
                if (editAccess.value !== props.entry.editAccess)
                    await putEditAccess.mutateAsync({ ...ids, editAccess: editAccess.value });
                if (visibility.value !== props.entry.visibility)
                    await putVisibility.mutateAsync({ ...ids, visibility: visibility.value });
            }
            emit("done");
        } catch (error) {
            toast.error(apiErrorMessage(error, "Could not save the entry."));
        } finally {
            saving.value = false;
        }
    }
</script>

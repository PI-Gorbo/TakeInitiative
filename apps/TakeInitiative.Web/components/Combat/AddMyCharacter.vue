<template>
    <!-- Add my character (18c.4): a player's claimed Character entries that are not in
         the combat yet. One is a button; several are a menu. They wait until rolled. -->
    <Button
        v-if="candidates.length === 1"
        variant="outline"
        class="h-11 gap-2 md:h-9"
        :disabled="add.isPending.value"
        @click="addCharacter(candidates[0]!)">
        <UserPlus
            class="size-4"
            aria-hidden="true" />
        Add {{ candidates[0]!.name }}
    </Button>
    <DropdownMenu v-else-if="candidates.length > 1">
        <DropdownMenuTrigger
            class="flex h-11 items-center gap-2 rounded-md border px-3 text-sm font-medium hover:bg-accent hover:text-accent-foreground md:h-9"
            :disabled="add.isPending.value">
            <UserPlus
                class="size-4"
                aria-hidden="true" />
            Add my character
            <ChevronDown
                class="size-4 text-muted-foreground"
                aria-hidden="true" />
        </DropdownMenuTrigger>
        <DropdownMenuContent
            align="start"
            class="w-56">
            <DropdownMenuItem
                v-for="entry in candidates"
                :key="entry.id"
                class="min-h-11 md:min-h-8"
                @select="addCharacter(entry)">
                {{ entry.name }}
            </DropdownMenuItem>
        </DropdownMenuContent>
    </DropdownMenu>
</template>

<script setup lang="ts">
    import { ChevronDown, UserPlus } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { Combat, EntrySummary } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import { myCharacterCandidates } from "~/utils/combat";
    import { addCombatantsMutation } from "~/utils/queries/combats";
    import { useEntryDirectory } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        combat: Combat;
        memberId: string;
    }>();

    const directory = useEntryDirectory(() => props.campaignId);
    const candidates = computed(() =>
        myCharacterCandidates(directory.value, props.combat, props.memberId)
    );

    const add = addCombatantsMutation();
    async function addCharacter(entry: EntrySummary) {
        try {
            await add.mutateAsync({
                campaignId: props.campaignId,
                combatId: props.combat.id,
                combatants: [{ entryId: entry.id }],
            });
        } catch (err) {
            toast.error(apiErrorMessage(err, `Could not add ${entry.name}.`));
        }
    }
</script>

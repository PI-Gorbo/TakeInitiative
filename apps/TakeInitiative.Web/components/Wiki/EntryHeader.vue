<template>
    <!-- An entry's header (design §4, 15c): name, kind, "aka …", a 🔒 badge for DM and
         Me, and edit access. [Edit] shows only to someone who can change something.
         15g adds History (for everyone who sees it) and Merge (for editors) in a menu
         beside it, and a player character's badge. -->
    <header class="flex flex-col gap-1.5">
        <div class="flex items-start gap-2">
            <h2 class="min-w-0 flex-1 break-words text-2xl font-semibold leading-tight">
                {{ entry.name }}
            </h2>
            <Button
                v-if="canEdit || canChangeAccess"
                variant="outline"
                class="h-11 shrink-0 gap-1 md:h-9"
                :aria-expanded="editing"
                @click="emit('edit')">
                <Pencil
                    class="size-4"
                    aria-hidden="true" />
                Edit
            </Button>
            <DropdownMenu>
                <DropdownMenuTrigger
                    aria-label="More actions"
                    class="flex size-11 shrink-0 items-center justify-center rounded-md border hover:bg-accent hover:text-accent-foreground md:size-9">
                    <MoreHorizontal
                        class="size-4"
                        aria-hidden="true" />
                </DropdownMenuTrigger>
                <DropdownMenuContent
                    align="end"
                    class="w-48">
                    <DropdownMenuItem
                        class="min-h-11 gap-2 md:min-h-8"
                        @select="emit('history')">
                        <History
                            class="size-4"
                            aria-hidden="true" />
                        History
                    </DropdownMenuItem>
                    <DropdownMenuItem
                        v-if="canEdit"
                        class="min-h-11 gap-2 md:min-h-8"
                        @select="emit('merge')">
                        <Merge
                            class="size-4"
                            aria-hidden="true" />
                        Merge into…
                    </DropdownMenuItem>
                </DropdownMenuContent>
            </DropdownMenu>
        </div>
        <p class="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted-foreground">
            <span class="flex items-center gap-1 text-foreground">
                <span aria-hidden="true">{{ ENTRY_KIND_ICONS[entry.kind] }}</span>
                {{ entry.kind }}
            </span>
            <span v-if="entry.aliases.length > 0">· {{ aliasesLabel(entry.aliases) }}</span>
            <span
                v-if="entry.visibility !== 'Everyone'"
                class="rounded border px-1.5 text-xs font-medium"
                :title="entry.visibility === 'DM' ? 'Visible to the DMs and the creator' : 'Visible only to the creator'">
                🔒 {{ entry.visibility }}
            </span>
            <span>· Edit access: {{ editAccessText }}</span>
            <span
                v-if="entry.claimedByMemberId"
                class="rounded border px-1.5 text-xs font-medium">
                {{ claimerName ? `${claimerName}'s character` : "Player character" }}
            </span>
        </p>
    </header>
</template>

<script setup lang="ts">
    import { History, Merge, MoreHorizontal, Pencil } from "lucide-vue-next";
    import type { Entry } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS, aliasesLabel, editAccessLabel } from "~/utils/entries";

    const props = defineProps<{
        entry: Entry;
        /** The viewer's member id and the creator's name, for "Only me" / "Only Sam". */
        viewerMemberId: string;
        creatorName: string;
        canEdit: boolean;
        canChangeAccess: boolean;
        editing?: boolean;
        /** The claimer's name, for a player character's badge (15g). */
        claimerName?: string;
    }>();
    const emit = defineEmits<{ edit: []; history: []; merge: [] }>();

    const editAccessText = computed(() => editAccessLabel(props.entry, props.viewerMemberId, props.creatorName));
</script>

<template>
    <!-- Player characters (15g, glossary: Claim): who this Character belongs to, and
         "Claim as my character", "Unclaim", or for DMs a member picker. Shown only on
         Character entries. -->
    <div
        v-if="entry.kind === 'Character'"
        class="flex flex-wrap items-center gap-2 text-sm">
        <span
            v-if="claimer"
            class="flex items-center gap-1 rounded-md border px-2 py-1">
            <UserCheck
                class="size-4"
                aria-hidden="true" />
            {{ claimer === viewer.memberId ? "Your player character" : `${nameOf(claimer)}'s player character` }}
        </span>
        <Button
            v-if="canClaimEntry(entry) && !viewer.isDm"
            variant="outline"
            class="h-11 md:h-8"
            :disabled="busy"
            @click="claim(viewer.memberId)">
            Claim as my character
        </Button>
        <label
            v-if="canAssignClaim(entry, viewer)"
            class="flex items-center gap-2">
            <span class="text-muted-foreground">{{ claimer ? "Player character of" : "Claim for" }}</span>
            <select
                :value="claimer ?? ''"
                :disabled="busy"
                aria-label="Player character of"
                class="h-11 rounded-md border bg-background px-2 text-base md:h-8 md:text-sm"
                @change="(e) => claim(((e.target as HTMLSelectElement).value || null) as string | null)">
                <option value="">Nobody</option>
                <option
                    v-for="member in claimable"
                    :key="member.memberId"
                    :value="member.memberId">
                    {{ member.memberId === viewer.memberId ? `${member.username} (you)` : member.username }}
                </option>
            </select>
        </label>
        <Button
            v-else-if="canUnclaimEntry(entry, viewer)"
            variant="ghost"
            class="h-11 md:h-8"
            :disabled="busy"
            @click="claim(null)">
            Unclaim
        </Button>
    </div>
</template>

<script setup lang="ts">
    import { UserCheck } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { CampaignMember, Entry } from "~/utils/api/types";
    import {
        canAssignClaim,
        canClaimEntry,
        canUnclaimEntry,
        claimerOf,
        entryVisibleTo,
        type EntryViewer,
    } from "~/utils/entries";
    import { putEntryClaimMutation } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        viewer: EntryViewer;
        members: readonly CampaignMember[];
        nameOf: (memberId: string) => string;
    }>();

    const claimer = computed(() => claimerOf(props.entry));
    // A DM can assign it only to members who can see it (the API's rule).
    const claimable = computed(() => props.members.filter((m) => entryVisibleTo(props.entry, m)));

    const mutation = putEntryClaimMutation();
    const busy = computed(() => mutation.isPending.value);

    async function claim(memberId: string | null) {
        if (busy.value || memberId === claimer.value) return;
        try {
            await mutation.mutateAsync({ campaignId: props.campaignId, entryId: props.entry.id, memberId });
        } catch (error) {
            toast.error(apiErrorMessage(error, "Could not change the claim."));
        }
    }
</script>

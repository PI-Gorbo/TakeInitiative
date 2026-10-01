<template>
    <!-- An entry's Details (25e), under "More about X": who plays it (`ClaimControl`, on a
         Character), the D&D Beyond sheet field on a claimed one (27e, under the claim line),
         who made it and when, who can see and edit it, where it came from (20d's source
         line), and — on a phone, for everything but a claimed Character — its Links (27d).
         Changing any of it stays in Edit details. The desktop's right-hand panel (25f) shows
         who plays it at its top, so there `hideClaim` leaves it out here, and Links is a
         block of its own under this one rather than part of it. -->
    <div class="flex flex-col gap-3 text-sm">
        <WikiClaimControl
            v-if="!hideClaim"
            :campaignId="campaignId"
            :entry="entry"
            :viewer="viewer"
            :members="members"
            :nameOf="nameOf" />
        <WikiDndBeyondField
            :campaignId="campaignId"
            :entry="entry"
            :viewer="viewer" />
        <dl class="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-1.5">
            <dt class="text-muted-foreground">Created by</dt>
            <dd>
                {{ entry.creatorMemberId === viewer.memberId ? "You" : creatorName }}
                <span class="text-muted-foreground">· {{ createdOn }}</span>
            </dd>
            <dt class="text-muted-foreground">Who can see</dt>
            <dd>{{ visibilityText(entry, viewer.memberId, creatorName) }}</dd>
            <dt class="text-muted-foreground">Who can edit</dt>
            <dd>
                {{ editAccessLabel(entry, viewer.memberId, creatorName) }}
                <span
                    v-if="entry.editAccess !== 'Anyone'"
                    class="text-muted-foreground"
                    >· DMs can always edit</span
                >
            </dd>
        </dl>
        <ReferenceEntrySourceLine
            v-if="entry.source"
            :campaignId="campaignId"
            :source="entry.source" />
        <WikiEntryLinks
            v-if="showLinks"
            :campaignId="campaignId"
            :entry="entry"
            :viewer="viewer" />
    </div>
</template>

<script setup lang="ts">
    import type { CampaignMember, Entry } from "~/utils/api/types";
    import { editAccessLabel, type EntryViewer } from "~/utils/entries";
    import { visibilityText } from "~/utils/entrySections";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        viewer: EntryViewer;
        members: readonly CampaignMember[];
        nameOf: (memberId: string) => string;
        creatorName: string;
        /** Leave out who plays it (25f: the right-hand panel shows it on its own). */
        hideClaim?: boolean;
        /**
         * Draw the Links section here (27d). The phone does, for everything but a claimed
         * Character — whose links sit high, under the stats line. The desktop never does: there
         * Links is its own block in the right-hand panel, under Details.
         */
        showLinks?: boolean;
    }>();

    const createdOn = computed(() =>
        new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(props.entry.createdAt))
    );
</script>

<template>
    <!-- An entry (design §4, 15c): its header and its timeline. The article (15f),
         connections (19), gallery (16) and combats (18) arrive with their steps. -->
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-5 px-4 py-4 pb-safe">
        <NuxtLink
            :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki`"
            class="-ml-2 flex h-11 w-fit items-center gap-1 rounded-md px-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-9">
            <ChevronLeft
                class="size-4"
                aria-hidden="true" />
            Wiki
        </NuxtLink>

        <EmptyState
            v-if="notFound"
            :icon="BookX"
            title="Entry not found">
            That entry is not there, or you cannot see it.
        </EmptyState>
        <LoadingFallback
            v-else-if="!entry || !campaign"
            :isLoading="entryQuery.isLoading.value || !campaign"
            :isError="entryQuery.isError.value"
            iconSize="2x"
            class="pt-8" />
        <template v-else>
            <WikiEntryHeaderEditor
                v-if="editing"
                :key="entry.id"
                :campaignId="campaignId"
                :entry="entry"
                :canEdit="canEdit"
                :canChangeAccess="canChangeAccess"
                @done="editing = false" />
            <WikiEntryHeader
                v-else
                :entry="entry"
                :viewerMemberId="campaign.currentMemberId"
                :creatorName="creatorName"
                :canEdit="canEdit"
                :canChangeAccess="canChangeAccess"
                :editing="editing"
                @edit="editing = true" />

            <WikiEntryTimeline
                :campaign="campaign"
                :entryId="entry.id"
                :entryName="entry.name" />

            <!-- Posts to the current session with the mention prefilled (design §4). -->
            <Button
                as-child
                variant="outline"
                class="h-12 justify-start gap-2 text-muted-foreground md:h-10">
                <NuxtLink :to="aboutLink">
                    <MessageSquarePlus
                        class="size-4"
                        aria-hidden="true" />
                    <span class="truncate">Add a note about {{ entry.name }}…</span>
                </NuxtLink>
            </Button>
        </template>
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { BookX, ChevronLeft, MessageSquarePlus } from "lucide-vue-next";
    import { apiErrorStatus } from "~/utils/apiErrorParser";
    import { currentMember } from "~/utils/campaign";
    import { ABOUT_PARAM, canChangeEntryAccess, canEditEntry } from "~/utils/entries";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getEntryQuery } from "~/utils/queries/entries";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId-wiki-entryId");
    const campaignId = computed(() => route.params.campaignId as string);
    const entryId = computed(() => route.params.entryId as string);

    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const campaign = computed(() => campaignQuery.data.value);
    const entryQuery = useQuery(getEntryQuery(campaignId, entryId));
    const entry = computed(() => entryQuery.data.value);
    const notFound = computed(() => apiErrorStatus(entryQuery.error.value) === 404);

    useHead({ title: () => (entry.value ? `${entry.value.name} · Wiki` : "Wiki") });

    const viewer = computed(() => ({
        memberId: campaign.value?.currentMemberId ?? "",
        isDm: currentMember(campaign.value)?.role === "DM",
    }));
    const canEdit = computed(() => !!entry.value && canEditEntry(entry.value, viewer.value));
    const canChangeAccess = computed(() => !!entry.value && canChangeEntryAccess(entry.value, viewer.value));
    const creatorName = computed(
        () => campaign.value?.members.find((m) => m.memberId === entry.value?.creatorMemberId)?.username ?? "the creator"
    );

    const editing = ref(false);
    // Losing the right to edit (edit access or a role change) closes the editor.
    watch([canEdit, canChangeAccess], ([edit, access]) => {
        if (!edit && !access) editing.value = false;
    });
    watch(entryId, () => (editing.value = false));

    const aboutLink = computed(
        () => `/app/campaigns/${encodeURIComponent(campaignId.value)}?${ABOUT_PARAM}=${encodeURIComponent(entryId.value)}`
    );
</script>

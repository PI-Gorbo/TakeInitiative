<template>
    <div class="flex h-full w-full flex-col">
        <!-- The Campaign tab is the session stream (design §3). The root stays one
             element while the campaign loads: the page transition animates it. -->
        <template v-if="campaign">
            <!-- The filter chips and the members button. -->
            <div class="shrink-0 border-b">
                <PageContainer class="flex items-center gap-2 px-2">
                    <SessionStreamFilters v-model="filter" />
                    <button
                        type="button"
                        class="flex h-11 min-w-11 items-center justify-center gap-2 rounded-md px-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        :aria-label="`Members (${campaign.members.length})`"
                        @click="openMembers?.()">
                        <Users class="size-5" />
                        <span>{{ campaign.members.length }}</span>
                        <span class="hidden sm:inline">Members</span>
                    </button>
                </PageContainer>
            </div>
            <!-- While a combat is live (18e.3). -->
            <CombatJoinCombatBanner :campaignId="campaign.id" />

            <PageContainer class="flex min-h-0 flex-1 flex-col">
                <SessionStream
                    ref="stream"
                    :campaignId="campaign.id"
                    :campaign="campaign"
                    :filter="filter"
                    :focusNoteId="focusNoteId"
                    :focusSessionNumber="focusSessionNumber"
                    @noteOpened="clearNoteLink"
                    @sessionOpened="clearParam(SESSION_LINK_PARAM)" />
                <Composer
                    :key="campaign.id"
                    :campaign="campaign"
                    :filter="filter"
                    :about="aboutEntry"
                    :share="shareId"
                    :compose="composeText"
                    @aboutUsed="clearParam(ABOUT_PARAM)"
                    @shareUsed="clearParam(SHARE_PARAM)"
                    @composeUsed="clearParam(COMPOSE_PARAM)" />
            </PageContainer>
        </template>
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { Users } from "lucide-vue-next";
    import type { SessionStreamFilter } from "~/utils/api/types";
    import { ABOUT_PARAM, entryDirectory, resolveEntry } from "~/utils/entries";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";
    import { FILTER_PARAM, filterFromQuery, filterToQuery } from "~/utils/streamFilters";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getEntriesQuery } from "~/utils/queries/entries";
    import { OPEN_MEMBERS, SESSION_LINK_PARAM, sessionFromQuery } from "~/utils/search";
    import { COMPOSE_PARAM, composeFromQuery } from "~/utils/searchActions";
    import { SHARE_PARAM, validShareId } from "~/utils/shareTarget";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId");
    const router = useRouter();
    const campaignQuery = useQuery(getCampaignQuery(() => route.params.campaignId as string));
    const campaign = computed(() => campaignQuery.data.value);

    // The panel lives in the campaign layout, so ⌘K's Share opens it from any tab.
    const openMembers = inject(OPEN_MEMBERS);

    // The filter lives in the URL (`?filter=recaps`), so a reload or a shared link
    // keeps it. Changing it replaces the entry rather than adding history.
    const filter = computed<SessionStreamFilter>({
        get: () => filterFromQuery(route.query[FILTER_PARAM]),
        set: (value) => {
            const { [FILTER_PARAM]: _, ...query } = route.query;
            const param = filterToQuery(value);
            void router.replace({ query: param ? { ...query, [FILTER_PARAM]: param } : query });
        },
    });

    // A copied note link: `?note={noteId}`. The stream opens at the note, then the
    // parameter is dropped so a reload opens at the bottom again.
    const focusNoteId = computed(() => {
        const value = route.query[NOTE_LINK_PARAM];
        return typeof value === "string" && value ? value : undefined;
    });
    function clearParam(name: string) {
        const { [name]: _, ...query } = route.query;
        void router.replace({ query });
    }
    const clearNoteLink = () => clearParam(NOTE_LINK_PARAM);

    // A session from ⌘K: `?session={number}` (17b). The stream opens at its divider,
    // then the parameter is dropped, as `?note=` is.
    const focusSessionNumber = computed(() => sessionFromQuery(route.query[SESSION_LINK_PARAM]));
    watch(
        () => route.query[SESSION_LINK_PARAM],
        (value) => {
            if (value !== undefined && !sessionFromQuery(value)) clearParam(SESSION_LINK_PARAM);
        },
        { immediate: true }
    );

    // "Add a note about X" from an entry page: `?about={entryId}` (15c). The composer
    // uses it once and the page drops it. An id the viewer's directory does not hold
    // (unknown, or hidden from them) is dropped without a word.
    const entriesQuery = useQuery(getEntriesQuery(() => route.params.campaignId as string));
    const aboutId = computed(() => {
        const value = route.query[ABOUT_PARAM];
        return typeof value === "string" && value ? value : undefined;
    });
    const aboutEntry = computed(() =>
        aboutId.value ? resolveEntry(entryDirectory(entriesQuery.data.value), aboutId.value) : undefined
    );
    watch(
        [aboutId, aboutEntry, () => entriesQuery.isSuccess.value],
        ([id, entry, loaded]) => {
            if (id && !entry && loaded) clearParam(ABOUT_PARAM);
        },
        { immediate: true }
    );

    // A share from the phone (16e): `/app/share` sends `?share={id}`, and the composer
    // takes the images once and drops the parameter.
    const shareId = computed(() => validShareId(route.query[SHARE_PARAM]));

    // "New note mentioning X" from ⌘K: `?compose=@X` (17c). The composer takes it once,
    // into an empty draft only, and the page drops it. A blank value is dropped here.
    const composeText = computed(() => composeFromQuery(route.query[COMPOSE_PARAM]));
    watch(
        () => route.query[COMPOSE_PARAM],
        (value) => {
            if (value !== undefined && composeFromQuery(value) === undefined) clearParam(COMPOSE_PARAM);
        },
        { immediate: true }
    );
</script>

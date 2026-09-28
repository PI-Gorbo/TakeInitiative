<template>
    <PageContainer class="flex flex-col gap-3 px-4 py-4 pb-safe">
        <!-- Loose ends (design §5, 19e): the viewer's own to-dos, derived on every read.
             `?session={number}` narrows it to one session (a divider's 🧵 n), and
             `?note={id}` marks one note's row (the 16c hint). Each row is resolved in place.
             ✨ model suggestions (23d) join 19's link suggestions on the note rows. -->
        <NuxtLink
            :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki`"
            class="-ml-2 flex h-11 w-fit items-center gap-1 rounded-md px-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-9">
            <ChevronLeft
                class="size-4"
                aria-hidden="true" />
            Wiki
        </NuxtLink>

        <div class="flex flex-wrap items-center gap-2">
            <h1 class="min-w-0 flex-1 text-lg font-semibold">
                <span aria-hidden="true">🧵</span>
                {{ heading }}
            </h1>
            <Button
                v-if="session !== null"
                as-child
                variant="ghost"
                class="h-11 px-3 md:h-9">
                <NuxtLink :to="looseEndsHref(campaignId)">All</NuxtLink>
            </Button>
        </div>

        <SuggestionsFindSuggestionsButton
            v-if="looseEndsQuery.data.value && suggestions.noteCount.value > 0"
            :phase="suggestions.phase.value"
            :noteCount="suggestions.noteCount.value"
            :total="suggestions.total.value"
            @retry="suggestions.retry" />

        <LoadingFallback
            v-if="!looseEndsQuery.data.value"
            :isLoading="looseEndsQuery.isLoading.value"
            :isError="looseEndsQuery.isError.value"
            iconSize="2x"
            class="pt-8" />
        <EmptyState
            v-else-if="items.length === 0 && !listHasHeld"
            :icon="CircleCheck"
            title="Nothing loose">
            Everything is linked.
        </EmptyState>
        <LooseEndsLooseEndList
            v-else
            ref="list"
            :campaignId="campaignId"
            :items="items"
            :authorName="authorName"
            :highlightedNoteId="highlightedNoteId"
            @openImage="imageViewer.open" />

        <!-- The image viewer, following `?image=` (16c), over the image notes listed here. -->
        <ImageViewer
            :campaignId="campaignId"
            :items="viewerItems"
            :authorName="() => authorName"
            :ready="!!looseEndsQuery.data.value && !looseEndsQuery.isFetching.value" />
    </PageContainer>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { ChevronLeft, CircleCheck } from "lucide-vue-next";
    import { currentMember } from "~/utils/campaign";
    import { itemsInSession, looseEndsHeading, looseEndsHref, LOOSE_END_SESSION_PARAM, sessionFromQuery } from "~/utils/looseEnds";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getLooseEndsQuery } from "~/utils/queries/looseEnds";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });
    useHead({ title: "Loose ends · Wiki" });

    const route = useRoute("app-campaigns-campaignId-wiki-loose-ends");
    const campaignId = computed(() => route.params.campaignId as string);

    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const authorName = computed(() => currentMember(campaignQuery.data.value)?.username ?? "You");

    const looseEndsQuery = useQuery(getLooseEndsQuery(campaignId));
    // Derived and never pushed: read them again whenever the page opens.
    onMounted(() => {
        if (looseEndsQuery.data.value) void looseEndsQuery.refetch();
    });

    const session = computed(() => sessionFromQuery(route.query[LOOSE_END_SESSION_PARAM]));
    const items = computed(() => itemsInSession(looseEndsQuery.data.value?.items ?? [], session.value));
    const heading = computed(() => looseEndsHeading(items.value.length, session.value));
    // ✨ Suggestions (23d): as the device setting allows; the rows read them by `inject`.
    const suggestions = useLooseEndSuggestions(campaignId, items);
    // The list keeps a just-resolved row for its ✓, so the empty state waits for it.
    const list = useTemplateRef<{ holding: boolean }>("list");
    const listHasHeld = computed(() => !!list.value?.holding);

    const imageViewer = useImageViewer();
    const viewerItems = computed(() =>
        items.value.flatMap((i) => (i.note ? [{ note: i.note, sessionNumber: i.sessionNumber ?? undefined }] : []))
    );

    // `?note={id}` (the stream's "⚠ Tag what's in this?") is used once, when the list has
    // loaded: its row scrolls into view and is marked for a moment, then it is dropped.
    const highlightedNoteId = ref<string | null>(null);
    let highlightTimer: ReturnType<typeof setTimeout> | undefined;
    watch(
        [() => route.query[NOTE_LINK_PARAM], () => looseEndsQuery.data.value],
        async ([noteId, data]) => {
            if (typeof noteId !== "string" || !data) return;
            const { [NOTE_LINK_PARAM]: _, ...query } = route.query;
            void navigateTo({ query }, { replace: true });
            const row = data.items.find((i) => i.note?.id.toLowerCase() === noteId.toLowerCase());
            if (!row?.note) return;
            highlightedNoteId.value = row.note.id;
            await nextTick();
            document.querySelector(`[data-loose-note="${row.note.id}"]`)?.scrollIntoView({ block: "center" });
            clearTimeout(highlightTimer);
            highlightTimer = setTimeout(() => (highlightedNoteId.value = null), 2500);
        },
        { immediate: true }
    );
    onBeforeUnmount(() => clearTimeout(highlightTimer));
</script>

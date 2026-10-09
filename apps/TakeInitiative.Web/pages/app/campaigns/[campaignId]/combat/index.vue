<template>
    <PageContainer class="flex flex-col gap-4 px-3 py-3 md:px-4">
        <!-- The Combat tab (18c.3): the combats the viewer can see, live first. A DM
             creates one with New combat. Pushes keep the list current. -->
        <div class="flex min-h-11 items-center gap-2">
            <h2 class="flex-1 text-lg font-semibold">Combats</h2>
            <Button
                v-if="isDm && hasCombats"
                class="h-11 gap-1 md:h-9"
                @click="openNew()">
                <Plus
                    class="size-4"
                    aria-hidden="true" />
                New combat
            </Button>
        </div>

        <LoadingFallback
            v-if="!combatsQuery.data.value"
            :isLoading="combatsQuery.isLoading.value"
            :isError="combatsQuery.isError.value"
            iconSize="2x"
            class="pt-8" />
        <div
            v-else-if="!hasCombats"
            class="flex flex-col items-center">
            <EmptyState
                :icon="Swords"
                title="No combats yet.">
                {{
                    isDm
                        ? "Prepare one as a draft: add the combatants, then start it."
                        : "When your DM starts a combat, it shows here."
                }}
            </EmptyState>
            <Button
                v-if="isDm"
                class="-mt-10 h-11 gap-1 md:h-9"
                @click="openNew()">
                <Plus
                    class="size-4"
                    aria-hidden="true" />
                New combat
            </Button>
        </div>
        <CombatList
            v-else
            :campaignId="campaignId"
            :combats="combatsQuery.data.value.combats"
            :isDm="isDm" />

        <CombatNewCombatDialog
            v-model:open="newOpen"
            :campaignId="campaignId"
            :initialName="newName" />
    </PageContainer>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { Plus, Swords } from "lucide-vue-next";
    import { currentMember } from "~/utils/campaign";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getCombatsQuery } from "~/utils/queries/combats";
    import {
        NEW_COMBAT_PARAM,
        newCombatFromQuery,
    } from "~/utils/searchActions";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId-combat");
    const router = useRouter();
    const campaignId = computed(() => route.params.campaignId as string);

    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const isDm = computed(
        () => currentMember(campaignQuery.data.value)?.role === "DM"
    );

    const combatsQuery = useQuery(getCombatsQuery(campaignId));
    const hasCombats = computed(
        () => (combatsQuery.data.value?.combats.length ?? 0) > 0
    );

    const newOpen = ref(false);
    const newName = ref("");
    function openNew(name = "") {
        newName.value = name;
        newOpen.value = true;
    }

    // "⚔ Start combat" from ⌘K (18f): `?new=Goblin Ambush` opens New combat with that
    // name, once, and the page drops the parameter, as `?compose=` is. A player (or a
    // role the campaign has not loaded yet) waits for the role; a player's is dropped.
    watch(
        [() => route.query[NEW_COMBAT_PARAM], () => campaignQuery.data.value],
        ([value, campaign]) => {
            const name = newCombatFromQuery(value);
            if (name === undefined || !campaign) return;
            if (isDm.value) openNew(name);
            const { [NEW_COMBAT_PARAM]: _, ...query } = route.query;
            void router.replace({ query });
        },
        { immediate: true }
    );
</script>

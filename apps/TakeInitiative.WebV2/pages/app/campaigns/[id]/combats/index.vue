<template>
    <div>
        
        <LoadingFallback :isLoading="queryResult.isLoading.value">
            <Card
                v-if="
                    !(
                        queryResult.data.value!.plannedCombats.length ||
                        queryResult.data.value!.combats.length
                    )
                "
                class="border-2 p-4 border-primary/50">
                <div v-if="campaignStore.isDm" class="flex flex-col gap-4">
                    <div>Start by making your first draft combat</div>
                    <CampaignCombatDraftCreateForm
                        :onCreateDraftCombat="onCreatePlannedCombat" />
                </div>
                <template v-else>
                    Nothing here yet! When you complete a combat, you will see a
                    history here.
                </template>
            </Card>
        </LoadingFallback>
    </div>
</template>
<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
    import { combatQueries } from "~/utils/queries/combats";
    const route = useRoute("app-campaigns-id-combats");
    const srceenSize = useScreenSize();
    const queryResult = useQuery(
        combatQueries.getAllCombatsQuery(() => route.params.id)
    );

    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
        layoutTransition: false,
        middleware: [
            async (to) => {
                // This middleware is used to redirect the user to the first combat in the list if they are not on a large screen.
                // This is because the campaignCombats layout will show a sidebar list with all the combats on large screens.
                if (import.meta.server) return;
                if (!srceenSize.isLargeScreen.value) return;

                const queryResult = await useQuery(
                    combatQueries.getAllCombatsQuery(
                        () => to.params.id as string
                    )
                ).suspense();

                if (queryResult.data?.plannedCombats.length) {
                    return navigateTo({
                        name: "app-campaigns-id-combats-drafts-draftCombatId",
                        params: {
                            id: to.params.id as string,
                            draftCombatId:
                                queryResult.data.plannedCombats[0].id,
                        },
                    });
                } else if (queryResult.data?.combats.length) {
                    return navigateTo({
                        name: "app-campaigns-id-combats-history-combatId",
                        params: {
                            id: to.params.id as string,
                            combatId: queryResult.data.combats[0].combatId,
                        },
                    });
                }
            },
        ],
    });

    const campaignStore = useCampaignStore();
    const campaignCombatStore = useCampaignCombatsStore();

    async function onCreatePlannedCombat(
        input: Omit<CreatePlannedCombatRequest, "campaignId">,
        startCombatImmediately: boolean = false
    ) {
        return await campaignCombatStore
            .createPlannedCombat(input)
            .then(async (pc) => {
                if (startCombatImmediately) {
                    await onOpenCombat(pc?.id);
                }
            });
    }

    async function onOpenCombat(plannedCombatId: string) {
        return campaignStore
            .openCombat(plannedCombatId)
            .then((c) =>
                Promise.resolve(
                    useNavigator().toCombat(
                        campaignStore.state.campaign?.id!,
                        campaignStore.state.currentCombatInfo?.id!
                    )
                )
            );
    }
</script>

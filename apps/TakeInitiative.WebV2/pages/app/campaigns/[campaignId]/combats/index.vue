<template>
    <div>
        <LoadingFallback :isLoading="test.isLoading.value">
            <Card
                v-if="
                    !(
                        test.data.value!.plannedCombats.length ||
                        test.data.value!.combats.length
                    )
                "
                class="border-2 p-4 border-primary/50">
                <div v-if="campaignStore.isDm" class="flex flex-col gap-4">
                    <div>Start by making your first draft combat</div>
                    <CampaignCombatDraftCreateForm
                        :onCreateDraftCombat="createPlannedCombat" />
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
    import {
        createDraftCombatMutation,
        getAllCombatsQuery,
    } from "~/utils/queries/combats";
    const api = useApi();
    const route = useRoute("app-campaigns-campaignId-combats");
    const srceenSize = useScreenSize();
    const test = useQuery(getAllCombatsQuery(() => route.params.campaignId));

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
                    getAllCombatsQuery(() => to.params.campaignId as string)
                ).suspense();

                if (queryResult.data?.plannedCombats.length) {
                    return navigateTo({
                        name: "app-campaigns-campaignId-combats-drafts-draftCombatId",
                        params: {
                            campaignId: to.params.campaignId as string,
                            draftCombatId:
                                queryResult.data.plannedCombats[0].id,
                        },
                    });
                } else if (queryResult.data?.combats.length) {
                    return navigateTo({
                        name: "app-campaigns-campaignId-combats-history-combatId",
                        params: {
                            campaignId: to.params.campaignId as string,
                            combatId: queryResult.data.combats[0].combatId,
                        },
                    });
                }
            },
        ],
    });

    const campaignStore = useCampaignStore();

    const draftCombatHelper = useDraftCombatHelper();
    async function createPlannedCombat(
        request: Omit<CreatePlannedCombatRequest, "campaignId">,
        startImmidately: boolean
    ) {
        return await draftCombatHelper.createDraftCombat(
            { ...request, campaignId: route.params.campaignId },
            startImmidately
        );
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

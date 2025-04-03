<template>
    <main>
        <div v-if="campaignStore.isDm" class="flex flex-col gap-4">
            <div>Start by making your first draft combat</div>
            <CampaignCombatCreatePlannedCombatForm
                :onCreatePlannedCombat="onCreatePlannedCombat" />
        </div>
        <template v-else>
            Nothing here yet! When you complete a combat, you will see a history
            here.
        </template>
    </main>
</template>
<script setup lang="ts">
    import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";

    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
        layoutTransition: false,
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

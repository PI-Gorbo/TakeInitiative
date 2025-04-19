<template>
    <LoadingFallback
        :isLoading="
            campaignQuery.isLoading.value || combatQuery.isLoading.value
        "
        class="h-full w-full flex justify-center">
        <div class="lg:grid lg:grid-cols-3 w-page lg:gap-4">
            <div class="hidden lg:block lg:col-span-1 lg:col-start-1">
                <Card>
                    <CardHeader>
                        <CardTitle class="font-NovaCut text-gold">{{
                            combatQuery.data.value?.combat.combatName
                        }}</CardTitle>
                    </CardHeader>
                </Card>
            </div>
            <div class="lg:col-span-2 lg:col-start-2 flex flex-col">
                <CampaignCombatInitiativeList
                    :campaignId="route.params.campaignId"
                    :combatId="route.params.combatId" />
            </div>
        </div>
    </LoadingFallback>
</template>
<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getCombatQuery } from "~/utils/queries/combats";

    const route = useRoute("app-campaigns-campaignId-combats-combatId");

    const campaignQuery = useQuery(
        getCampaignQuery(() => route.params.campaignId)
    );
    const combatQuery = useQuery(
        getCombatQuery(
            () => route.params.campaignId,
            () => route.params.combatId
        )
    );

    definePageMeta({
        layout: "main-app",
        requiresAuth: true,
        layoutTransition: false,
    });
</script>

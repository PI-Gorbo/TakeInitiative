<template>
    <LoadingFallback
        :isLoading="
            store.campaignQuery.isLoading || store.combatQuery.isLoading
        "
        class="h-full w-full max-h-full flex justify-center">
        <div class="lg:grid lg:grid-cols-3 w-page lg:gap-4 max-h-full">
            <div
                class="hidden lg:block lg:col-span-1 lg:col-start-1 max-h-full">
                <Card class="max-h-full w-full flex flex-col">
                    <CardHeader>
                        <CardTitle class="font-NovaCut text-gold">{{
                            store.combatQuery.data?.combat.combatName
                        }}</CardTitle>
                        <CardDescription>
                            <template
                                v-if="
                                    store.combatQuery.data?.combat.state ===
                                    CombatState.Open
                                ">
                                Combat has not started yet. Players can add
                                their characters to the combat.
                            </template>
                            <template
                                v-else-if="
                                    store.combatQuery.data?.combat.state ===
                                    CombatState.Started
                                ">
                                Round
                                {{
                                    store.combatQuery.data?.combat?.roundNumber
                                }}
                            </template>
                            <template else> This combat has ended. </template>
                        </CardDescription>
                    </CardHeader>

                    <CardContent class="flex-1 ">
                        <Transition name="fade" mode="out-in">
                            <section
                                v-if="
                                    store.combatQuery.data?.combat.state ===
                                    CombatState.Started
                                "
                                class="flex flex-col gap-2 h-full max-h-full overflow-hidden">
                                <header>
                                    <div>
                                        <FontAwesomeIcon
                                            :icon="faPersonMilitaryPointing" />
                                        Reinformcements
                                    </div>
                                    <CardDescription>
                                        Characters that can be added to the
                                        combat by the DM.
                                    </CardDescription>
                                </header>
                                <div class="flex-grow overflow-auto">
                                    <CampaignCombatReinforcementList
                                        :campaignId="route.params.campaignId"
                                        :combatId="route.params.combatId" />
                                </div>
                                <div class="flex justify-end">
                                    <Sheet
                                        v-model:open="
                                            sheetStates.addReinforcementsSheetOpen
                                        ">
                                        <SheetTrigger asChild>
                                            <Button variant="link">
                                                <FontAwesomeIcon
                                                    :icon="
                                                        faPlusCircle
                                                    " />Add</Button
                                            >
                                        </SheetTrigger>
                                        <SheetContent>
                                            <SheetHeader>
                                                <SheetTitle>
                                                    Add Reinforcements
                                                </SheetTitle>
                                            </SheetHeader>
                                            <StageCharactersForm
                                                @submitted="
                                                    () =>
                                                        (sheetStates.addReinforcementsSheetOpen = false)
                                                "
                                                :campaignId="
                                                    route.params.campaignId
                                                "
                                                :combatId="
                                                    route.params.combatId
                                                " />
                                        </SheetContent>
                                    </Sheet>
                                </div>
                            </section>
                        </Transition>
                    </CardContent>
                </Card>
            </div>
            <div class="lg:col-span-2 lg:col-start-2 flex flex-col">
                <CampaignCombatInitiativeList />
            </div>
        </div>
    </LoadingFallback>
</template>
<script setup lang="ts">
    import {
        faPersonMilitaryPointing,
        faPlusCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery } from "@tanstack/vue-query";
    import StageCharactersForm from "~/components/Campaign/Combat/StageCharactersForm.vue";
    import CardContent from "~/components/ui/card/CardContent.vue";
    import CardDescription from "~/components/ui/card/CardDescription.vue";
    import Sheet from "~/components/ui/sheet/Sheet.vue";
    import SheetContent from "~/components/ui/sheet/SheetContent.vue";
    import SheetHeader from "~/components/ui/sheet/SheetHeader.vue";
    import SheetTitle from "~/components/ui/sheet/SheetTitle.vue";
    import SheetTrigger from "~/components/ui/sheet/SheetTrigger.vue";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getCombatQuery } from "~/utils/queries/combats";
    import { CombatState } from "~/utils/types/models";

    const route = useRoute("app-campaigns-campaignId-combats-combatId");

    const store = useCombatStore();
    watchEffect(() => {
        store.init(route.params.campaignId, route.params.combatId);
    });

    // state
    const sheetStates = reactive({
        addReinforcementsSheetOpen: false,
    });

    definePageMeta({
        layout: "main-app",
        requiresAuth: true,
        layoutTransition: false,
    });
</script>

<template>
    <LoadingFallback
        :isLoading="
            store.campaignQuery.isLoading || store.combatQuery.isLoading
        "
        class="h-full w-full max-h-full flex justify-center">
        <div class="lg:grid lg:grid-cols-3 w-page lg:gap-4 max-h-full pb-4">
            <div
                class="hidden lg:block lg:col-span-1 lg:col-start-1 overflow-auto">
                <Card class="w-full max-h-full flex flex-col">
                    <CardHeader>
                        <span class="flex justify-between flex-wrap items-center">
                            <CardTitle class="font-NovaCut text-gold">{{
                                    store.combatQuery.data?.combat.combatName
                                }}
                            </CardTitle>
                            <template v-if="store.userIsDm">

                                <AsyncButton
                                    v-if="store.combatIsStarted"
                                    label="End Combat"
                                    loadingLabel="Ending..."
                                    :icon="faFlag"
                                    variant="outline"
                                    class="interactable"
                                    :click="finishCombat"
                                />
                            </template>
                        </span>

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
                            <template else> This combat has ended.</template>
                        </CardDescription>
                    </CardHeader>
                    <CardContent class="flex-1 h-full max-h-full overflow-auto">
                        <Transition name="fade" mode="out-in">
                            <section
                                v-if="
                                    store.combatQuery.data?.combat.state ===
                                    CombatState.Started
                                "
                                class="flex flex-col gap-2 flex-1 overlfow-auto h-full max-h-full">
                                <header>
                                    <div>
                                        <FontAwesomeIcon
                                            :icon="faPersonMilitaryPointing"/>
                                        Reinformcements
                                    </div>
                                    <CardDescription>
                                        Characters that can be added to the
                                        combat by the DM.
                                    </CardDescription>
                                </header>
                                <div class="flex-1 overflow-y-auto">
                                    <CampaignCombatReinforcementList
                                        class="overflow-auto"
                                        :campaignId="route.params.campaignId"
                                        :combatId="route.params.combatId"/>
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
                                                    "/>
                                                Add
                                            </Button
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
                                                "/>
                                        </SheetContent>
                                    </Sheet>
                                </div>
                            </section>
                        </Transition>
                    </CardContent>
                </Card>
            </div>
            <div
                class="lg:col-span-2 lg:col-start-2 flex flex-col overflow-auto gap-4">
                <div class="flex-1 overflow-auto">
                    <CampaignCombatInitiativeList/>
                </div>
                <div class="flex justify-end">
                    <AsyncButton
                        v-if="store.combatIsStarted"
                        variant="destructive"
                        label="End Turn"
                        loadingLabel="Ending..."
                        :click="endTurn"/>
                </div>
            </div>
        </div>
    </LoadingFallback>
</template>
<script setup lang="ts">
import {
    faFlag,
    faPersonMilitaryPointing,
    faPlusCircle,
} from "@fortawesome/free-solid-svg-icons";
import {FontAwesomeIcon} from "@fortawesome/vue-fontawesome";
import {toast} from "vue-sonner";
import StageCharactersForm from "~/components/Campaign/Combat/StageCharactersForm.vue";
import CardContent from "~/components/ui/card/CardContent.vue";
import CardDescription from "~/components/ui/card/CardDescription.vue";
import Sheet from "~/components/ui/sheet/Sheet.vue";
import SheetContent from "~/components/ui/sheet/SheetContent.vue";
import SheetHeader from "~/components/ui/sheet/SheetHeader.vue";
import SheetTitle from "~/components/ui/sheet/SheetTitle.vue";
import SheetTrigger from "~/components/ui/sheet/SheetTrigger.vue";
import {useEndTurnMutation, useFinishCombatMutation} from "~/utils/queries/combats";
import {CombatState} from "~/utils/types/models";

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
    pageType: "fixed",
    requiresAuth: true,
    layoutTransition: false,
});

const endTurnMutation = useEndTurnMutation();
const endTurn = async () => {
    await endTurnMutation
        .mutateAsync({
            combatId: route.params.combatId,
        })
        .then(() => toast.success("Ended Turn!"))
        .catch(() => toast.error("Failed to end turn!"));
};

const finishCombatMutation = useFinishCombatMutation()
const finishCombat = async () => {
    await finishCombatMutation
        .mutateAsync({
            combatId: route.params.combatId
        })
        .then(() => toast.success("Combat Finished!"))
        .catch(() => toast.error("Something went wrong while trying to finish the combat"));
}
</script>

<template>
    <Tabs defaultValue="Combat">
        <div class="lg:hidden flex gap-1 justify-between">
            <TabsList>
                <TabsTrigger value="Combat">Combat</TabsTrigger>
                <TabsTrigger value="Reinforcements">Reinforcements</TabsTrigger>
            </TabsList>
        </div>

        <TabsContent
            value="Combat"
            class="lg:mt-0">
            <header class="lg:hidden flex gap-2 items-center">
                <CardTitle class="font-NovaCut text-gold font-bold"
                    >{{ store.combatQuery.data?.combat?.combatName }}
                </CardTitle>
                <span>
                    <template
                        v-if="
                            store.combatQuery.data?.combat?.state ===
                            CombatState.Open
                        ">
                        - Combat is open to players, but has not started.
                    </template>
                    <template
                        v-else-if="
                            store.combatQuery.data?.combat?.state ===
                            CombatState.Started
                        "
                        >- Round
                        {{ store.combatQuery.data?.combat?.roundNumber }}
                    </template>
                    <template v-else>- This combat has ended.</template>
                </span>
            </header>
            <slot />
        </TabsContent>
        <TabsContent value="Reinforcements">
            <header>
                <div>
                    <FontAwesomeIcon :icon="faPersonMilitaryPointing" />
                    Reinformcements
                </div>
                <CardDescription>
                    Characters that can be added to the combat by the DM.
                </CardDescription>
            </header>

            <CampaignCombatReinforcementList
                class="overflow-auto"
                :campaignId="props.campaignId"
                :combatId="props.combatId" />
        </TabsContent>
        <!-- <Card class="w-full max-h-full flex flex-col">
        <CardHeader>
            <span class="flex justify-between flex-wrap items-center">
                <CardTitle class="font-NovaCut text-gold"
                    >{{ store.combatQuery.data?.combat?.combatName }}
                </CardTitle>
                <template v-if="store.userIsDm">
                    <AsyncButton
                        v-if="store.combatIsOpen"
                        label="Start Combat"
                        loadingLabel="Starting..."
                        :icon="faFlag"
                        variant="outline"
                        class="interactable"
                        :click="combatControls.startCombat" />
                    <AsyncButton
                        v-else-if="store.combatIsStarted"
                        label="End Combat"
                        loadingLabel="Ending..."
                        :icon="faFlag"
                        variant="outline"
                        class="interactable"
                        :click="combatControls.finishCombat" />
                </template>
                <NuxtLink
                    v-if="store.combatIsFinished"
                    :to="{
                        name: 'app-campaigns-campaignId',
                        params: {
                            campaignId: props.campaignId,
                        },
                    }">
                    <Button variant="link"> Go Home</Button>
                </NuxtLink>
            </span>

            <CardDescription>
                <template
                    v-if="
                        store.combatQuery.data?.combat?.state ===
                        CombatState.Open
                    ">
                    Combat has not started yet. Players can add their characters
                    to the combat.
                </template>
                <template
                    v-else-if="
                        store.combatQuery.data?.combat?.state ===
                        CombatState.Started
                    ">
                    Round
                    {{ store.combatQuery.data?.combat?.roundNumber }}
                </template>
                <template v-else> This combat has ended.</template>
            </CardDescription>
        </CardHeader>
        <CardContent class="flex-1 h-full max-h-full overflow-auto">
            <Transition
                name="fade"
                mode="out-in">
                <section
                    v-show="
                        store.combatQuery.data?.combat?.state ===
                        CombatState.Started
                    "
                    class="flex flex-col gap-2 flex-1 overlfow-auto h-full max-h-full">
                    <header>
                        <div>
                            <FontAwesomeIcon :icon="faPersonMilitaryPointing" />
                            Reinformcements
                        </div>
                        <CardDescription>
                            Characters that can be added to the combat by the
                            DM.
                        </CardDescription>
                    </header>
                    <div class="flex-1 overflow-y-auto">
                        <CampaignCombatReinforcementList
                            class="overflow-auto"
                            :campaignId="props.campaignId"
                            :combatId="props.combatId" />
                    </div>
                    <div class="flex justify-end">
                        <Sheet
                            v-model:open="
                                sheetStates.addReinforcementsSheetOpen
                            ">
                            <SheetTrigger asChild>
                                <Button variant="link">
                                    <FontAwesomeIcon :icon="faPlusCircle" />
                                    Add
                                </Button>
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
                                    :campaignId="props.campaignId"
                                    :combatId="props.combatId"
                                    :userIsDm="store.userIsDm"
                                    :plannedStages="
                                        store.combatQuery.data?.combat
                                            ?.plannedStages!
                                    " />
                            </SheetContent>
                        </Sheet>
                    </div>
                </section>
            </Transition>
        </CardContent>
    </Card> -->
    </Tabs>
</template>
<script setup lang="ts">
    import {
        faFlag,
        faPersonMilitaryPointing,
        faPlusCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { CombatState } from "~/utils/types/models";
    const props = defineProps<{
        combatId: string;
        campaignId: string;
    }>();
    const store = useCombatStore();
    const combatControls = useCombatControls(() => props.combatId);

    // state
    const sheetStates = reactive({
        addReinforcementsSheetOpen: false,
    });
</script>

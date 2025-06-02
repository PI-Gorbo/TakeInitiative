<template>
    <Card class="w-full max-h-full flex flex-col">
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
                </section>
            </Transition>
        </CardContent>
    </Card>
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
</script>

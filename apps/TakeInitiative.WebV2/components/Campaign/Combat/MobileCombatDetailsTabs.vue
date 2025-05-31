<template>
    <Tabs
        defaultValue="Combat"
        v-model="currentTab"
        class="flex flex-col h-full gap-2">
        <div
            class="lg:hidden flex gap-1 justify-between"
            :class="{
                hidden: store.combatIsFinished,
            }">
            <TabsList>
                <TabsTrigger value="Combat">Combat</TabsTrigger>
                <TabsTrigger value="Reinforcements">Reinforcements</TabsTrigger>
            </TabsList>
        </div>

        <TabsContent
            value="Combat"
            class="mt-0 flex flex-col max-h-full data-[state=active]:h-full">
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
                        - Open to players.
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
            <section class="overflow-auto flex-1">
                <slot />
            </section>
        </TabsContent>
        <TabsContent
            value="Reinforcements"
            class="mt-0 data-[state=active]:h-full">
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
    const currentTab = ref("Combat");

    const screenSize = useScreenSize();
    watch(screenSize.isLargeScreen, () => {
        if (screenSize.isLargeScreen.value) {
            currentTab.value = "Combat";
        }
    });

    watch(
        () => store.combatIsFinished,
        (combatIsFinished) => {
            if (combatIsFinished) {
                currentTab.value = "Combat";
            }
        }
    );
</script>

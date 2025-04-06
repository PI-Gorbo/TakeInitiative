<template>
    <Collapsible :defaultOpen="true" class="w-full" v-slot="{ open }">
        <CollapsibleTrigger
            class="flex items-center justify-between gap-2 w-full">
            <div class="flex items-center gap-2">
                <CampaignCombatDraftStageNameForm
                    @click.prevent="(e) => e.stopPropagation()"
                    :initalStageName="props.stage.name"
                    :allStageNames="props.allStages.map((x) => x.name)"
                    :updateStageName="(name) => props.updateStage({ name })" />
                <Button
                    size="icon"
                    variant="link"
                    class="text-destructive"
                    @click.prevent="() => props.deleteStage()">
                    <FontAwesomeIcon :icon="faTrash" />
                </Button>
            </div>
            <ChevronDownIcon
                class="transition-transform duration-200"
                :class="open ? 'rotate-180' : ''" />
        </CollapsibleTrigger>
        <CollapsibleContent>
            <section class="flex flex-1 flex-col gap-2 py-2">
                <section v-for="npc in npcList" :key="npc.id">
                    <CampaignCombatDraftNpcDisplay
                        :npc="npc"
                        :editNpc="(request) => props.updateNpc(request)"
                        :deleteNpc="(request) => props.deleteNpc(request)" />
                </section>
                <div class="flex justify-end">
                    <Button variant="link" @click="addNpc">
                        <FontAwesomeIcon :icon="faPlusCircle" />
                        {{ !isAdding ? "Add Npc" : "Adding Npc..." }}
                    </Button>
                </div>
            </section>
        </CollapsibleContent>
    </Collapsible>
</template>

<script setup lang="ts">
    import { faPlusCircle, faTrash } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { ChevronDownIcon } from "lucide-vue-next";
    import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
    import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
    import type { UpdatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";
    import type { PlannedCombatStage } from "~/utils/types/models";

    const props = defineProps<{
        allStages: PlannedCombatStage[];
        stage: PlannedCombatStage;
        updateStage: (
            req: Omit<UpdatePlannedCombatStageRequest, "combatId" | "stageId">
        ) => Promise<any>;
        deleteStage: () => Promise<any>;
        createNpc: (
            request: Omit<CreatePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<any>;
        updateNpc: (
            request: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<any>;
        deleteNpc: (
            request: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<any>;
    }>();

    const npcList = computed(() =>
        props.stage.npcs.sort((npc1, npc2) => {
            // Sort Alphabetically, then by Id.
            if (npc1.name < npc2.name) return -1;
            if (npc1.name > npc2.name) return 1;
            if (npc1.id < npc2.id) return -1;
            if (npc1.id > npc2.id) return 1;
            return 0;
        })
    );

    const showCreatePlannedCharacterModal = () => {
        console.log("show planned character modal");
    };

    const isAdding = ref(false);
    async function addNpc() {
        isAdding.value = true;
        return await props
            .createNpc({
                name: "New NPC",
                armourClass: null,
                health: { "!": "None" },
                initiative: { roll: "1d20 + 2" },
                quantity: 1,
            })
            .finally(() => (isAdding.value = false));
    }
</script>

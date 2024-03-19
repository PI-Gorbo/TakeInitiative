import type { CreatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import type {
    PlannedCombat,
    PlannedCombatCharacter,
    PlannedCombatStage,
} from "./../utils/types/models";
import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
export const usePlannedCombatStore = defineStore("plannedCombatStore", () => {

    const campaignStore = useCampaignStore();

    const api = useApi();
    const state = reactive({
        plannedCombat: null as PlannedCombat | null | undefined,
    });

    function setPlannedCombat(combat: PlannedCombat | null | undefined) {
        state.plannedCombat = combat;
    }

    async function addStage(
        request: Omit<CreatePlannedCombatStageRequest, "combatId">,
    ) {
        return await api.plannedCombat.stage
            .create({
                ...request,
                combatId: state.plannedCombat?.id!,
            })
            .then(setPlannedCombat);
    }

    async function removeStage(stageId: string) {
        return await api.plannedCombat.stage
            .delete({
                combatId: state.plannedCombat?.id!,
                stageId: stageId,
            })
            .then(setPlannedCombat);
    }

    async function addNpc(
        stage: PlannedCombatStage,
        npc: Omit<CreatePlannedCombatNpcRequest, "combatId" | "stageId">,
    ) {
        return await api.plannedCombat.stage.npc
            .create({
                combatId: state.plannedCombat?.id!,
                stageId: stage.id,
                ...npc,
            })
            .then(setPlannedCombat);
    }

    async function removeNpc(stage: PlannedCombatStage, npcId: string) {
		return await api.plannedCombat.stage.npc
			.delete({
				combatId: state.plannedCombat?.id!,
				stageId: stage.id,
				npcId: npcId,
			}).then(setPlannedCombat)
	}

	async function updateNpc(stage: PlannedCombatStage, npc: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">) {
		return await api.plannedCombat.stage.npc
			.update({
                ...npc,
				armourClass: npc.armourClass,
				combatId: state.plannedCombat?.id!,
                stageId: stage.id,
			}).then(setPlannedCombat)
	}

    async function createOpenCombat() {

        return api.combat.open({ plannedCombatId: state.plannedCombat?.id! })
            .then(() => campaignStore.setCampaignById(state.plannedCombat?.campaignId))
            .then(() => state.plannedCombat = null)
    }

    return {
        selectedPlannedCombat: computed(() => state.plannedCombat),
        setPlannedCombat,
        addStage,
        removeStage,
        addNpc,
		removeNpc,
		updateNpc,
        createOpenCombat
    };
});

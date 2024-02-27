import type { CreatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import type {
    PlannedCombat,
    PlannedCombatNonPlayerCharacter,
    PlannedCombatStage,
} from "./../utils/types/models";
export const usePlannedCombatStore = defineStore("plannedCombatStore", () => {
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

    async function addNpc(
        stage: PlannedCombatStage,
        npc: PlannedCombatNonPlayerCharacter,
    ) {}

    async function removeStage(stageId: string) {
		return await api.plannedCombat.stage.delete({
			combatId: state.plannedCombat?.id!,
			stageId: stageId
		}).then(setPlannedCombat)
	}

    async function removeNpc(stage: PlannedCombatStage, npcId: string) {}

    return {
        selectedPlannedCombat: computed(() => state.plannedCombat),
        setPlannedCombat,
        addStage,
		removeStage
    };
});

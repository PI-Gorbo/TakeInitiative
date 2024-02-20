import type { PlannedCombat, PlannedCombatNonPlayerCharacter, PlannedCombatStage } from './../utils/types/models';
export const usePlannedCombatStore = defineStore("plannedCombatStore", () => {

	const state = reactive({
		plannedCombat: null as PlannedCombat | null
	})	

	function setPlannedCombat(combat: PlannedCombat | null) {
		state.plannedCombat = combat
	}

	async function addStage(stage: PlannedCombatStage) {

	}

	async function addNpc(stage: PlannedCombatStage, npc: PlannedCombatNonPlayerCharacter) {

	}

	async function removeStage(stageId: string) {

	}

	async function removeNpc(stage: PlannedCombatStage, npcId: string) {

	}

	return {
		selectedPlannedCombat: computed(() => state.plannedCombat),
		setPlannedCombat,
		
	}
})
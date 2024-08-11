import type {
    GetCombatsResponse,
    PlannedCombat,
    PlannedCombatStage,
    PostPlannedCombatNpcRequest,
    PostPlannedCombatRequest,
    PutPlannedCombatNpcRequest,
    PutPlannedCombatStageRequest,
} from "base/utils/api/api";

export const useCampaignCombatsStore = defineStore(
    "campaignCombatsStore",
    () => {
        const api = useApi();

        const state = reactive<
            GetCombatsResponse & {
                campaignId: string | undefined;
                selectedCombat:
                    | {
                          type: "Planned" | "Normal";
                          id: string;
                      }
                    | undefined;
                selectedCombatHistory: undefined | object;
            }
        >({
            campaignId: undefined,
            combats: undefined,
            plannedCombats: undefined,
            selectedCombat: undefined,
            selectedCombatHistory: undefined,
        });

        function init(campaignId: string) {
            return api.combat
                .getAll({
                    campaignId: campaignId,
                })
                .then((resp) => {
                    state.campaignId = campaignId;
                    Object.keys(resp).forEach((key) => {
                        // @ts-ignore
                        state[key] = resp[key];
                    });
                });
        }

        function refetchCombatInfo() {
            return init(state.campaignId!);
        }

        function unselectCombat() {
            state.selectedCombat = undefined;
        }

        async function selectCombat(combatId: string) {
            state.selectedCombat = {
                id: combatId,
                type: "Normal",
            };
            const result = await api.combat.getHistory({
                id: combatId,
            });
            state.selectedCombatHistory = result;
        }

        function selectPlannedCombat(combatId: string) {
            state.selectedCombatHistory = undefined;
            state.selectedCombat = {
                id: combatId,
                type: "Planned",
            };
        }

        function updatePlannedCombat(combat: PlannedCombat) {
            const index = state.plannedCombats?.findIndex(
                (x) => x.id == combat.id,
            );
            state.plannedCombats![index!] = combat;
        }

        // Planned Combats
        async function createPlannedCombat(
            request: Omit<PostPlannedCombatRequest, "campaignId">,
        ): Promise<PlannedCombat> {
            return await api.plannedCombat
                .create({
                    ...request,
                    campaignId: state.campaignId!,
                })
                .then((pc) => {
                    state.plannedCombats?.push(pc);
                    selectPlannedCombat(pc.id!);
                    return pc;
                });
        }

        async function deletePlannedCombat(plannedCombatId: string) {
            return await api.plannedCombat
                .delete({
                    campaignId: state.campaignId!,
                    combatId: plannedCombatId,
                })
                .then(() => {
                    state.plannedCombats = state.plannedCombats?.filter(
                        (x) => x.id != plannedCombatId,
                    );

                    if (state.selectedCombat?.id == plannedCombatId) {
                        unselectCombat();
                    }
                });
        }

        async function addStage(
            request: Omit<PutPlannedCombatStageRequest, "combatId">,
        ) {
            return await api.plannedCombat.stage
                .create({
                    ...request,
                    combatId: state.selectedCombat?.id!,
                })
                .then(updatePlannedCombat);
        }

        async function removeStage(stageId: string) {
            return await api.plannedCombat.stage
                .delete({
                    combatId: state.selectedCombat?.id!,
                    stageId: stageId,
                })
                .then(updatePlannedCombat);
        }

        async function addNpc(
            stage: PlannedCombatStage,
            npc: Omit<PostPlannedCombatNpcRequest, "combatId" | "stageId">,
        ) {
            return await api.plannedCombat.stage.npc
                .create({
                    combatId: state.selectedCombat?.id!,
                    stageId: stage.id,
                    ...npc,
                })
                .then(updatePlannedCombat);
        }

        async function removeNpc(stage: PlannedCombatStage, npcId: string) {
            return await api.plannedCombat.stage.npc
                .delete({
                    combatId: state.selectedCombat?.id!,
                    stageId: stage.id,
                    npcId: npcId,
                })
                .then(updatePlannedCombat);
        }

        async function updateNpc(
            stage: PlannedCombatStage,
            npc: Omit<PutPlannedCombatNpcRequest, "combatId" | "stageId">,
        ) {
            return await api.plannedCombat.stage.npc
                .update({
                    ...npc,
                    armourClass: npc.armourClass,
                    combatId: state.selectedCombat?.id!,
                    stageId: stage.id,
                })
                .then(updatePlannedCombat);
        }

        async function updateStage(
            req: Omit<PutPlannedCombatStageRequest, "combatId">,
        ) {
            const request = {
                combatId: state.selectedCombat?.id!,
                stageId: req.stageId,
                name: req.name,
            };
            return api.plannedCombat.stage
                .update(request)
                .then(updatePlannedCombat);
        }

        return {
            state,
            init,
            refetchCombatInfo,
            selectCombat,
            selectPlannedCombat,
            unselectCombat,
            hasAnyPlannedCombats: computed(
                () => (state.plannedCombats?.length ?? 0) > 0,
            ),
            hasAnyCombats: computed(() => (state.combats?.length ?? 0) > 0),
            selectedPlannedCombat: computed(() => {
                if (
                    state.selectedCombat == undefined ||
                    state.selectedCombat.type != "Planned"
                ) {
                    return undefined;
                }

                return state.plannedCombats?.find(
                    (x) => x.id == state.selectedCombat?.id,
                );
            }),
            selectedCombat: computed(() => {
                if (
                    state.selectedCombat == undefined ||
                    state.selectedCombat.type != "Normal"
                ) {
                    return undefined;
                }

                return state.combats?.find(
                    (x) => x.combatId == state.selectedCombat?.id,
                );
            }),
            addStage,
            removeStage,
            updateStage,
            addNpc,
            removeNpc,
            updateNpc,
            createPlannedCombat,
            deletePlannedCombat,
        };
    },
);

import type { CreatePlannedCombatStageRequest } from "../utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import type {
    PlannedCombat,
    PlannedCombatCharacter,
    PlannedCombatStage,
} from "../utils/types/models";
import type { CreatePlannedCombatNpcRequest } from "../utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "../utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import type { UpdatePlannedCombatStageRequest } from "../utils/api/plannedCombat/stages/updatePlannedCombatStageRequest";
import type { GetCombatsResponse } from "../utils/api/combat/getCombatsRequest";
import type { CreatePlannedCombatRequest } from "../utils/api/plannedCombat/createPlannedCombatRequest";
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
            }
        >({
            campaignId: undefined,
            combats: [],
            plannedCombats: [],
            selectedCombat: undefined,
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
        }

        async function selectPlannedCombat(combatId: string) {
            state.selectedCombat = {
                id: combatId,
                type: "Planned",
            };


        }

        function updatePlannedCombat(combat: PlannedCombat) {
            const index = state.plannedCombats?.findIndex(
                (x) => x.id == combat.id,
            );
            //@ts-expect-error
            state.plannedCombats![index!] = combat;
        }

        // Planned Combats
        async function createPlannedCombat(
            request: Omit<CreatePlannedCombatRequest, "campaignId">
        ): Promise<PlannedCombat> {
            return await api.draftCombat
                .create({
                    ...request,
                    campaignId: state.campaignId!,
                })
                .then((pc) => {
                    //@ts-expect-error
                    state.plannedCombats?.push(pc);
                    selectPlannedCombat(pc.id);
                    return pc;
                });
        }

        async function deletePlannedCombat(plannedCombatId: string) {
            return await api.draftCombat
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
            request: Omit<CreatePlannedCombatStageRequest, "combatId">,
        ) {
            return await api.draftCombat.stage
                .create({
                    ...request,
                    combatId: state.selectedCombat?.id!,
                })
                .then(updatePlannedCombat);
        }

        async function removeStage(stageId: string) {
            return await api.draftCombat.stage
                .delete({
                    combatId: state.selectedCombat?.id!,
                    stageId: stageId,
                })
                .then(updatePlannedCombat);
        }

        async function addNpc(
            stage: PlannedCombatStage,
            npc: Omit<CreatePlannedCombatNpcRequest, "combatId" | "stageId">,
        ) {
            return await api.draftCombat.stage.npc
                .create({
                    combatId: state.selectedCombat?.id!,
                    stageId: stage.id,
                    ...npc,
                })
                .then(updatePlannedCombat);
        }

        async function removeNpc(stage: PlannedCombatStage, npcId: string) {
            return await api.draftCombat.stage.npc
                .delete({
                    combatId: state.selectedCombat?.id!,
                    stageId: stage.id,
                    npcId: npcId,
                })
                .then(updatePlannedCombat);
        }

        async function updateNpc(
            stage: PlannedCombatStage,
            npc: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">,
        ) {
            return await api.draftCombat.stage.npc
                .update({
                    ...npc,
                    armourClass: npc.armourClass,
                    combatId: state.selectedCombat?.id!,
                    stageId: stage.id,
                })
                .then(updatePlannedCombat);
        }

        async function updateStage(
            req: Omit<UpdatePlannedCombatStageRequest, "combatId">,
        ) {
            const request = {
                combatId: state.selectedCombat?.id!,
                stageId: req.stageId,
                name: req.name,
            };
            return api.draftCombat.stage
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

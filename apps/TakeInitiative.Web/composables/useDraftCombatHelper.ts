import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
import { createDraftCombatMutation, openCombatToPlayersMutation } from "~/utils/queries/combats";

export const useDraftCombatHelper = () => {
    const _createDraftCombatMutation = createDraftCombatMutation();
    async function createDraftCombat(
        request: CreatePlannedCombatRequest,
        startImmidately: boolean
    ) {
        return await _createDraftCombatMutation
            .mutateAsync(request)
            .then(async (pc) => {
                await navigateTo({
                    name: "app-campaigns-campaignId-combats-drafts-draftCombatId",
                    params: {
                        campaignId: request.campaignId,
                        draftCombatId: pc.id,
                    },
                });
                return pc;
            })
            .then(async (pc) => {
                if (startImmidately) {
                    await openDraftCombat(request.campaignId, pc?.id);
                }
            });
    }

    const _openDraftCombatMutation = openCombatToPlayersMutation()
    async function openDraftCombat(campaignId: string, plannedCombatId: string) {
        return await _openDraftCombatMutation
            .mutateAsync({ plannedCombatId })
            .then(async (combat) => {
                await navigateTo({
                    name: 'app-campaigns-campaignId-combats-combatId',
                    params: {
                        campaignId: campaignId,
                        combatId: combat.combat.id
                    }
                })
            })
    }

    return {
        createDraftMutation: _createDraftCombatMutation,
        openDraftMutation: _openDraftCombatMutation,
        createDraftCombat,
        openDraftCombat
    }
}

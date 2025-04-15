import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
import { createDraftCombatMutation } from "~/utils/queries/combats";

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
                    await openDraftCombat(pc?.id);
                }
            });
    }

    async function openDraftCombat(plannedCombatId: string) {
        // return campaignStore
        //     .openCombat(plannedCombatId)
        //     .then((c) =>
        //         Promise.resolve(
        //             useNavigator().toCombat(
        //                 campaignStore.state.campaign?.id!,
        //                 campaignStore.state.currentCombatInfo?.id!
        //             )
        //         )
        //     );
    }

    return {
        mutation: _createDraftCombatMutation,
        createDraftCombat,
        openDraftCombat
    }
}

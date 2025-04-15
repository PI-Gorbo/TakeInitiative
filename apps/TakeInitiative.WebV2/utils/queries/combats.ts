import { queryOptions, useMutation, useQueryClient } from "@tanstack/vue-query";
import type { ShallowRef } from "vue";
import type { GetCombatsResponse } from "../api/combat/getCombatsRequest";
import type { RefOrGetter } from "./utils";
import type { CreatePlannedCombatRequest } from "../api/plannedCombat/createPlannedCombatRequest";

export const getDraftCombatQueryKey = (campaignId: MaybeRefOrGetter<string>, draftCombatId: MaybeRefOrGetter<string>) => [campaignId, 'combats', 'draft', draftCombatId]
export const getDraftCombatQuery = (campaignId: RefOrGetter<string>, draftComabtId: RefOrGetter<string>) => queryOptions({
    queryKey: getDraftCombatQueryKey(campaignId, draftComabtId),
    queryFn: () => useApi().draftCombat.get({ campaignId: toValue(campaignId), combatId: toValue(draftComabtId) }),
    enabled: () => !!toValue(campaignId) && !!toValue(draftComabtId),
    staleTime: 1000 * 60 * 5, // 5 minutes
})

export const getAllCombatsQueryKey = (campaignId: MaybeRefOrGetter<string>) => [campaignId, 'combats', 'all']
export const getAllCombatsQuery = (campaignId: RefOrGetter<string>) => queryOptions({
    queryKey: [campaignId, 'combats', 'all'],
    queryFn: () => useApi().combat.getAll({ campaignId: toValue(campaignId) }),
    enabled: () => !!toValue(campaignId),
    select: (data) => {
        data.combats.sort(sortByFinishedTimestamp);
        return data;
    },
    staleTime: 1000 * 60 * 5, // 5 minutes
})

export const createDraftCombatMutation = () => {
    const client = useQueryClient();
    return useMutation({
        mutationFn: (req: CreatePlannedCombatRequest) => useApi().draftCombat.create(req),
        onSuccess: (data, variables) => {
            client.setQueryData(getDraftCombatQueryKey(variables.campaignId, data.id), data);
            client.invalidateQueries({
                queryKey: getAllCombatsQueryKey(variables.campaignId)
            });
        }
    });
}

function sortByFinishedTimestamp(
    a: GetCombatsResponse["combats"][number],
    b: GetCombatsResponse["combats"][number]
) {
    if (a.finishedTimestamp == null && b.finishedTimestamp != null) {
        return -1;
    }

    if (a.finishedTimestamp != null && b.finishedTimestamp == null) {
        return 1;
    }

    if (a.finishedTimestamp == b.finishedTimestamp) {
        return 0;
    }

    if (a.finishedTimestamp! < b.finishedTimestamp!) {
        return -1;
    }

    if (a.finishedTimestamp! > b.finishedTimestamp!) {
        return 1;
    }

    return 0;
}


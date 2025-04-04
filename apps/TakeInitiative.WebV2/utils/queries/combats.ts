import { queryOptions } from "@tanstack/vue-query";
import type { ShallowRef } from "vue";
import type { GetCombatsResponse } from "../api/combat/getCombatsRequest";
import type { RefOrGetter } from "./utils";

export const combatQueries = {
    getAllCombatsQuery: (campaignId: RefOrGetter<string>) => queryOptions({
        queryKey: [campaignId, 'combats', 'all'],
        queryFn: () => useApi().combat.getAll({ campaignId: toValue(campaignId) }),
        enabled: () => !!toValue(campaignId),
        select: (data) => {
            data.combats.sort(sortByFinishedTimestamp);
            return data;
        },
    })
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
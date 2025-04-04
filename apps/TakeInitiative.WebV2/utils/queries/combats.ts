import { queryOptions } from "@tanstack/vue-query";
import type { ShallowRef } from "vue";

type RefOrGetter<T> = ShallowRef<T> | WritableComputedRef<T> | Ref<T> | ComputedRef<T> | (() => T)
export const combatQueries = {
    getAllCombatsQuery: (campaignId: RefOrGetter<string>) => queryOptions({
        queryKey: [campaignId, 'combats', 'all'],
        queryFn: () => useApi().combat.getAll({ campaignId: toValue(campaignId) }),
        enabled: () => !!toValue(campaignId)
    })
}
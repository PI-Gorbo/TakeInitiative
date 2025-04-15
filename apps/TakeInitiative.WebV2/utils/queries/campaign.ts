import { queryOptions } from "@tanstack/vue-query";
import type { RefOrGetter } from "./utils";

export const getCampaignQueryKey = (campaignId: MaybeRefOrGetter<string>) => ["campaign", campaignId];
export const getCampaignQuery = (campaign: RefOrGetter<string>) => queryOptions({
    queryKey: getCampaignQueryKey(campaign),
    queryFn: () => useApi().campaign.get({ campaignId: toValue(campaign) }),
    enabled: () => !!toValue(campaign),
    staleTime: 1000 * 60 * 5, // 5 minutes
})
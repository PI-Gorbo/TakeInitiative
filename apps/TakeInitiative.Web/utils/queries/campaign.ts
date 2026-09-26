import { queryOptions, useMutation, useQueryClient } from "@tanstack/vue-query";
import type { RefOrGetter } from "./utils";

export const getCampaignQueryKey = (
    campaignId: MaybeRefOrGetter<string | null>
) => ["campaign", campaignId];
export const getCampaignQuery = (campaign: RefOrGetter<string | null>, onError: (() => Promise<unknown>) | undefined = undefined) =>
    queryOptions({
        queryKey: getCampaignQueryKey(campaign),
        queryFn: () =>
            useApi().campaign.get({ campaignId: toValue(campaign)! }).catch(async err => {
                if (onError) {
                    await onError();
                }
                throw err;
            }),
        enabled: () => !!toValue(campaign),
        staleTime: 1000 * 60 * 5, // 5 minutes,
    });

// The caller's campaigns (GET /api/campaigns).
export const getCampaignsQueryKey = () => ["campaigns"];
export const getCampaignsQuery = () =>
    queryOptions({
        queryKey: getCampaignsQueryKey(),
        queryFn: () => useApi().campaign.list(),
        staleTime: 1000 * 60 * 5, // 5 minutes
    });

// Owner only: change a member's role.
export const putMemberRoleMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.campaign.putMemberRole,
        onSuccess: (campaign) => {
            queryClient.setQueryData(getCampaignQueryKey(campaign.id), campaign);
        },
    });
};

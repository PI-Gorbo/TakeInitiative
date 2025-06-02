import { queryOptions, useMutation, useQueryClient } from "@tanstack/vue-query";
import type { RefOrGetter } from "./utils";
import type { CampaignMemberResource } from "../types/models";
import type { GetCampaignResponse } from "../api/campaign/getCampaignRequest";

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

export const updateCampaignDetailsMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.campaign.update,
        onSuccess: (data, request) => {
            queryClient.setQueryData(
                getCampaignQueryKey(data.id),
                (oldData: GetCampaignResponse) => {
                    return {
                        ...oldData,
                        campaign: data,
                    } satisfies GetCampaignResponse;
                }
            );
        },
    });
};

export const createCharacterMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: api.campaign.playerCharacters.create,
        onSuccess: (createCampaignMember, request) => {
            queryClient.setQueryData(
                getCampaignQueryKey(createCampaignMember.campaignId),
                (oldData: GetCampaignResponse) => {
                    return {
                        ...oldData,
                        userCampaignMember: createCampaignMember,
                    } satisfies GetCampaignResponse;
                }
            );
        },
    });
};

export const editCharacterMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.campaign.playerCharacters.update,
        onSuccess: (newUserCampaignMember, request) => {
            queryClient.setQueryData(
                getCampaignQueryKey(newUserCampaignMember.campaignId),
                (oldData: GetCampaignResponse) => {
                    return {
                        ...oldData,
                        userCampaignMember: newUserCampaignMember,
                    } satisfies GetCampaignResponse;
                }
            );
        },
    });
};

export const deleteCharacterMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.campaign.playerCharacters.delete,
        onSuccess: (data, request) => {
            queryClient.setQueryData(
                getCampaignQueryKey(data.campaignId),
                (oldData: GetCampaignResponse) => {
                    return {
                        ...oldData,
                        userCampaignMember: data,
                    } satisfies GetCampaignResponse;
                }
            );
        },
    });
};

export const setResourceMutation = () => {
    const api = useApi();
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.campaign.member.setResources,
        onSuccess: (data, request) => {
            queryClient.setQueryData(
                getCampaignQueryKey(data.campaignId),
                (oldData: GetCampaignResponse) => {
                    return {
                        ...oldData,
                        userCampaignMember: data,
                    } satisfies GetCampaignResponse;
                }
            );
        },
    });
};

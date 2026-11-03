import { useQuery, useQueryClient } from "@tanstack/vue-query";
import type { CreateCampaignRequest } from "../utils/api/campaign/createCampaignRequest";
import type { JoinCampaignRequest } from "../utils/api/campaign/joinCampaignRequest";
import type { GetUserResponse } from "../utils/api/user/getUserRequest";
import type { Campaign } from "../utils/api/types";
import { getUserQuery, getUserQueryKey } from "~/utils/queries/user";
import { getCampaignsQuery, getCampaignsQueryKey } from "~/utils/queries/campaign";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {

    const queryClient = useQueryClient()
    const userDetails = useQuery(getUserQuery())
    const state = computed(() => userDetails.data.value)

    // Stores
    const api = useApi();

    // Computed
    const username = computed(() => userDetails.data.value?.username);

    // The caller's campaigns, from the Campaign projection (GET /api/campaigns).
    const campaignsQuery = useQuery({
        ...getCampaignsQuery(),
        enabled: () => userDetails.data.value != null,
    });

    const campaignList = computed(() => campaignsQuery.data.value?.campaigns ?? []);
    const campaignCount = computed(() => campaignList.value.length);

    /** Loads the caller's campaigns, waiting for the request if needed. */
    async function fetchCampaigns() {
        const data = await queryClient.fetchQuery(getCampaignsQuery());
        return data.campaigns;
    }

    async function refetchCampaigns() {
        await queryClient.invalidateQueries({ queryKey: getCampaignsQueryKey() });
    }

    // Mutations
    /** Resolves who the caller is, from cache when it is already known. */
    async function init(): Promise<void> {
        await queryClient.ensureQueryData(getUserQuery());
    }

    const isLoggedIn = computed(() => userDetails.data.value != null)

    /** Signs out of SamStack, which comes back to the homepage. */
    async function logout(): Promise<void> {
        // Clears the password sign-in's cookie too, or the API would still let them in with it.
        await api.user.logout().catch(() => {});
        queryClient.setQueryData(getUserQueryKey(), () => null);
        queryClient.removeQueries({ queryKey: getCampaignsQueryKey() });
        signOut(new URL("/", window.location.origin).href);
    }

    async function createCampaign(
        request: CreateCampaignRequest
    ): Promise<Campaign> {
        return await api.campaign
            .create(request)
            .then((campaign) => refetchCampaigns().then(() => campaign));
    }

    async function joinCampaign(
        request: JoinCampaignRequest
    ): Promise<Campaign> {
        return await api.campaign
            .join(request)
            .then((campaign) => refetchCampaigns().then(() => campaign));
    }

    async function navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin() {
        if (userDetails.data.value == null) {
            return;
        }

        // Get the first campaign available
        const campaign = (await fetchCampaigns())[0];

        if (campaign == null) {
            return useNavigator().toCreateOrJoinCampaign();
        }

        return useNavigator().toCampaign(campaign.id);
    }

    // Helper functions
    return {
        state,
        init,
        isLoggedIn,
        createCampaign,
        fetchCampaigns,
        logout,
        joinCampaign,
        username,
        campaignCount,
        campaignList,
        navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin,

    };
});

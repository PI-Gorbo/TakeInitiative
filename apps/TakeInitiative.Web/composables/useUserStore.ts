import { useQuery, useQueryClient } from "@tanstack/vue-query";
import type { CreateCampaignRequest } from "../utils/api/campaign/createCampaignRequest";
import type { JoinCampaignRequest } from "../utils/api/campaign/joinCampaignRequest";
import type { GetUserResponse } from "../utils/api/user/getUserRequest";
import type { LoginRequest } from "../utils/api/user/loginRequest";
import type { SignUpRequest } from "../utils/api/user/signUpRequest";
import type { Campaign } from "../utils/types/models";
import { getUserQuery, getUserQueryKey } from "~/utils/queries/user";
import { getCampaignsQuery, getCampaignsQueryKey } from "~/utils/queries/campaign";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {

    const queryClient = useQueryClient()
    const userDetails = useQuery({ ...getUserQuery(), retry: false })
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
    async function init(): Promise<void> {
        await fetchUser()
    }

    async function fetchUser(): Promise<User> {
        // fetch the user.
        await userDetails.refetch();
        return userDetails.data.value!
    }

    const isLoggedIn = computed(() => userDetails.data.value != null)

    async function login(request: LoginRequest): Promise<void> {
        await api.user.login(request).then(async () => {
            return await fetchUser();
        });
    }

    async function signUp(signUpRequest: SignUpRequest): Promise<unknown> {
        return await api.user.signUp(signUpRequest).then(fetchUser);
    }

    async function confirmEmail(code: string): Promise<unknown> {
        return await api.user
            .confirmEmailWithToken(code)
            .then((user) => (userDetails.data.value = user));
    }

    async function logout(): Promise<void> {
        await api.user
            .logout()
            .then(() => {
                queryClient.setQueryData(getUserQueryKey(), () => null)
                queryClient.removeQueries({ queryKey: getCampaignsQueryKey() })
            })
            .then(async () => await navigateTo("/login"))
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
        refetchUser: fetchUser,
        ConfirmEmail: confirmEmail,
        login,
        signUp,
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

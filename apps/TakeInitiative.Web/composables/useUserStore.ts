import { useQuery, useQueryClient } from "@tanstack/vue-query";
import type { CreateCampaignRequest } from "../utils/api/campaign/createCampaignRequest";
import type { DeleteCampaignRequest } from "../utils/api/campaign/deleteCampaignRequest";
import type { JoinCampaignRequest } from "../utils/api/campaign/joinCampaignRequest";
import type { GetUserResponse } from "../utils/api/user/getUserRequest";
import type { LoginRequest } from "../utils/api/user/loginRequest";
import type { SignUpRequest } from "../utils/api/user/signUpRequest";
import type { Campaign } from "../utils/types/models";
import { getUserQuery, getUserQueryKey } from "~/utils/queries/user";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {

    const queryClient = useQueryClient()
    const userDetails = useQuery({ ...getUserQuery(), retry: false })
    const state = computed(() => userDetails.data.value)

    // Stores
    const api = useApi();

    // Computed
    const username = computed(() => userDetails.data.value?.username);

    const campaignCount = computed(() => {
        if (userDetails.data.value == null) {
            return 0;
        }

        return (
            userDetails.data.value.dmCampaigns.length + userDetails.data.value.memberCampaigns.length
        );
    });

    const campaignList = computed(() => {
        return userDetails.data.value?.dmCampaigns
            .map((campaign) => ({ ...campaign, isDm: true }))
            .concat(
                userDetails.data.value?.memberCampaigns.map((c) => ({
                    ...c,
                    isDm: false,
                }))
            );
    });

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
            })
            .then(async () => await navigateTo("/login"))
    }

    async function createCampaign(
        request: CreateCampaignRequest
    ): Promise<Campaign> {
        return await api.campaign
            .create(request)
            .then((campaign) => fetchUser().then(() => campaign));
    }

    async function joinCampaign(
        request: JoinCampaignRequest
    ): Promise<Campaign> {
        return await api.campaign
            .join(request)
            .then((campaign) => fetchUser().then(() => campaign));
    }

    async function deleteCampaign(
        request: DeleteCampaignRequest
    ): Promise<any> {
        return await api.campaign
            .delete(request)
            .then(fetchUser)
            .then(async () => {
                debugger;
                if ((campaignList.value?.length ?? 0) == 0) {
                    return await useNavigator().toCreateOrJoinCampaign();
                }

                return await useNavigator().toCampaignsList()
            });
    }

    function navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin() {
        if (userDetails.data.value == null) {
            return;
        }

        // Get the first campaign available
        const campaign = userDetails.data.value?.memberCampaigns.concat(
            userDetails.data.value?.dmCampaigns
        )[0];

        if (campaign == null) {
            return useNavigator().toCreateOrJoinCampaign();
        }

        return useNavigator().toCampaign(campaign.campaignId);
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
        deleteCampaign,
        logout,
        joinCampaign,
        username,
        campaignCount,
        campaignList,
        navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin,

    };
});

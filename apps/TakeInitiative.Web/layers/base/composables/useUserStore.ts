import type { GetUserResponse } from "base/utils/api/TakeApiClient";
import type { CreateCampaignRequest } from "../utils/api/campaign/createCampaignRequest";
import type { DeleteCampaignRequest } from "../utils/api/campaign/deleteCampaignRequest";
import type { JoinCampaignRequest } from "../utils/api/campaign/joinCampaignRequest";
import type { UpdateCampaignDetailsRequest } from "../utils/api/campaign/updateCampaignDetailsRequest";
import type { LoginRequest } from "../utils/api/user/loginRequest";
import type { SignUpRequest } from "../utils/api/user/signUpRequest";
import type { Campaign } from "../utils/types/models";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {
    // Stores
    const api = useApi();

    // State
    const state = reactive({
        user: null as User | null,
    });

    // Computed
    const username = computed(() => state.user?.username);

    const campaignCount = computed(() => {
        if (
            state.user == null ||
            state.user.dmCampaigns == null ||
            state.user.memberCampaigns == null
        ) {
            return 0;
        }

        return (
            state.user.dmCampaigns.length + state.user.memberCampaigns.length
        );
    });

    const campaignList = computed(() => {
        if (state.user == null) {
            return [];
        }

        return state.user
            .dmCampaigns!.map((campaign) => ({ ...campaign, isDm: true }))
            .concat(
                state.user.memberCampaigns!.map((c) => ({
                    ...c,
                    isDm: false,
                })),
            );
    });

    // Mutations
    async function init(): Promise<void> {
        return await isLoggedIn().then(() => {});
    }

    async function fetchUser(): Promise<User> {
        // fetch the user.k
        const userData = await api.user.getUser();
        state.user = userData;
        return userData;
    }

    async function isLoggedIn(): Promise<Boolean> {
        if (state.user != null) {
            return true;
        }

        return await fetchUser()
            .then(() => true)
            .catch((error) => {
                return false;
            });
    }

    async function login(request: LoginRequest): Promise<void> {
        await api.user.login(request).then(async () => {
            return await fetchUser();
        });
    }

    async function signUp(
        signUpRequest: SignUpRequest,
        redirectPath: string | null,
    ): Promise<unknown> {
        return await api.user.signUp(signUpRequest).then(async () => {
            if (redirectPath != null) {
                return await navigateTo(redirectPath);
            } else {
                return await navigateToFirstAvailableCampaign();
            }
        });
    }

    async function confirmEmail(code: string): Promise<unknown> {
        return await api.user
            .confirmEmailWithToken(code)
            .then((user) => (state.user = user));
    }

    async function logout(): Promise<void> {
        await api.user
            .logout()
            .then(() => {
                state.user = null;
            })
            .then(async () => await navigateTo("/login"));
    }

    async function createCampaign(
        request: CreateCampaignRequest,
    ): Promise<Campaign> {
        return await api.campaign
            .create(request)
            .then((campaign) => fetchUser().then(() => campaign));
    }

    async function joinCampaign(
        request: JoinCampaignRequest,
    ): Promise<Campaign> {
        return await api.campaign
            .join(request)
            .then((campaign) => fetchUser().then(() => campaign));
    }

    async function deleteCampaign(
        request: DeleteCampaignRequest,
    ): Promise<any> {
        return await api.campaign
            .delete(request)
            .then(fetchUser)
            .then(async () => {
                console.log(campaignList.value);
                if ((campaignList.value?.length ?? 0) == 0) {
                    console.log("Navigating to create or join page");
                    return useNavigator().toCreateOrJoinCampaign();
                }

                // Check if its the campaigns route.
                const route = useRoute();
                const isCampaignRoute = route.name
                    ?.toString()
                    .startsWith("campaign-id");
                if (!isCampaignRoute) {
                    return;
                }

                // Check if the campaign the user is viewing is the
                // campaign that is being deleted
                const id = route.params.id as string;
                if (id != request.campaignId) {
                    return;
                }

                if ((campaignList.value?.length ?? 0) > 0) {
                    return useNavigator().toCampaignTab(
                        campaignList.value![0].campaignId!,
                        "summary",
                    );
                }
            });
    }

    function navigateToFirstAvailableCampaign() {
        if (state.user == null) {
            return;
        }

        // Get the first campaign available
        const campaign = state.user.memberCampaigns!.concat(
            state.user.dmCampaigns!,
        )[0];

        if (campaign == null) {
            return useNavigator().toCreateOrJoinCampaign();
        }

        return useNavigator().toCampaignTab(campaign.campaignId!, "summary");
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
        navigateToFirstAvailableCampaign,
    };
});

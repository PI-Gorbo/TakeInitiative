import type { SignUpRequest } from "~/utils/api/user/signUpRequest";
import type { GetUserResponse } from "~/utils/api/user/getUserRequest";
import type { Campaign } from "~/utils/types/models";
import { type CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import type { LoginRequest } from "~/utils/api/user/loginRequest";
import type { UpdateCampaignDetailsRequest } from "~/utils/api/campaign/updateCampaignDetailsRequest";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {
    const api = useApi();

    const state = reactive({
        user: null as User | null,
    });

    async function init(): Promise<void> {
        return await isLoggedIn().then();
    }

    async function fetchUser(): Promise<User> {
        // fetch the user.
        return await api.user.getUser().then((user) => (state.user = user));
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
        return await api.user
            .login(request)
            .then(async () => await fetchUser)
            .then();
    }

    async function signUp(signUpRequest: SignUpRequest): Promise<void> {
        return await api.user.signUp(signUpRequest).then(async () => {
            await navigateTo("/");
        });
    }

    async function signOut(): Promise<void> {
        await navigateTo("/login");
    }

    async function createCampaign(
        request: CreateCampaignRequest,
    ): Promise<Campaign> {
        return await api.campaign
            .create(request)
            .then((campaign) => fetchUser().then(() => campaign));
    }

	async function updateCampaign(
        request: UpdateCampaignDetailsRequest,
    ): Promise<Campaign> {
        return await api.campaign
            .update(request)
            .then((campaign) => fetchUser().then(() => campaign));
    }


    // Helper functions
    return {
        state,
        init,
        login,
        signUp,
        isLoggedIn,
        createCampaign,
		updateCampaign,
        signOut,
        username: computed(() => state.user?.username),
    };
});

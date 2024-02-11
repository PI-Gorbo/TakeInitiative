import { useTakeInitApi } from "~/utils/api/takeInitaitiveApi";
import type { SignUpRequest } from "~/utils/api/user/signUpRequest";
import type { GetUserResponse } from "~/utils/api/user/getUserRequest";
import type { Campaign } from "~/utils/types/models";
import { type CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {
    const api = useTakeInitApi();

    const state = reactive({
        user: null as User | null,
    });

	async function init() : Promise<void> {
		return await isLoggedIn().then();
	}

    async function fetchUser(): Promise<User> {
        // fetch the user.
        return api.user.getUser().then((user) => (state.user = user));
    }

    async function isLoggedIn(): Promise<Boolean> {
        const jwt = jwtUtils.getJwt();
        if (jwt == false) {
            return false;
        }

        if (state.user == null || state.user.userId != jwt.UserId) {
            return await fetchUser().then(() => true);
        }

        return true;
    }

    async function login(email: string, password: string): Promise<void> {}

    async function signUp(signUpRequest: SignUpRequest): Promise<void> {
        return await api.user
            .signUp(signUpRequest)
            .then((response) => {
                jwtUtils.setJwt(response.token);
            })
            .then(async () => {
                await navigateTo("/");
            });
    }

    async function createCampaign(
        request: CreateCampaignRequest,
    ): Promise<Campaign> {
        return await api.campaign
            .create(request)
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
    };
});

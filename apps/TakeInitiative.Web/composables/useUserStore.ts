import type { SignUpRequest } from "~/utils/api/user/signUpRequest";
import type { GetUserResponse } from "~/utils/api/user/getUserRequest";
import type { Campaign } from "~/utils/types/models";
import { type CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import type { LoginRequest } from "~/utils/api/user/loginRequest";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {
	const authPersistence = useAuthPersistence()
    const api = useApi();

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
        const token = authPersistence.getToken();
        if (token == false) {
            return false;
        }

        if (state.user == null || state.user.userId != token.UserId) {
            return await fetchUser().then(() => true);
        }

        return true;
    }

    async function login(request : LoginRequest): Promise<void> {
		return await api.user
			.login(request)
			.then((response) => {authPersistence.setToken(response.token)})
			.then(fetchUser)
			.then()
	}

    async function signUp(signUpRequest: SignUpRequest): Promise<void> {
        return await api.user
            .signUp(signUpRequest)
            .then((response) => {
                authPersistence.setToken(response.token)
            })
            .then(async () => {
                await navigateTo("/");
            });
    }

	async function signOut() : Promise<void> {
		authPersistence.clearToken()
		await navigateTo("/login")
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
		signOut
    };
});

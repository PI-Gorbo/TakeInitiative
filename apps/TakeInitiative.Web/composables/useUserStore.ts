import { useTakeInitApi } from "~/utils/api/takeInitaitiveApi";
import type { GetUserResponse, SignUpRequest } from "~/utils/api/user";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {
    const api = useTakeInitApi();

    const state = reactive({
        user: null as User | null,
    });

    async function fetchUser(): Promise<User> {
        // fetch the user.
		return api.user.getUser()
			.then(user => state.user = user)
    }

    async function isLoggedIn(): Promise<Boolean> {
        const jwt = jwtUtils.getJwt()
		if (jwt == false) {
			return false;
		}

       if (state.user == null || state.user.userId != jwt.UserId) {
		   return await fetchUser().then(() => true);
	   }

	   return true;
    }

    async function login(email: string, password: string): Promise<void> {}

    async function signUp(
        signUpRequest: SignUpRequest,
    ): Promise<void> {
        return await api.user
            .signUp(signUpRequest)
            .then((response) => {
                jwtUtils.setJwt(response.token);
				console.log("signed up!")
            }).then(async () => { await navigateTo("/")});
    }

	async function createCampaign()

    // Helper functions
    return {
		state,
        login,
        signUp,
        isLoggedIn,
    };
});

import { useTakeInitApi } from "~/utils/api/takeInitaitiveApi";
import type { GetUserResponse } from "~/utils/api/user";

type User = GetUserResponse;
export const useUserStore = defineStore("userStore", () => {
    const api = useTakeInitApi();

    const state = reactive({
        user: null as User | null,
    });

    async function fetchUser(): Promise<User> {
        // fetch the user.
        try {
            state.user = await api.user.getUser();
            return state.user;
        } catch {
            return Promise.reject();
        }
    }

    async function isLoggedIn(): Promise<Boolean> {
        const token = jwtUtils.getJwt();
        if (token == null || !jwtUtils.isValidJwt(token)) {
            return false;
        }

        return fetchUser().then(() => true);
    }

    async function login(email: string, password: string): Promise<void> {}

    async function signUp(
        email: string,
        username: string,
        password: string,
    ): Promise<void> {
        return await api.user
            .signUp(email, username, password)
            .then((response) => {
                jwtUtils.setJwt(response.token);
				console.log("signed up!")
            });
    }

    // Helper functions
    return {
        login,
        signUp,
        isLoggedIn,
    };
});

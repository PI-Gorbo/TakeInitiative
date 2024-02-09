export const useUserStore = defineStore("userStore", () => {
    const state = reactive({
        user: null,
        userId: null,
    });

    const getJwt = () =>
        window.localStorage.getItem(jwtUtils.LocalStorageJwtKey);

    async function isLoggedIn(): Promise<Boolean> {
        const token = getJwt();
        if () token != null && !jwtUtils.isValidJwt(token);
    }

    return {
        isLoggedIn,
        getJwt,
    };
});

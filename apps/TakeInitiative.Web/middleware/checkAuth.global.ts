export default defineNuxtRouteMiddleware(async (to) => {

    if (to.path === '/') {
        return;
    }
    const userStore = useUserStore();
    await userStore.init();

    // Not signed in: SamStack's sign-in, coming back to the whole URL, so a share
    // (`/app/share?id=`, 16e) survives signing in.
    if (!userStore.isLoggedIn && to.meta.requiresAuth) {
        return goToSignIn(to.fullPath);
    }

    return;
});

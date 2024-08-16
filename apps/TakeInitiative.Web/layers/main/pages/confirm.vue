<template>
    <main>
        <div>Confirm page</div>
        <div v-if="error">
            {{ error }}
        </div>
        <div v-else-if="!route.query.code">
            <div>
                An email has been sent to the email you provided. Please click
                on the button in the email which will confirm your account.
            </div>
        </div>
    </main>
</template>
<script setup lang="ts">
definePageMeta({
    requiresAuth: true,
    layout: "auth",
});
const userStore = useUserStore();
const route = useRoute();
const { pending, error } = await useAsyncData(
    "confirmEmailRequest",
    async () => {
        if (userStore.state.user?.confirmedEmail) {
            await userStore.navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin();
            return true;
        }

        const code = route.query.code;
        if (!code) {
            return await useApi()
                .user.sendConfirmationEmail()
                .then(() => true);
        }

        return await userStore
            .ConfirmEmail(code as string)
            .then(
                async () =>
                    await userStore.navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin(),
            )
            .then(() => true);
    },
);
</script>

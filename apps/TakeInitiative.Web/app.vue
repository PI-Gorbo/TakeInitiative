<template>
    <NuxtLoadingIndicator />
    <NuxtLayout>
        <NuxtPage />
    </NuxtLayout>
</template>

<script setup lang="ts">
const userStore = useUserStore();
const { refresh, pending, error } = useAsyncData("User", async () => {
    console.log("TESTING");
    return await userStore.isLoggedIn().then(() => true);
});
</script>

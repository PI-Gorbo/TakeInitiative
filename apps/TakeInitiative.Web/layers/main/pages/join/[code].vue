<template>
    <main>
        <div v-if="pending" class="font-NovaCut text-lg text-take-yellow">
            Joining...
        </div>
        <div v-else-if="error" class="flex flex-col items-center gap-2">
            <label class="font-NovaCut text-lg text-take-red"
                >Failed to join Combat</label
            >
            <label>{{ errorMessage }}</label>
        </div>
        <div v-else class="font-NovaCut text-lg text-take-yellow">Joined</div>
    </main>
</template>
<script setup lang="ts">
import type { NuxtApp } from "nuxt/schema";
definePageMeta({
    middleware: [],
    requiresAuth: true,
    layout: "auth",
});

const {
    data: errorMessage,
    pending,
    error,
} = await useAsyncData(
    "joinPage",
    async () => {
        // Route
        const joinCode = useRoute().params.code as string;
        const userStore = useUserStore();
        return await userStore
            .joinCampaign({ joinCode })
            .then(
                async (c) =>
                    await useNavigator().toCampaignTab(c.id, "summary"),
            )
            .then(() => true)
            .catch(
                async (err) =>
                    "The url provided does not correspond to a valid join code.",
            );
    },
    { server: false },
);
</script>

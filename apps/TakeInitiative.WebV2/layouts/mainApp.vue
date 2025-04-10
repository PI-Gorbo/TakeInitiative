<template>
    <div class="h-full w-full">
        <NuxtLayout name="default">
            <SidebarProvider :defaultOpen="false" class="h-full w-full">
                <AppSidebar />
                <div class="flex h-full w-full flex-col gap-4">
                    <div class="flex justify-center">
                        <header class="w-page flex justify-between p-2">
                            <div class="flex items-center gap-1">
                                <CustomSidebarTrigger
                                    v-if="showSidebar"
                                    v-slot="{ open }"
                                    class="flex items-center gap-4">
                                    <FontAwesomeIcon :icon="faBars" />
                                </CustomSidebarTrigger>
                                <h1
                                    class="flex items-center gap-4 font-NovaCut text-2xl font-bold text-gold sm:text-3xl">
                                    <img
                                        class="dice-icon h-[2em] w-[2em]"
                                        src="~/public/img/yellowDice.png" />
                                    <div class="hidden sm:inline">
                                        Take Initiative
                                    </div>
                                </h1>
                                <span
                                    class="font-NovaCut text-xl text-gold"
                                    v-if="
                                        !isDesktopSized && campaignNameToShow
                                    ">
                                    {{ campaignNameToShow }}
                                </span>
                            </div>
                            <AppNavigationBar v-if="!showSidebar" />
                        </header>
                    </div>
                    <div v-if="user.state.user" class="flex-1 px-2">
                        <slot />
                    </div>
                    <div v-else class="flex items-center justify-center">
                        <FontAwesomeIcon :icon="faSpinner" class="fa-spin" />
                    </div>
                </div>
            </SidebarProvider>
        </NuxtLayout>
    </div>
</template>

<script setup lang="ts">
    import {
        faBars,
        faSpinner,
        faUser,
        faUserCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useMediaQuery } from "@vueuse/core";

    const isDesktopSized = useMediaQuery("(min-width: 640px)", {
        ssrWidth: 640,
    });

    const showSidebar = computed(() => {
        return !isDesktopSized.value;
    });

    const route = useRoute();
    const user = useUserStore();

    const campaignStore = useCampaignStore();
    const campaignNameToShow = computed(() => {
        if (route.name.startsWith("app-campaigns-id")) {
            return campaignStore.state.campaign?.campaignName;
        }

        return null;
    });
</script>

<style scoped lang="css">
    .dice-icon {
        /* Initial setup for the icon */
        display: inline-block;
        transform-origin: center;
        animation: spin 45s infinite linear; /* Start slow, speed up */
    }

    /* Keyframes for spinning */
    @keyframes spin {
        0% {
            transform: rotate(0deg);
        }
        100% {
            transform: rotate(360deg);
        }
    }
</style>

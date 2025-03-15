<template>
    <div class="h-full w-full">
        <NuxtLayout name="default">
            <SidebarProvider :defaultOpen="false" class="h-full w-full">
                <AppSidebar />
                <div class="flex h-full w-full flex-col gap-4">
                    <header class="flex justify-between p-1 px-4 sm:p-2">
                        <div class="flex items-center gap-1">
                            <CustomSidebarTrigger
                                v-slot="{ open }"
                                class="flex items-center gap-4">
                                <FontAwesomeIcon :icon="faBars" />
                                <h1
                                    class="flex items-center gap-4 font-NovaCut text-2xl font-bold text-gold sm:text-3xl">
                                    <img
                                        class="dice-icon h-[2em] w-[2em]"
                                        src="~/public/img/yellowDice.png"
                                        :data-open="open" />
                                    <div class="hidden sm:inline">
                                        Take Initiative
                                    </div>
                                </h1>
                            </CustomSidebarTrigger>
                        </div>
                    </header>
                    <div v-if="userStore.state.user" class="flex-1 px-2">
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
    import { useSidebar } from "~/components/ui/sidebar";

    const route = useRoute();
    const userStore = useUserStore();

    const shouldShowAllCampaigns = computed(() =>
        route.name.startsWith("app-campaigns-id")
    );
</script>

<style scoped lang="css">
    .dice-icon {
        transition: transform 0.4s;
    }

    .dice-icon[data-open="false"] {
        transform: rotate(0deg);
    }

    .dice-icon[data-open="true"] {
        transform: rotate(360deg);
    }
</style>

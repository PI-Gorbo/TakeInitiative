<template>
    <div class="h-full w-full">
        <NuxtLayout name="default">
            <SidebarProvider
                :defaultOpen="false"
                class="h-full w-full">
                <AppSidebar />
                <div class="flex h-full w-full flex-col lg:gap-4 gap-2">
                    <div class="flex justify-center">
                        <header class="w-page flex justify-between p-2">
                            <div class="flex items-center gap-1">
                                <CustomSidebarTrigger
                                    v-if="showSidebar"
                                    v-slot="{ open }"
                                    class="flex items-center gap-4">
                                    <FontAwesomeIcon :icon="faBars" />
                                </CustomSidebarTrigger>

                                <component
                                    :is="headerLink ? NuxtLink : 'h1'"
                                    :to="headerLink"
                                    class="flex items-center gap-4 font-NovaCut text-2xl font-bold text-gold sm:text-3xl">
                                    <img
                                        class="dice-icon h-[2em] w-[2em]"
                                        src="~/public/img/yellowDice.png" />
                                    <div class="flex flex-col">
                                        <span
                                            v-if="
                                                isDesktopSized ||
                                                !currentRoute.name.startsWith(
                                                    'app-campaigns-campaignId'
                                                )
                                            ">
                                            Take Initiative
                                        </span>
                                        <span
                                            class="font-NovaCut text-xl text-gold lg:text-lg text-gray-300"
                                            v-if="
                                                campaignNameQuery.isSuccess
                                                    .value
                                            ">
                                            {{ campaignNameQuery.data.value }}
                                        </span>
                                    </div>
                                </component>
                            </div>
                            <AppNavigationBar v-if="!showSidebar" />
                        </header>
                    </div>
                    <div
                        v-if="userStore.state"
                        class="flex-grow px-2 overflow-auto">
                        <slot />
                    </div>
                    <div
                        v-else
                        class="flex items-center justify-center">
                        <FontAwesomeIcon
                            :icon="faSpinner"
                            class="fa-spin" />
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
    import { useQuery } from "@tanstack/vue-query";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { helpers } from "@typed-router";
    import { NuxtLink } from "#components";

    const isDesktopSized = useMediaQuery("(min-width: 640px)", {
        ssrWidth: 640,
    });

    const showSidebar = computed(() => {
        return !isDesktopSized.value;
    });
    const currentRoute = useRoute();
    const route = useRoute("app-campaigns-campaignId");
    const userStore = useUserStore();

    const campaignNameQuery = useQuery({
        ...getCampaignQuery(() => route.params?.campaignId),
        select: (data) => data.campaign.campaignName,
    });

    const headerLink = computed(() => {
        if (route.params?.campaignId) {
            return helpers.route({
                name: "app-campaigns-campaignId",
                params: { campaignId: route.params.campaignId },
            });
        }
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

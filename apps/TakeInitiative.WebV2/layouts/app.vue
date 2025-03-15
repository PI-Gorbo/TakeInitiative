<template>
    <div class="h-full w-full">
        <NuxtLayout name="default">
            <div class="flex h-full w-full flex-col gap-4">
                <header class="flex justify-between p-1 px-4 sm:p-2">
                    <NuxtLink to="/app/campaigns">
                        <h1
                            class="flex items-center gap-4 font-NovaCut text-2xl font-bold text-gold sm:text-3xl">
                            <img
                                class="h-[2em] w-[2em]"
                                src="~/public/img/yellowDice.png" />
                            <div class="hidden sm:inline">Take Initiative</div>
                        </h1>
                    </NuxtLink>

                    <aside class="flex items-center gap-4 px-2">
                        <NuxtLink
                            v-if="shouldShowAllCampaigns"
                            :to="`/app/campaigns`"
                            ><Button variant="outline"
                                >All Campaigns</Button
                            ></NuxtLink
                        >
                        <DropdownMenu>
                            <DropdownMenuTrigger
                                class="flex items-center justify-center">
                                <FontAwesomeIcon
                                    :icon="faUserCircle"
                                    class="text-primary"
                                    size="2x" />
                            </DropdownMenuTrigger>
                            <DropdownMenuContent>
                                <DropdownMenuLabel
                                    >My Account</DropdownMenuLabel
                                >
                                <DropdownMenuSeparator />
                                <DropdownMenuItem asChild>
                                    <NuxtLink to="/app/me"> Settings </NuxtLink>
                                </DropdownMenuItem>
                                <DropdownMenuItem @click="userStore.logout">
                                    Logout
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    </aside>
                </header>

                <div v-if="userStore.state.user" class="flex-1 px-2">
                    <slot />
                </div>
                <div v-else class="flex items-center justify-center">
                    <FontAwesomeIcon :icon="faSpinner" class="fa-spin" />
                </div>
            </div>
        </NuxtLayout>
    </div>
</template>

<script setup lang="ts">
    import { faSpinner, faUser, faUserCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    const route = useRoute();
    const userStore = useUserStore();

    const shouldShowAllCampaigns = computed(() =>
        route.name.startsWith("app-campaigns-id")
    );
</script>

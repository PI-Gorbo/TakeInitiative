<template>
    <div class="h-full w-full">
        <NuxtLayout name="default">
            <div class="flex h-full w-full flex-col gap-4">
                <header
                    class="flex justify-between bg-accent/20 p-1 px-4 sm:p-2">
                    <NuxtLink to="/app/campaigns">
                        <h1
                            class="flex items-center gap-4 font-NovaCut text-2xl font-bold text-gold sm:text-3xl">
                            <img
                                class="h-[2em] w-[2em]"
                                src="~/public/img/yellowDice.png" />
                            <div class="hidden sm:inline">Take Initiative</div>
                        </h1>
                    </NuxtLink>
                    <DropdownMenu>
                        <DropdownMenuTrigger
                            class="flex items-center justify-center">
                            <FontAwesomeIcon :icon="faUserCircle" size="2x" />
                        </DropdownMenuTrigger>
                        <DropdownMenuContent>
                            <DropdownMenuLabel>My Account</DropdownMenuLabel>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem asChild>
                                <NuxtLink to="/app/me"> Settings </NuxtLink>
                            </DropdownMenuItem>
                            <DropdownMenuItem @click="userStore.logout">
                                Logout
                            </DropdownMenuItem>
                        </DropdownMenuContent>
                    </DropdownMenu>
                </header>

                <div class="flex-1 px-2">
                    <slot />
                </div>
            </div>
        </NuxtLayout>
    </div>
</template>

<script setup lang="ts">
    import { faUserCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const userStore = useUserStore();
</script>

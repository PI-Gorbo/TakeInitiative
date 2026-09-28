<template>
    <div class="flex h-full w-full flex-col bg-background">
        <!-- Pages outside a campaign: the campaign list and account settings.
             Not wrapped in the default layout: the layout transition can only animate an
             element root, not a nested <NuxtLayout>. So its Toaster is repeated below. -->
        <header class="border-b pt-safe px-safe">
            <div
                class="mx-auto flex h-14 w-full max-w-2xl items-center gap-2 px-2">
                <NuxtLink
                    to="/app/campaigns"
                    class="flex min-w-0 flex-1 items-center gap-2 font-NovaCut text-xl text-gold">
                    <img
                        src="/yellowDice.png"
                        alt=""
                        class="size-8" />
                    Take Initiative
                </NuxtLink>
                <DropdownMenu>
                    <DropdownMenuTrigger
                        class="flex h-11 min-w-11 items-center justify-center gap-2 rounded-md px-2 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Account">
                        <CircleUser class="size-6" />
                        <span class="hidden max-w-40 truncate sm:inline">{{
                            userStore.username
                        }}</span>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                        <DropdownMenuItem
                            class="min-h-11"
                            @select="navigateTo('/app/me')">
                            <Settings class="size-4" /> Settings
                        </DropdownMenuItem>
                        <DropdownMenuItem
                            class="min-h-11"
                            @select="userStore.logout()">
                            <LogOut class="size-4" /> Log out
                        </DropdownMenuItem>
                    </DropdownMenuContent>
                </DropdownMenu>
            </div>
        </header>
        <main class="min-h-0 flex-1 overflow-y-auto pb-safe px-safe">
            <div class="mx-auto w-full max-w-2xl p-4">
                <slot />
            </div>
        </main>
        <ClientOnly>
            <Toaster
                :position="'top-right'"
                :duration="1000" />
        </ClientOnly>
    </div>
</template>

<script setup lang="ts">
    import { CircleUser, LogOut, Settings } from "lucide-vue-next";

    const userStore = useUserStore();
</script>

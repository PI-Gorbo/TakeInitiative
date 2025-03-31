<template>
    <NavigationMenu class="list-none space-x-4">
        <NavigationMenuList>
            <NavigationMenuItem>
                <NavigationMenuTrigger> My Campaigns </NavigationMenuTrigger>
                <NavigationMenuContent>
                    <ul class="flex flex-col gap-2 p-2">
                        <NuxtLink
                            v-slot="{ isActive, href, navigate }"
                            :to="{
                                name: 'app-campaigns',
                            }"
                            class="w-full">
                            <NavigationMenuLink
                                :active="isActive"
                                :href
                                :class="navigationMenuTriggerStyle()"
                                @click="navigate"
                                class="w-full">
                                All Campaigns
                            </NavigationMenuLink>
                        </NuxtLink>
                        <NuxtLink
                            v-for="(campaign, index) in user.campaignList"
                            :key="index"
                            v-slot="{ isActive, href, navigate }"
                            class="w-full"
                            :to="{
                                name: 'app-campaigns-id',
                                params: {
                                    id: campaign.campaignId,
                                },
                            }">
                            <NavigationMenuLink
                                :active="isActive"
                                :href
                                :class="navigationMenuTriggerStyle()"
                                @click="navigate"
                                class="w-full">
                                {{ campaign.campaignName }}
                            </NavigationMenuLink>
                        </NuxtLink>
                    </ul>
                </NavigationMenuContent>
            </NavigationMenuItem>
        </NavigationMenuList>
        <NavigationMenuItem>
            <DropdownMenu>
                <DropdownMenuTrigger class="flex items-center gap-2"
                    ><FontAwesomeIcon :icon="faUserCircle" size="2x" />
                    {{ user.state.user?.username }}</DropdownMenuTrigger
                >
                <DropdownMenuContent>
                    <DropdownMenuItem>
                        <NuxtLink
                            :to="{
                                name: 'app-me',
                            }">
                            Settings
                        </NuxtLink>
                    </DropdownMenuItem>
                    <DropdownMenuItem asChild>
                        <NavigationMenuLink
                            @click="user.logout"
                            class="hover:bg-secondary">
                            <FontAwesomeIcon :icon="faArrowRightFromBracket" />
                            Logout
                        </NavigationMenuLink>
                    </DropdownMenuItem>
                </DropdownMenuContent>
            </DropdownMenu>
        </NavigationMenuItem>
    </NavigationMenu>
</template>
<script setup lang="ts">
    import {
        NavigationMenu,
        NavigationMenuContent,
        NavigationMenuItem,
        NavigationMenuLink,
        NavigationMenuList,
        NavigationMenuTrigger,
        navigationMenuTriggerStyle,
    } from "@/components/ui/navigation-menu";
    import {
        faArrowRightFromBracket,
        faUserCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const user = useUserStore()
    
    
</script>

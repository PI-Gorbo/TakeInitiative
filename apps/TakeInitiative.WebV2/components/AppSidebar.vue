<template>
    <Sidebar>
        <SidebarContent class="bg-background">
            <SidebarContent>
                <SidebarGroup>
                    <SidebarMenuItem>
                        <SidebarMenuButton
                            class="cursor-pointer select-none hover:bg-secondary">
                            <NuxtLink
                                :to="{ name: 'app-campaigns' }"
                                class="w-full"
                                >All Campaigns</NuxtLink
                            ></SidebarMenuButton
                        >
                    </SidebarMenuItem>
                    <SidebarMenuItem>
                        <SidebarMenuButton
                            class="cursor-pointer select-none truncate hover:bg-secondary"
                            >Campaign List</SidebarMenuButton
                        >
                        <SidebarMenuSub>
                            <SidebarMenuSubItem
                                v-for="(campaign, index) in user.campaignList"
                                :key="campaign.campaignId">
                                <SidebarMenuSubButton
                                    class="cursor-pointer select-none hover:bg-secondary">
                                    <NuxtLink
                                        :to="{
                                            name: 'app-campaigns-campaignId',
                                            params: {
                                                campaignId: campaign.campaignId,
                                            },
                                        }"
                                        class="flex w-full items-center justify-between gap-2 truncate"
                                        @click="closeSidebar">
                                        <span>{{ campaign.campaignName }}</span>
                                        <FontAwesomeIcon
                                            v-if="campaign.currentCombatName"
                                            class="text-destructive"
                                            :icon="faHandFist" />
                                    </NuxtLink>
                                </SidebarMenuSubButton>
                            </SidebarMenuSubItem>
                        </SidebarMenuSub>
                    </SidebarMenuItem>
                </SidebarGroup>
            </SidebarContent>
            <SidebarFooter>
                <SidebarMenuItem>
                    <SidebarMenuButton class="hover:bg-secondary">
                        <NuxtLink
                            :to="{ name: 'app-me' }"
                            class="flex items-center gap-2">
                            <FontAwesomeIcon
                                :icon="faUserCircle"
                                size="2x" />
                            {{ user.state.user?.username }}
                        </NuxtLink>
                    </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                    <SidebarMenuButton
                        @click="user.logout"
                        class="hover:bg-secondary">
                        <FontAwesomeIcon :icon="faArrowRightFromBracket" />
                        Logout
                    </SidebarMenuButton>
                </SidebarMenuItem>
            </SidebarFooter>
        </SidebarContent>
    </Sidebar>
</template>

<script setup lang="ts">
    import {
        faArrowRightFromBracket,
        faHandFist,
        faUserCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useSidebar } from "./ui/sidebar";
    const user = useUserStore();

    const sidebar = useSidebar();

    function closeSidebar() {
        sidebar.toggleSidebar()
    }
</script>

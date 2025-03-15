<template>
    <Sidebar>
        <SidebarContent class="bg-background">
            <SidebarHeader>My Super Cool sidebar</SidebarHeader>
            <SidebarContent>
                <SidebarGroup>
                    <SidebarMenuItem>
                        <SidebarMenuButton
                            class="cursor-pointer select-none truncate hover:bg-secondary"
                            >Campaigns</SidebarMenuButton
                        >
                        <SidebarMenuSub>
                            <SidebarMenuSubItem
                                v-for="(campaign, index) in user.campaignList"
                                :key="campaign.campaignId">
                                <SidebarMenuSubButton
                                    class="flex cursor-pointer select-none items-center justify-between truncate hover:bg-secondary"
                                    @click="
                                        () =>
                                            onSetSelectedCampaign(
                                                campaign.campaignId
                                            )
                                    ">
                                    <span>{{ campaign.campaignName }}</span>
                                    <FontAwesomeIcon
                                        v-if="campaign.currentCombatName"
                                        class="text-destructive"
                                        :icon="faHandFist" />
                                </SidebarMenuSubButton>
                            </SidebarMenuSubItem>
                        </SidebarMenuSub>
                    </SidebarMenuItem>
                </SidebarGroup>
            </SidebarContent>
            <SidebarFooter>
                <div class="flex items-center gap-2">
                    <FontAwesomeIcon :icon="faUserCircle" size="2x" />
                    {{ user.state.user?.username }}
                </div>
                <SidebarMenuItem>
                    <SidebarMenuButton
                        @click="user.logout"
                        class="hover:bg-secondary">
                        Logout
                    </SidebarMenuButton>
                </SidebarMenuItem>

                <!-- <aside class="flex items-center gap-4 px-2">
                    <NuxtLink v-if="shouldShowAllCampaigns" :to="`/app/campaigns`"
                    ><Button variant="outline">All Campaigns</Button></NuxtLink
                    >
                        <DropdownMenuTrigger
                        class="flex items-center justify-center">
            
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
            </aside> -->
            </SidebarFooter>
        </SidebarContent>
    </Sidebar>
</template>

<script setup lang="ts">
    import {
        faHandFist,
        faUserCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useSidebar } from "./ui/sidebar";
    const user = useUserStore();
    const navigator = useNavigator();
    const sidebar = useSidebar();

    // Events
    function onSetSelectedCampaign(campaignId: string) {
        navigator.toCampaign(campaignId);
        sidebar.setOpen(false);
    }
</script>

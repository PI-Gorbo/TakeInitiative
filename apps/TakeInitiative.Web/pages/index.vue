<template>
    <div
        v-if="!pending && !error"
        class="flex h-full w-full flex-col overflow-auto"
    >
        <header>
            <div
                class="flex cursor-pointer items-center border-b-2 border-take-yellow bg-take-navy-medium px-4 py-1"
            >
                <h1
                    class="flex items-center gap-2 font-NovaCut text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl"
                >
                    <img
                        class="h-[1.5em] w-[1.5em]"
                        src="~assets/yellowDice.png"
                    />
                    Take Initiative
                </h1>
                <section class="flex min-h-0 flex-1 basis-0 justify-end gap-2">
                    <div
                        class="flex items-center px-2 py-1 font-NovaCut text-xl text-take-yellow"
                    >
                        <label>{{ userStore.username }}</label>
                    </div>
                    <div class="h-full min-w-fit self-center">
                        <!-- <DropDown
                            Items="Items"
                            DisplayFunc="((item) => item.CampaignName)"
                            IdFunc="((item) => item.Id.ToString())"
                            CategoryFunc="CategorizeByIfUserOwnsCampaign"
                            CurrentlySelectedItem="CurrentlySelectedCampaign"
                            ItemSelected="(Campaign campaign) => CampaignStore.SetCurrentCampaign(campaign)"
                        /> -->
                    </div>
                    <div class="flex items-center">
                        <IconButton
                            IconName="fa-share-from-square"
                            OnClick="SharedContext.ShowShareModal"
                        />
                    </div>
                    <div class="flex items-center">
                        <IconButton
                            IconName="fa-user-gear"
                            OnClick="SharedContext.ShowUserStateModal"
                        />
                    </div>
                </section>
            </div>
        </header>
        <Tabs
            class="flex-1 overflow-auto p-4"
            backgroundColour="take-navy-medium"
            notSelectedTabColour="take-navy"
            :renameTabs="{
                PlannedCombats: 'Planned Combats',
            }"
            :showTabs="{
                PlannedCombats: () => campaignStore.state.isDm,
            }"
        >
            <template #Summary>
                <IndexSummary class="px-4" />
            </template>
            <template #Characters></template>
            <template #PlannedCombats></template>
        </Tabs>
    </div>
</template>

<script setup lang="ts">
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";
const userStore = useUserStore();
const campaignStore = useCampaignStore();
const { refresh, pending, error } = useAsyncData("Campaign", () => {
    return campaignStore.init().then(() => true);
}); // Return something so that nuxt does not recall this on this client

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>

<template>
    <TransitionGroup
        name="fade"
        tag="main"
        class="flex h-full flex-col items-center overflow-y-auto bg-take-navy text-white"
    >
        <div
            v-if="
                isSignalRRefresh ||
                (!pending && !error) ||
                userStore.state.selectedCampaignId == null
            "
            :key="userStore.state.selectedCampaignId!"
            class="flex w-full flex-1 flex-col overflow-auto md:w-4/5 md:max-w-[1200px] 2xl:w-full"
        >
            <Tabs
                class="flex-1 flex-col overflow-auto"
                backgroundColour="take-navy"
                notSelectedTabColour="take-navy"
                :renameTabs="{
                    Combats: 'Combats',
                }"
                :showTabs="{
                    Combats: () => campaignStore.isDm,
                    Settings: () => campaignStore.isDm,
                }"
                negativeSectionId="IndexPageTabs"
            >
                <template #Summary>
                    <IndexSummarySection />
                </template>
                <template #Characters> <IndexCharactersSection /> </template>
                <template #Combats>
                    <IndexCombatSection />
                </template>
                <template #Settings>
                    <main class="flex w-2/3 flex-col gap-4">
                        <div class="w-fit">
                            <FormToggleableInput
                                label="Campaign Name"
                                v-model:value="campaignName"
                                buttonColour="take-navy-medium"
                                notEditableColour="take-navy-medium"
                                :onSave="
                                    async () => {
                                        return userStore.updateCampaign({
                                            campaignId: campaign?.campaignId!,
                                            campaignName: campaignName,
                                        });
                                    }
                                "
                            />
                        </div>
                        <div>
                            <FormButton
                                label="Delete Campaign"
                                icon="trash"
                                buttonColour="take-navy-light"
                                hoverButtonColour="take-red"
                                size="sm"
                                :click="() => deleteCampaign()"
                            />
                        </div>
                    </main>
                </template>
            </Tabs>
        </div>
        <div v-else class="flex h-full w-full items-center justify-center">
            <FontAwesomeIcon class="fa-spin" icon="circle-notch" size="10x" />
        </div>
    </TransitionGroup>
</template>

<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import * as signalR from "@microsoft/signalr";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";
import { CombatState, type Campaign } from "~/utils/types/models";
useHead({
    title: "Take Initiative",
});

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});

// Main Page Data & Settings Page config
const userStore = useUserStore();
const { selectedCampaignDto: campaign } = storeToRefs(userStore);
const campaignName = ref<string | undefined>(undefined);
const campaignStore = useCampaignStore();
const {
    refresh: refreshPageData,
    pending,
    error,
} = await useAsyncData(
    "Campaign",
    () => {
        return campaignStore
            .init()
            .then(
                () =>
                    (campaignName.value =
                        campaignStore.state.campaign?.campaignName!),
            )
            .then(() => true);
    },
    { watch: [() => userStore.state.selectedCampaignId] },
); // Return something so that nuxt does not recall this on this client
onMounted(
    () => (campaignName.value = campaignStore.state.campaign?.campaignName!),
);

function deleteCampaign() {
    console.log(campaignStore.state.campaign?.id);
    return userStore
        .deleteCampaign({ campaignId: campaignStore.state.campaign?.id! })
        .then(async () => {
            if (userStore.campaignCount == 0) {
                await navigateTo("/createOrJoinCampaign");
            } else {
                userStore.setSelectedCampaign(
                    userStore.campaignList![0].campaignId,
                );
            }
        });
}

// Signal R connectivity.
const isSignalRRefresh = ref<boolean>(false);
const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${useRuntimeConfig().public.axios.baseURL}/campaignHub`, {
        accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
    })
    .build();

const { pending: pendingSignalR, error: errorSignalR } = await useAsyncData(
    "CampaignHub",
    async () => {
        await connection.start();
        connection.on("combatStateUpdated", async () => {
            console.log("combat state has updated");
            isSignalRRefresh.value = true;
            await refreshPageData();
            isSignalRRefresh.value = false;
        });
        await connection
            .send(
                "Join",
                userStore.state.user?.userId,
                campaignStore.state.campaign?.id,
            )
            .catch((error) => console.log("error connecting"));
    },
    { server: false },
);

watch(
    () => userStore.state.selectedCampaignId,
    async (newValue, oldValue) => {
        if (oldValue != null) {
            await connection.send("Leave", oldValue);
        }

        if (newValue != null) {
            await connection.send("Join", newValue);
        }
    },
);

onUnmounted(async () => {
    await connection
        .send("Leave", campaignStore.state.campaign?.id)
        .catch((error) => console.log("error leaving"));
    await connection.stop();
});
</script>

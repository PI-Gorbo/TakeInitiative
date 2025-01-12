<template>
    <div
        class="drawer drawer-end flex h-full bg-take-purple-very-dark text-white"
    >
        <input
            ref="drawerToggle"
            id="drawer"
            type="checkbox"
            class="drawer-toggle"
        />
        <div class="drawer-content w-full">
            <main class="flex h-full w-full flex-col">
                <header
                    class="navbar border-b border-b-take-yellow bg-take-purple-dark"
                >
                    <section class="navbar-start flex gap-2">
                        <label
                            class="flex cursor-pointer items-center gap-2"
                            @click="takeInitiativeIconClicked"
                        >
                            <img
                                class="h-[3em] w-[3em]"
                                src="/img/yellowDice.png"
                            />
                            <span
                                class="select-none font-NovaCut text-xl text-take-yellow md:text-3xl lg:text-2xl"
                                >Take Initiative</span
                            >
                        </label>
                    </section>
                    <section class="navbar-end flex items-center gap-2">
                        <template v-if="isCampaignsRoute">
                            <label
                                class="font-NovaCut text-lg text-take-yellow lg:text-xl"
                                >{{
                                    selectedCampaignInfo?.campaign?.campaignName
                                }}</label
                            >
                        </template>
                        <label
                            for="drawer"
                            class="aspect-square cursor-pointer rounded-md p-2 hover:bg-take-purple"
                        >
                            <FontAwesomeIcon icon="bars" />
                        </label>
                    </section>
                </header>
                <div class="flex-1 overflow-y-scroll">
                    <slot />
                </div>
            </main>
        </div>
        <div class="drawer-side">
            <label
                for="drawer"
                aria-label="close sidebar"
                class="drawer-overlay"
            ></label>
            <ul
                class="menu flex min-h-full w-80 flex-col gap-2 bg-take-purple-dark p-4 text-base-content"
            >
                <Dropdown
                    v-if="isCampaignsRoute"
                    colour="take-purple-light"
                    hoverColour="take-navy-medium"
                    labelFallback="Campaigns"
                    headerLabel="Campaign: "
                    :items="userStore.campaignList!"
                    :displayFunc="(c) => c.campaignName"
                    :keyFunc="(c) => c.campaignId"
                    :selectedItem="
                        userStore.campaignList?.find(
                            (x) =>
                                x.campaignId ==
                                selectedCampaignInfo.campaign?.id,
                        )
                    "
                    @update:selectedItem="
                        (item) => onSetSelectedCampaign(item.campaignId)
                    "
                >
                    <template #Footer>
                        <li
                            :class="['rounded-md', ``]"
                            @click="
                                () => {
                                    createOrJoinCampaignModal?.show();
                                    toggleDrawer();
                                }
                            "
                        >
                            <a class="text-take-yellow"
                                ><FontAwesomeIcon icon="plus" /> Create or
                                Join</a
                            >
                        </li>
                    </template>
                </Dropdown>
                <li v-else-if="isCombatRoute">
                    <FormButton
                        icon="house"
                        label="Return Home"
                        buttonColour="take-purple-light"
                        hoverButtonColour="take-yellow"
                        @clicked="() => toCampaign(combatInfo?.campaignId!)"
                    />
                </li>
                <li class="flex-1 bg-take-purple-dark"></li>
                <li class="bg-take-purple-dark font-NovaCut text-take-yellow">
                    {{ userStore.username }}
                </li>
                <li v-if="!userStore.state?.user?.confirmedEmail">
                    <FormButton
                        label="Confirm Email"
                        icon="envelope"
                        :click="confirmEmail"
                        buttonColour="take-purple-light"
                        hoverButtonColour="take-navy-medium"
                        size="sm"
                    />
                </li>
                <li>
                    <FormButton
                        label="Logout"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Logging out...',
                        }"
                        icon="right-from-bracket"
                        :click="userStore.logout"
                        buttonColour="take-purple-light"
                        hoverButtonColour="take-navy-medium"
                        size="sm"
                    />
                </li>
            </ul>
        </div>
      
        <Modal ref="createOrJoinCampaignModal" title="Create or Join">
            <Tabs notSelectedTabColour="take-navy-medium">
                <template #Create>
                    <CreateCampaignForm :submit="createCampaign" class="px-2" />
                </template>
                <template #Join>
                    <JoinCampaignForm :submit="joinCampaign" class="px-2" />
                </template>
            </Tabs>
        </Modal>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import Modal from "base/components/Modal.vue";
import type { CreateCampaignRequest } from "base/utils/api/campaign/createCampaignRequest";
import type { JoinCampaignRequest } from "base/utils/api/campaign/joinCampaignRequest";

const nav = useNavigator();
const userStore = useUserStore();
const { isMobile } = useDevice();

// Drawer
const drawerToggle = ref<HTMLInputElement | null>(null);
function toggleDrawer() {
    if (drawerToggle.value) {
        drawerToggle.value.checked = !drawerToggle.value?.checked;
    }
}

// Modals
const createOrJoinCampaignModal = ref<InstanceType<typeof Modal> | null>(null);

// Route info
const routeInfo = useRoute();
const isCampaignsRoute = computed(() =>
    routeInfo.name?.toString().startsWith("campaign-id"),
);
const selectedCampaignInfo = computed(() => useCampaignStore().state);

const isCombatRoute = computed(() => routeInfo.name == "combat-id");
const combatInfo = computed(() => useCombatStore().state?.combat);

// Events
function onSetSelectedCampaign(campaignId: string) {
    nav.toCampaignTab(campaignId, "summary");
    toggleDrawer();
}

async function createCampaign(req: CreateCampaignRequest) {
    return await useUserStore()
        .createCampaign(req)
        .then(async (c) => {
            await nav.toCampaignTab(c.id, "summary");
        })
        .then(createOrJoinCampaignModal.value?.hide);
}

async function joinCampaign(req: JoinCampaignRequest) {
    return await useUserStore()
        .joinCampaign(req)
        .then(async (c) => {
            await nav.toCampaignTab(c.id, "summary");
        })
        .then(createOrJoinCampaignModal.value?.hide);
}

const takeInitiativeIconClicked = async () => {
    const campaignStore = useCampaignStore();
    if (campaignStore.state.campaign != null) {
        await useNavigator().toCampaignTab(
            campaignStore.state.campaign.id,
            "summary",
        );
        return;
    }

    await toHomePage();
};

const toHomePage = async () => await useNavigator().toHomePage();
const toCampaign = async (id: string) => {
    await useNavigator().toCampaignTab(id, "summary");
    toggleDrawer();
};
const confirmEmail = async () => await useNavigator().confirmEmail();
</script>

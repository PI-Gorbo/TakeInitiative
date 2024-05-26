<template>
    <div class="drawer flex h-full bg-take-navy text-white">
        <input
            ref="drawerToggle"
            id="drawer"
            type="checkbox"
            class="drawer-toggle"
        />
        <div class="drawer-content w-full">
            <main class="flex h-full w-full flex-col">
                <header
                    class="navbar border border-take-navy-medium border-b-take-yellow bg-take-navy-medium"
                >
                    <section class="navbar-start flex gap-2">
                        <label
                            for="drawer"
                            class="flex cursor-pointer items-center gap-2"
                        >
                            <img
                                class="h-[3em] w-[3em]"
                                src="~assets/yellowDice.png"
                            />
                            <label
                                class="font-NovaCut text-2xl text-take-yellow md:text-3xl"
                                v-if="!isMobile"
                                >Take Initiative</label
                            >
                        </label>
                    </section>
                    <section
                        class="navbar-end flex items-center gap-2"
                        v-if="isCampaignsRoute"
                    >
                        <label class="font-NovaCut text-xl text-take-yellow">{{
                            selectedCampaignInfo?.campaign?.campaignName
                        }}</label>
                        <FormButton
                            icon="share-from-square"
                            size="sm"
                            textColour="take-grey"
                            buttonColour="take-navy-medium"
                            hoverButtonColour="take-navy-dark"
                            @clicked="() => shareCampaignModal?.show()"
                        />
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
                class="menu flex min-h-full w-80 flex-col gap-2 bg-take-navy p-4 text-base-content"
            >
                <li v-if="isCampaignsRoute">
                    <Dropdown
                        colour="take-navy-dark"
                        hoverColour="take-navy-medium"
                        labelFallback="Campaigns"
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
                </li>
                <li v-else-if="isCombatRoute">
                    <FormButton
                        icon="house"
                        label="Return Home"
                        buttonColour="take-navy-dark"
                        hoverButtonColour="take-yellow"
                        @clicked="
                            () => {
                                useNavigator().toCampaignTab(
                                    combatInfo?.campaignId!,
                                    'summary',
                                );
                                toggleDrawer();
                            }
                        "
                    />
                </li>
                <li class="flex-1 bg-take-navy"></li>
                <li>
                    <div
                        class="font-NovaCut text-take-yellow hover:bg-take-navy"
                    >
                        {{ userStore.username }}
                    </div>
                    <FormButton
                        label="Logout"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Logging out...',
                        }"
                        icon="right-from-bracket"
                        :click="userStore.logout"
                        buttonColour="take-navy-dark"
                        hoverButtonColour="take-navy-medium"
                        size="sm"
                    />
                </li>
            </ul>
        </div>
        <Modal ref="shareCampaignModal" title="Share">
            <main class="flex flex-col gap-4 text-white">
                <div class="flex flex-col gap-2">
                    <label class="font-NovaCut text-take-yellow"
                        >Campaign Code</label
                    >
                    <div class="flex w-full items-center gap-2">
                        <TimedTooltip tooltip="Copied!" class="peer">
                            <FormButton
                                icon="copy"
                                buttonColour="take-navy-dark"
                                hoverButtonColour="take-yellow"
                                size="sm"
                                :preventClickBubbling="false"
                                @clicked="
                                    () =>
                                        copyText(
                                            selectedCampaignInfo?.joinCode!,
                                        )
                                "
                            />
                        </TimedTooltip>
                        <div
                            class="pointer-events-none -order-1 flex w-full justify-start rounded-lg border border-take-navy-dark bg-take-navy-dark p-1 px-2 text-center transition-colors peer-hover:border-take-yellow"
                        >
                            {{ selectedCampaignInfo?.joinCode }}
                        </div>
                    </div>
                </div>

                <div class="flex flex-col gap-2">
                    <label class="font-NovaCut text-take-yellow">URL</label>
                    <div class="group group flex w-full items-center gap-2">
                        <TimedTooltip tooltip="Copied!" class="peer">
                            <FormButton
                                icon="copy"
                                size="sm"
                                class="transition-colors"
                                buttonColour="take-navy-dark"
                                hoverButtonColour="take-yellow"
                                :preventClickBubbling="false"
                                @clicked="
                                    () =>
                                        copyText(
                                            `${config.public.webUrl}/join/${selectedCampaignInfo?.joinCode}`,
                                        )
                                "
                            />
                        </TimedTooltip>
                        <div
                            class="-order-1 flex w-full justify-start truncate rounded-lg border border-take-navy bg-take-navy-dark p-1 px-2 text-center transition-colors peer-hover:border-take-yellow"
                        >
                            {{
                                `${config.public.webUrl}/join/${selectedCampaignInfo?.joinCode}`
                            }}
                        </div>
                    </div>
                </div>
            </main>
        </Modal>
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
import Modal from "~/components/Modal.vue";
import type { CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import type { JoinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";

const nav = useNavigator();
const config = useRuntimeConfig();
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
const shareCampaignModal = ref<InstanceType<typeof Modal> | null>(null);

// Route info
const routeInfo = useRoute();
const isCampaignsRoute = computed(() =>
    routeInfo.name?.toString().startsWith("campaign-id"),
);
const selectedCampaignInfo = computed(() => useCampaignStore().state);

const isCombatRoute = computed(() => routeInfo.name == "combat-id");
const combatInfo = computed(() => useCombatStore().state?.combat);

// Helper methods
function copyText(value: string) {
    navigator.clipboard.writeText(value);
}

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
</script>

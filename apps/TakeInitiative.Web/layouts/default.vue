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
                        <label for="drawer" class="cursor-pointer flex gap-2 items-center">
                            <img
                                class="h-[3em] w-[3em]"
                                src="~assets/yellowDice.png"
                            />
                            <label class="text-2xl md:text-3xl font-NovaCut text-take-yellow" v-if="!isMobile">Take Initiative</label>
                        </label>
                    </section>
                    <section class="navbar-end flex items-center gap-2">
                        <label class="font-NovaCut text-xl text-take-yellow">{{
                            userStore.selectedCampaignDto?.campaignName
                        }}</label>
                        <FormButton
                            icon="share-from-square"
                            size="sm"
                            textColour=""
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
                <li v-if="isIndexRoute">
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
                                    userStore.state.selectedCampaignId,
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
                <li v-else>
                    <FormButton
                        icon="house"
                        label="Return Home"
                        buttonColour="take-navy-dark"
                        hoverButtonColour="take-yellow"
                        @clicked="
                            () => {
                                navigateTo('/');
                                toggleDrawer();
                            }
                        "
                    />
                </li>
                <li class="flex-1 bg-take-navy"></li>
                <li>
                    <div class="text-take-yellow font-NovaCut hover:bg-take-navy">{{ userStore.username }}</div>
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
                                            userStore.selectedCampaignDto
                                                ?.joinCode!,
                                        )
                                "
                            />
                        </TimedTooltip>
                        <div
                            class="pointer-events-none -order-1 flex w-full justify-start rounded-lg border border-take-navy-dark bg-take-navy-dark p-1 px-2 text-center transition-colors peer-hover:border-take-yellow"
                        >
                            {{ userStore.selectedCampaignDto?.joinCode }}
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
                                            `${config.public.webUrl}/join/${userStore.selectedCampaignDto?.joinCode}`,
                                        )
                                "
                            />
                        </TimedTooltip>
                        <div
                            class="-order-1 flex w-full justify-start truncate rounded-lg border border-take-navy bg-take-navy-dark p-1 px-2 text-center transition-colors peer-hover:border-take-yellow"
                        >
                            {{
                                `${config.public.webUrl}/join/${userStore.selectedCampaignDto?.joinCode}`
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


const {isMobile} = useDevice();
const drawerToggle = ref<HTMLInputElement | null>(null);
function toggleDrawer() {
    if (drawerToggle.value) {
        drawerToggle.value.checked = !drawerToggle.value?.checked;
    }
}

const createOrJoinCampaignModal = ref<InstanceType<typeof Modal> | null>(null);
const shareCampaignModal = ref<InstanceType<typeof Modal> | null>(null);
const userStore = useUserStore();
const routeInfo = useRoute();
const isIndexRoute = computed(() => routeInfo.name == "index");
const isCampaignsRoute = computed(() => routeInfo.name == "campaigns");
const isCombatRoute = computed(() => routeInfo.name == "combat-id");

const config = useRuntimeConfig();
const manageUserState = reactive({
    loggingOut: false,
});

function onSetSelectedCampaign(campaignId: string) {
    userStore.setSelectedCampaign(campaignId);
    toggleDrawer();
}

function copyText(value: string) {
    navigator.clipboard.writeText(value);
}

async function createCampaign(req: CreateCampaignRequest) {
    return await useUserStore()
        .createCampaign(req)
        .then(async (c) => {
            userStore.setSelectedCampaign(c.id);
        })
        .then(createOrJoinCampaignModal.value?.hide);
}

async function joinCampaign(req: JoinCampaignRequest) {
    return await useUserStore()
        .joinCampaign(req)
        .then(async (c) => {
            userStore.setSelectedCampaign(c.id);
        })
        .then(createOrJoinCampaignModal.value?.hide);
}
</script>

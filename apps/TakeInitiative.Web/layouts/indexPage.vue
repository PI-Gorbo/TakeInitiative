<template>
    <div class="drawer drawer-end flex h-full bg-take-navy text-white">
        <input
            ref="drawerToggle"
            id="drawer"
            type="checkbox"
            class="drawer-toggle"
        />
        <div class="drawer-content w-full overflow-y-auto">
            <main class="flex h-full w-full flex-col">
                <header class="navbar">
                    <section class="navbar-start flex gap-2">
                        <img
                            class="h-[3em] w-[3em]"
                            src="~assets/yellowDice.png"
                        />
                        <label class="font-NovaCut text-xl text-take-yellow">{{
                            userStore.selectedCampaignDto?.campaignName
                        }}</label>
                        <FormButton
                            icon="share-from-square"
                            textColour="white"
                            size="sm"
                            buttonColour="take-navy"
                            hoverButtonColour="take-navy-dark"
                            @clicked="() => shareCampaignModal?.show()"
                        />
                    </section>
                    <section class="navbar-end">
                        <label
                            for="drawer"
                            class="btn cursor-pointer border-none bg-take-navy hover:bg-take-navy-dark"
                        >
                            <FontAwesomeIcon icon="bars" />
                        </label>
                    </section>
                </header>
                <div class="flex-1">
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
                <li>
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
                                :class="[
                                    'rounded-md',
                                    `bg-take-navy-dark hover:bg-take-navy-medium`,
                                ]"
                                @click="
                                    () => {
                                        createOrJoinCampaignModal?.show();
                                        toggleDrawer();
                                    }
                                "
                            >
                                <a
                                    ><FontAwesomeIcon icon="plus" /> Create or
                                    Join</a
                                >
                            </li>
                        </template>
                    </Dropdown>
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
                    <label>Campaign Code</label>
                    <div class="flex w-full items-center gap-2">
                        <div
                            class="flex w-full justify-start rounded-lg bg-take-navy p-1 px-2 text-center"
                        >
                            {{ userStore.selectedCampaignDto?.joinCode }}
                        </div>
                        <TimedTooltip tooltip="Copied!">
                            <FormButton
                                icon="copy"
                                buttonColour="take-navy"
                                hoverButtonColour="take-navy-medium"
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
                    </div>
                </div>

                <div class="flex flex-col gap-2">
                    <label>URL</label>
                    <div class="flex w-full items-center gap-2">
                        <div
                            class="flex w-full justify-start truncate rounded-lg bg-take-navy p-1 px-2 text-center"
                        >
                            {{
                                `${config.public.webUrl}/join/${userStore.selectedCampaignDto?.joinCode}`
                            }}
                        </div>
                        <TimedTooltip tooltip="Copied!">
                            <FormButton
                                icon="copy"
                                buttonColour="take-navy"
                                hoverButtonColour="take-navy-medium"
                                size="sm"
                                :preventClickBubbling="false"
                                @clicked="
                                    () =>
                                        copyText(
                                            `${config.public.webUrl}/join/${userStore.selectedCampaignDto?.joinCode}`,
                                        )
                                "
                            />
                        </TimedTooltip>
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
const drawerToggle = ref<HTMLInputElement | null>(null);
function toggleDrawer() {
    if (drawerToggle.value) {
        drawerToggle.value.checked = !drawerToggle.value?.checked;
    }
}

const createOrJoinCampaignModal = ref<InstanceType<typeof Modal> | null>(null);
const shareCampaignModal = ref<InstanceType<typeof Modal> | null>(null);
const userStore = useUserStore();
// const routeInfo = useRoute();
// const isIndexRoute = computed(() => routeInfo.name == "index");
// const isCampaignsRoute = computed(() => routeInfo.name == "campaigns");
// const isCombatRoute = computed(() => routeInfo.name == "combat-id");

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

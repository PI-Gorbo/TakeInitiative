<template>
    <main class="flex h-full flex-col overflow-auto bg-take-navy text-white">
        <header>
            <div
                class="flex select-none justify-center border-b-2 border-take-yellow bg-take-navy-medium px-4 py-1"
            >
                <div class="lg:w-7/10 flex max-w-[1200px] sm:w-full md:w-4/5 2xl:w-full">
                    <h1
                        @click="() => navigateTo('/')"
                        class="flex cursor-pointer items-center gap-2 font-NovaCut text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl"
                    >
                        <img class="h-[1.5em] w-[1.5em]" src="~assets/yellowDice.png" />
                        Take Initiative
                    </h1>
                    <section class="flex flex-1 justify-end gap-2">
                        <div class="flex items-center gap-2">
                            <TransitionGroup name="delayedFade">
                                <div
                                    v-if="isCombatRoute"
                                    class="flex items-center gap-2"
                                    key="CombatRoute"
                                >
                                    <article>
                                        <label
                                            class="font-NovaCut text-lg text-take-yellow"
                                        >
                                            {{
                                                campaignStore.state.campaign?.campaignName
                                            }}
                                            <span class="text-white">-</span>
                                            {{ combatStore.state.combat?.combatName }}
                                        </label>
                                    </article>
                                </div>

                                <div
                                    v-if="isIndexRoute"
                                    class="flex items-center gap-2"
                                    key="IndexRoute"
                                >
                                    <article class="flex flex-row items-center gap-1">
                                        <label
                                            for="campaigns"
                                            class="font-NovaCut text-take-yellow"
                                            >Campaigns:</label
                                        >
                                        <select
                                            name="campaigns"
                                            id="campaigns"
                                            class="rounded-lg bg-take-navy p-2"
                                            :value="userStore.state.selectedCampaignId"
                                            @change="
                                                (e) => {
                                                    const campaignId = (
                                                        e.target as HTMLSelectElement
                                                    ).value;
                                                    userStore.state.selectedCampaignId =
                                                        campaignId;
                                                }
                                            "
                                        >
                                            <option
                                                v-for="c in userStore.campaignList"
                                                :key="c.campaignId"
                                                :value="c.campaignId"
                                            >
                                                {{ c.campaignName }}
                                            </option>
                                        </select>
                                    </article>
                                    <FormButton
                                        icon="share-from-square"
                                        textColour="white"
                                        size="sm"
                                        buttonColour="take-navy-light"
                                        hoverButtonColour="take-yellow"
                                        @clicked="() => shareCampaignModal?.show()"
                                    />
                                </div>
                                <FormButton
                                    key="CampaignsRoute"
                                    v-if="!isCampaignsRoute"
                                    @clicked="() => navigateTo('/campaigns')"
                                    icon="flag-checkered"
                                    size="sm"
                                    textColour="white"
                                    buttonColour="take-navy-light"
                                    hoverButtonColour="take-yellow"
                                />
                            </TransitionGroup>
                            <FormButton
                                icon="user-gear"
                                size="sm"
                                textColour="white"
                                buttonColour="take-navy-light"
                                hoverButtonColour="take-yellow"
                                @clicked="() => manageUserModal?.show()"
                            />
                        </div>
                        <div
                            class="flex items-center px-2 py-1 font-NovaCut text-xl text-take-yellow"
                        >
                            <label>{{ userStore.username }}</label>
                        </div>
                    </section>
                </div>
            </div>
            <Modal ref="manageUserModal" title="Manage">
                <main class="flex flex-col items-center">
                    <FormButton
                        label="Logout"
                        loadingDisplay="Logging out..."
                        icon="right-from-bracket"
                        @clicked="onLogout"
                        :isLoading="manageUserState.loggingOut"
                    />
                </main>
            </Modal>
            <Modal ref="shareCampaignModal" title="Share">
                <main class="flex flex-col gap-4 text-white">
                    <div class="flex flex-col gap-2">
                        <label>Code</label>
                        <div
                            class="w-full rounded-lg bg-take-navy p-1 text-center text-xl"
                        >
                            {{ userStore.selectedCampaignDto?.joinCode }}
                        </div>
                    </div>

                    <div class="flex flex-col gap-2">
                        <label>Join Url</label>
                        <div
                            class="w-full rounded-lg bg-take-navy p-1 px-2 text-center text-xl"
                        >
                            {{
                                `${config.public.webUrl}/join/${userStore.selectedCampaignDto?.joinCode}`
                            }}
                        </div>
                    </div>
                </main>
            </Modal>
        </header>
        <div class="w-full flex-1">
            <slot />
        </div>
    </main>
</template>
<script setup lang="ts">
import Modal from "~/components/Modal.vue";
const manageUserModal = ref<InstanceType<typeof Modal> | null>(null);
const shareCampaignModal = ref<InstanceType<typeof Modal> | null>(null);
const userStore = useUserStore();
const combatStore = useCombatStore();
const campaignStore = useCampaignStore();
const routeInfo = useRoute();
const isIndexRoute = computed(() => routeInfo.name == "index");
const isCampaignsRoute = computed(() => routeInfo.name == "campaigns");
const isCombatRoute = computed(() => routeInfo.name == "combat-id");
const joinCode = computed(() => {
    if (isIndexRoute) {
        return userStore.selectedCampaignDto?.joinCode;
    }
});

const config = useRuntimeConfig();
const manageUserState = reactive({
    loggingOut: false,
});

async function onLogout() {
    manageUserState.loggingOut = true;
    await userStore.logout().finally(async () => {
        manageUserState.loggingOut = false;
        manageUserModal.value?.hide();
    });
}
</script>

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
                            <Transition name="fade">
                                <FormButton
                                    v-if="isIndexRoute"
                                    icon="share-from-square"
                                    textColour="white"
                                    size="sm"
                                    buttonColour="take-navy-light"
                                    hoverButtonColour="take-yellow"
                                    @clicked="() => shareCampaignModal?.show()"
                                />
                            </Transition>
                            <Transition name="fade">
                                <FormButton
                                    v-if="!isCampaignsRoute"
                                    @clicked="() => navigateTo('/campaigns')"
                                    icon="flag-checkered"
                                    size="sm"
                                    textColour="white"
                                    buttonColour="take-navy-light"
                                    hoverButtonColour="take-yellow"
                                />
                            </Transition>
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
                <main class="text-white flex flex-col gap-4">
                    <div class="flex gap-2 flex-col">
                        <label>Code</label>
                        <div
                            class="w-full text-center bg-take-navy rounded-lg text-xl p-1"
                        >
                            {{ userStore.selectedCampaignDto?.joinCode }}
                        </div>
                    </div>

                    <div class="flex gap-2 flex-col">
                        <label>Join Url</label>
                        <div
                            class="w-full text-center bg-take-navy rounded-lg text-xl p-1 px-2"
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
const routeInfo = useRoute();
const isIndexRoute = computed(() => routeInfo.name == "index");
const isCampaignsRoute = computed(() => routeInfo.name == "campaigns");
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

<template>
    <main class="flex h-full flex-col">
        <header>
            <div
                class="flex select-none justify-center border-b-2 border-take-yellow bg-take-navy-medium px-4 py-1"
            >
                <div
                    class="lg:w-7/10 flex max-w-[1200px] sm:w-full md:w-4/5 2xl:w-full"
                >
                    <h1
                        @click="() => navigateTo('/')"
                        class="flex cursor-pointer items-center gap-2 font-NovaCut text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl"
                    >
                        <img
                            class="h-[1.5em] w-[1.5em]"
                            src="~assets/yellowDice.png"
                        />
                        Take Initiative
                    </h1>
                    <section class="flex flex-1 justify-end gap-2">
                        <div
                            class="flex items-center px-2 py-1 font-NovaCut text-xl text-take-yellow"
                        >
                            <label>{{ userStore.username }}</label>
                        </div>
                    </section>
                </div>
            </div>
        </header>
        <div class="flex w-full flex-1 justify-center">
            <div
                class="lg:w-7/10 flex max-w-[1200px] flex-col sm:w-full md:w-4/5 2xl:w-full"
            >
                <section class="py-4">
                    <div class="flex justify-center">
                        <label>Campaigns</label>
                    </div>
                    <div class="flex justify-end gap-2">
                        <FormButton
                            label="Create a Campaign"
                            icon="plus"
                            size="sm"
                            buttonColour="take-navy-light"
                            hoverButtonColour="take-yellow"
                            @clicked="() => createCampaignModal?.show()"
                            textColour="white"
                        />
                        <FormButton
                            label="Join a Campaign"
                            icon="share-from-square"
                            size="sm"
                            buttonColour="take-navy-light"
                            hoverButtonColour="take-yellow"
                            @clicked="() => joinCampaignModal?.show()"
                            textColour="white"
                        />
                    </div>
                    <Modal ref="createCampaignModal" title="Create a campaign">
                        <CreateCampaignForm :submit="createCampaign" />
                    </Modal>
                    <Modal ref="joinCampaignModal" title="Join a campaign">
                        <JoinCampaignForm :submit="joinCampaign" />
                    </Modal>
                </section>
                <TransitionGroup
                    name="fade"
                    class="flex flex-col gap-4"
                    tag="div"
                >
                    <article
                        v-for="campaign in campaigns"
                        :key="campaign.id"
                        class="flex items-center justify-between gap-4 rounded-lg bg-take-navy-medium p-4"
                    >
                        <FormToggleableInput
                            v-model:value="campaign.campaignName"
                            buttonColour="take-navy-medium"
                            notEditableColour="take-navy-medium"
                            :onNotEditable="
                                async () => {
                                    return userStore.updateCampaign({
                                        campaignId: campaign.id,
                                        campaignName: campaign.campaignName,
                                    });
                                }
                            "
                        />
                        <div class="text-white">
                            <FontAwesomeIcon
                                class="text-take-yellow"
                                :icon="
                                    campaign.role == 'Player'
                                        ? 'user-large'
                                        : 'crown'
                                "
                            />
                            {{ campaign.role }}
                        </div>
                        <div class="flex gap-2">
                            <FormButton
                                icon="arrow-up-right-from-square"
                                size="sm"
                                buttonColour="take-navy-light"
                            />
                            <FormButton
                                icon="trash"
                                buttonColour="take-navy-light"
                                hoverButtonColour="take-red"
                                size="sm"
                                :click="() => deleteCampaign(campaign)"
                            />
                        </div>
                    </article>
                </TransitionGroup>
            </div>
        </div>
    </main>
</template>
<script setup lang="ts">
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import Modal from "~/components/Modal.vue";
import type { JoinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";
const userStore = useUserStore();
const createCampaignModal = ref<InstanceType<typeof Modal> | null>(null);
const joinCampaignModal = ref<InstanceType<typeof Modal> | null>(null);
const campaigns = computed(() =>
    userStore.state.user?.dmCampaigns
        .map((c) => ({
            id: c.campaignId,
            campaignName: c.campaignName,
            role: "DM",
        }))
        .concat(
            userStore.state.user.memberCampaigns.map((c) => ({
                id: c.campaignId,
                campaignName: c.campaignName,
                role: "Player",
            })),
        ),
);
definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});

async function deleteCampaign(campaign: { id: string }) {
    const userStore = useUserStore();
    return userStore
        .deleteCampaign({ campaignId: campaign.id })
        .then(async () => {
            if (userStore.campaignCount == 0) {
                await navigateTo("/createOrJoinCampaign");
            }
        });
}

async function createCampaign(req: CreateCampaignRequest) {
    return await userStore
        .createCampaign(req)
        .then((c) => userStore.setSelectedCampaign(c.id))
        .then(() => createCampaignModal.value?.hide());
}

async function joinCampaign(req: JoinCampaignRequest) {
    return await userStore
        .joinCampaign(req)
        .then((c) => userStore.setSelectedCampaign(c.id))
        .then(() => createCampaignModal.value?.hide());
}
</script>

<template>
    <main class="p-4">
        <section
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

            <div>
                <FontAwesomeIcon
                    class="text-take-yellow"
                    :icon="campaign.role == 'Player' ? 'user-large' : 'crown'"
                />
                {{ campaign.role }}
            </div>
            <div>
                <FormButton
                    icon="arrow-up-right-from-square"
                    size="sm"
                    buttonColour="take-navy-light"
                />
            </div>
        </section>
    </main>
</template>
<script setup lang="ts">
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
const userStore = useUserStore();

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
            }))
        )
);
definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>

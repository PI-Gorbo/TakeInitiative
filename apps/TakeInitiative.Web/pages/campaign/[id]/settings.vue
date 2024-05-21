<template>
    <main class="flex w-2/3 flex-col gap-4">
        <div class="w-fit">
            <FormToggleableInput
                label="Campaign Name"
                v-model:value="campaignName"
                buttonColour="take-navy-medium"
                notEditableColour="take-navy-medium"
                :onSave="
                    async () => {
                        return campaignStore.updateCampaignDetails({
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
                :click="
                    () =>
                        userStore.deleteCampaign({
                            campaignId: campaignStore.state.campaign?.id!,
                        })
                "
            />
        </div>
    </main>
</template>
<script setup lang="ts">
const userStore = useUserStore();
const campaignStore = useCampaignStore();
definePageMeta({
    requiresAuth: true,
    layout: "campaign-tabs",
});

const campaignName = ref<string>("");
onMounted(() => {
    campaignName.value = campaignStore.state.campaign?.campaignName!;
});
</script>

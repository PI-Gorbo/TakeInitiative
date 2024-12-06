
<template>
    <div>
        <FormButton
            label="Invite"
            icon="plus"
            size="sm"
            textColour="take-grey"
            buttonColour="take-purple-light"
            hoverButtonColour="take-navy-medium"
            @clicked="() => shareCampaignModal?.show()"
        />
        <Modal ref="shareCampaignModal" title="Invite">
            <main class="flex flex-col gap-4 text-take-grey-light">
                <div class="flex flex-col gap-2">
                    <label class="font-NovaCut text-take-yellow"
                    >Campaign Code</label
                    >
                    <div class="flex w-full items-center gap-2">
                        <TimedTooltip tooltip="Copied!" class="peer">
                            <FormButton
                                icon="copy"
                                buttonColour="take-purple-light"
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
                                buttonColour="take-purple-light"
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
    </div>
</template>

<script setup lang="ts">

import Modal from "base/components/Modal.vue";
const config = useRuntimeConfig();
const shareCampaignModal = ref<InstanceType<typeof Modal> | null>(null);
const campaignStore = useCampaignStore()
const selectedCampaignInfo = computed(() => campaignStore.state);
function copyText(value: string) {
    navigator.clipboard.writeText(value);
}

</script>

<style scoped>

</style>
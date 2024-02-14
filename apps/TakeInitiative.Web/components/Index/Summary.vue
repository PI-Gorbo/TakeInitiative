import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome';
<template>
    <main class="flex h-full flex-col items-center overflow-y-auto">
        <div class="grid w-full flex-1 grid-cols-2 gap-4 overflow-y-auto">
            <section class="flex flex-col gap-4 overflow-y-auto">
                <article class="flex flex-1 flex-col">
                    <div class="flex-1">
                        <div class="w-full border-b border-take-navy-light">
                            Campaign Name
                        </div>
                        <div
                            class="px-3 py-2 font-NovaCut text-lg text-take-yellow"
                        >
                            {{ campaignStore.state.campaign?.campaignName }}
                        </div>
                    </div>
                    <div class="flex-1">
                        <div class="w-full border-b border-take-navy-light">
                            Creation Date
                        </div>
                        <div
                            class="px-3 py-2 font-NovaCut text-lg text-take-yellow"
                        >
                            {{ campaignStore.state.campaign?.createdTimestamp }}
                        </div>
                    </div>
                    <div class="flex-1">
                        <div class="w-full border-b border-take-navy-light">
                            Last Fight Date
                        </div>
                        <div
                            class="px-3 py-2 font-NovaCut text-lg text-take-yellow"
                        >
                            ...
                        </div>
                    </div>
                </article>
                <section class="flex flex-1 flex-col overflow-y-auto">
                    <label class="block w-full border-b border-take-navy-light">
                        Players
                    </label>
                    <div class="overflow-y-auto px-3 py-2">
                        <!-- <HomePlayersDisplay
                            Owner="CampaignStore.CurrentCampaign.Owner"
                            Players="CampaignStore.CurrentCampaign.MemberInfos"
                        /> -->
                    </div>
                </section>
            </section>
            <section class="flex flex-col">
                <div class="flex w-full flex-1 flex-col overflow-y-auto">
                    <div class="w-full border-b border-take-navy-light">
                        Campaign Description
                    </div>
                    <div class="flex-1 px-3 py-2">
                        <textarea
                            class="h-full w-full rounded-lg bg-take-navy-light p-1"
                            :value="
                                campaignStore.state.campaign
                                    ?.campaignDescription
                            "
                        />
                    </div>
                </div>
                <div class="flex w-full flex-1 flex-col overflow-y-auto">
                    <div class="w-full border-b border-take-navy-light">
                        Resources
                    </div>
                    <div class="flex-1 px-3 py-2">
                        <textarea
                            :value="
                                campaignStore.state.campaign?.campaignResources
                            "
                            class="h-full w-full rounded-lg bg-take-navy-light p-1"
                        />
                    </div>
                </div>
                <div class="flex w-full justify-end">
                    <FormButton
                        class="w-max text-sm"
                        icon="floppy-disk"
                        :disabled="true"
                    />
                </div>
            </section>
        </div>
    </main>
</template>
<script setup lang="ts">
const campaignStore = useCampaignStore();
</script>

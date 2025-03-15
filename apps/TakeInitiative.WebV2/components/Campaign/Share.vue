<template>
    <main class="text-take-grey-light flex flex-col gap-4">
        <div class="flex flex-col gap-2">
            <label class="text-take-yellow font-NovaCut">Campaign Code</label>
            <div class="group flex w-full items-center gap-2">
                <div
                    class="flex w-full justify-start rounded-lg border p-1 px-2 text-center transition-colors group-hover:border-primary">
                    {{ selectedCampaignInfo?.joinCode }}
                </div>

                <CopiedTooltip
                    :textToCopyToClipboard="
                        selectedCampaignInfo?.joinCode ?? ''
                    ">
                    <template #Trigger="{ copyText }">
                        <Button
                            size="icon"
                            @click="copyText"
                            class="bg-secondary hover:bg-primary">
                            <FontAwesomeIcon :icon="faCopy" />
                        </Button>
                    </template>
                </CopiedTooltip>
            </div>
        </div>

        <div class="flex flex-col gap-2">
            <label class="text-take-yellow font-NovaCut">URL</label>
            <div class="group flex w-full items-center gap-2">
                <div
                    class="flex w-full justify-start rounded-lg border p-1 px-2 text-center transition-colors group-hover:border-primary">
                    {{
                        `${config.public.webUrl}/join/${selectedCampaignInfo?.joinCode}`
                    }}
                </div>
                <CopiedTooltip
                    :textToCopyToClipboard="`${config.public.webUrl}/join/${selectedCampaignInfo?.joinCode}`">
                    <template #Trigger="{ copyText }">
                        <Button
                            size="icon"
                            @click="copyText"
                            class="bg-secondary hover:bg-primary">
                            <FontAwesomeIcon :icon="faCopy" />
                        </Button>
                    </template>
                </CopiedTooltip>
            </div>
        </div>
    </main>
</template>
<script setup lang="ts">
    import { faCopy } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const config = useRuntimeConfig();
    const selectedCampaignInfo = computed(() => useCampaignStore().state);
</script>

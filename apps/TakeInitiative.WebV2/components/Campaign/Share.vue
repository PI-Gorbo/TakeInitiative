<template>
    <main class="text-take-grey-light flex flex-col text-start gap-4 pt-4">
        <div class="flex flex-col gap-2">
            <label class="text-take-yellow text-sm">Join link</label>
            <div class="group flex w-full items-center gap-2">
                <div
                    class="flex rounded-lg border p-1 px-2 text-center transition-colors group-hover:border-primary truncate">
                    {{
                        `${config.public.webUrl}/app/campaigns/join/${campaignQuery.data.value?.joinCode}`
                    }}
                </div>
                <CopiedTooltip
                    :textToCopyToClipboard="`${config.public.webUrl}/app/campaigns/join/${campaignQuery.data.value?.joinCode}`">
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
        <div class="flex flex-col gap-1">
            <label class="text-take-yellow text-sm">Campaign Code</label>
            <div class="group flex w-full items-center gap-2">
                <div
                    class="flex w-full rounded-lg border p-1 px-2 text-center transition-colors group-hover:border-primary truncate">
                    {{ campaignQuery.data.value?.joinCode }}
                </div>

                <CopiedTooltip
                    :textToCopyToClipboard="
                        campaignQuery.data.value?.joinCode ?? ''
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
    </main>
</template>
<script setup lang="ts">
    import { faCopy } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery } from "@tanstack/vue-query";
    import { getCampaignQuery } from "~/utils/queries/campaign";

    const config = useRuntimeConfig();
    const route = useRoute("app-campaigns-campaignId");
    const campaignQuery = useQuery({
        ...getCampaignQuery(() => route.params.campaignId),
    });
</script>

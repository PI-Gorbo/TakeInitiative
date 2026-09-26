<template>
    <NuxtLink :to="`/app/campaigns/${props.campaignId}`">
        <Card
            v-wave
            :class="['interactable group transition-all']">
            <CardHeader>
                <CardTitle class="flex items-center justify-between gap-2 px-4">
                    <div class="space-x-4 py-1">
                        <FontAwesomeIcon
                            :icon="faCrown"
                            class="text-gold" />
                        <span>{{ props.campaignName }}</span>
                    </div>
                </CardTitle>
            </CardHeader>
        </Card>
    </NuxtLink>
</template>

<script setup lang="ts">
    import { faCrown } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const props = defineProps<{
        campaignId: string;
        campaignName: string;
    }>();
</script>

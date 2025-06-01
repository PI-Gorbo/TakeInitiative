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
                    <div
                        v-if="props.currentCombatName"
                        class="rounded-md bg-gold text-gold-foreground px-2 py-1">
                        <FontAwesomeIcon :icon="faHandFist" />
                        {{ props.currentCombatName }}
                    </div>
                </CardTitle>
            </CardHeader>
        </Card>
    </NuxtLink>
</template>

<script setup lang="ts">
    import { faCrown, faHandFist } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const props = defineProps<{
        campaignId: string;
        campaignName: string;
        currentCombatName?: string;
    }>();
</script>

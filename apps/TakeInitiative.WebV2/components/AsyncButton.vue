<template>
    <Button>
        <slot :isLoading="computedIsLoading">
            <div class="space-x-1">
                <template v-if="computedIsLoading">
                    <FontAwesomeIcon :icon="faSpinner" class="fa-spin" />
                    <span class="h-fit">{{ props.loadingLabel }}</span>
                </template>
                <template v-else>
                    <FontAwesomeIcon v-if="props.icon" :icon="props.icon" />
                    <span class="h-fit">{{ props.label }}</span>
                </template>
            </div>
        </slot>
    </Button>
</template>

<script setup lang="ts">
    import {
        faSpinner,
        type IconDefinition,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const props = defineProps<{
        label: string;
        loadingLabel: string;
        click?: () => Promise<unknown>;
        isLoading?: boolean;
        icon?: IconDefinition;
    }>();

    const _isLoading = ref(false);
    const computedIsLoading = computed(
        () => _isLoading.value || props.isLoading
    );
</script>

<template>
    <Button @click="onClick">
        <slot :isLoading="computedIsLoading">
            <div class="space-x-1">
                <template v-if="computedIsLoading">
                    <FontAwesomeIcon :icon="faSpinner" class="fa-spin" />
                    <span class="h-fit" v-if="props.loadingLabel">{{ props.loadingLabel }}</span>
                </template>
                <template v-else>
                    <FontAwesomeIcon v-if="props.icon" :icon="props.icon" />
                    <span class="h-fit" v-if="props.label">{{ props.label }}</span>
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
        label?: string;
        loadingLabel?: string;
        click?: () => Promise<unknown>;
        isLoading?: boolean;
        icon?: IconDefinition;
    }>();

    const _isLoading = ref(false);
    const computedIsLoading = computed(
        () => _isLoading.value || props.isLoading
    );

    async function onClick() {
        if (_isLoading.value) {
            return;
        }

        _isLoading.value = true;
        await Promise.resolve(props.click?.()).finally(() => {
            _isLoading.value = false;
        });
    }
</script>

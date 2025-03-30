<template>
    <component :is="props.container" class="w-full h-full">
        <Transition name="fade" mode="out-in">
            <div
                v-if="props.isLoading"
                class="w-full flex flex-col items-center gap-2">
                <FontAwesomeIcon :icon="faDiceD20" class="fa-spin" size="5x" />
                <div>loading...</div>
            </div>
            <div v-else>
                <slot />
            </div>
        </Transition>
    </component>
</template>

<script setup lang="ts">
    import {
        faCircleNotch,
        faDiceD20,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const props = withDefaults(
        defineProps<{
            isLoading: boolean;
            container: string | Comment;
        }>(),
        {
            container: "div",
        }
    );
</script>

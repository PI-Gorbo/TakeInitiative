<template>
    <component
        :is="props.container"
        class="w-full h-full">
        <!-- `class` spaces out the spinner and the error only. Falling through to the
             container too padded the content twice (a blank band under the campaign header). -->
        <Transition
            name="fade"
            mode="out-in">
            <div
                v-if="props.isLoading"
                :class="cn(['h-full w-full flex flex-col items-center gap-2', $attrs.class])">
                <FontAwesomeIcon
                    :icon="faDiceD20"
                    class="fa-spin"
                    :size="props.iconSize" />
                <div>loading...</div>
            </div>
            <div
                v-else-if="props.isError"
                :class="cn(['h-full w-full flex flex-col items-center gap-2', $attrs.class])">
                Something went wrong!
            </div>
            <div
                v-else
                class="w-full h-full">
                <slot />
            </div>
        </Transition>
    </component>
</template>

<script setup lang="ts">
    import { cn } from "@/lib/utils";
    import {
        faCircleNotch,
        faDiceD20,
    } from "@fortawesome/free-solid-svg-icons";
    import {
        FontAwesomeIcon,
        type FontAwesomeIconProps,
    } from "@fortawesome/vue-fontawesome";
    import type { Component } from "vue";

    defineOptions({ inheritAttrs: false });

    const props = withDefaults(
        defineProps<{
            isLoading: boolean;
            isError?: boolean;
            container?: string | Component;
            iconSize?: FontAwesomeIconProps["size"];
        }>(),
        {
            container: "div",
            iconSize: "5x",
            isError: false,
        }
    );
</script>

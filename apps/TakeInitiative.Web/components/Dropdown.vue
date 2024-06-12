<template>
    <li class="menu">
        <details
            ref="details"
            class="dropdown text-base text-white"
            :class="{
                relative: props.hoverOverContent,
            }"
        >
            <summary
                :class="[`bg-${props.colour} hover:bg-${props.hoverColour}`]"
            >
                {{
                    props.selectedItem != null
                        ? (props.headerLabel ? props.headerLabel + " " : "") +
                          props.displayFunc(props.selectedItem)
                        : labelFallback
                }}
            </summary>
            <ul
                class="z-50 flex flex-col gap-1 p-2"
                :class="{
                    absolute: props.hoverOverContent,
                }"
            >
                <li
                    v-for="item in props.items"
                    :key="props.keyFunc(item)"
                    @click="() => onSelectedItem(item)"
                    :class="[
                        'rounded-md',
                        `bg-${props.colour} hover:bg-${props.hoverColour}`,
                    ]"
                >
                    <a>{{ props.displayFunc(item) }}</a>
                </li>
                <slot name="Footer"></slot>
            </ul>
        </details>
    </li>
</template>
<script setup lang="ts" generic="TListItem">
import type { TakeInitColour } from "base/utils/types/HelperTypes";

const details = ref<HTMLDetailsElement | null>(null);
const props = withDefaults(
    defineProps<{
        selectedItem: TListItem | undefined | null;
        items: TListItem[];
        displayFunc: (item: TListItem) => string;
        keyFunc: (item: TListItem) => string;
        labelFallback?: string;
        headerLabel?: string;
        colour: TakeInitColour;
        hoverColour: TakeInitColour;
        hoverOverContent?: boolean;
    }>(),
    {
        colour: "take-navy",
        hoverColour: "take-navy-dark",
        hoverOverContent: false,
    },
);

const emit = defineEmits<{
    (e: "update:selectedItem", value: TListItem): void;
}>();

function onSelectedItem(item: TListItem) {
    emit("update:selectedItem", item);
    if (details.value) {
        details.value.open = false;
    }
}
</script>

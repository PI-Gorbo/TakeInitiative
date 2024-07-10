<template>
    <li class="menu p-0">
        <label class="p-0 pb-1" v-if="props.label">{{ props.label }}</label>
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
                        ? computedSelectedItemDisplayFunc(props.selectedItem)
                        : selectedItemFallbackDisplay
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
        selectedItemDisplayFunc?: (item: TListItem) => string;
        displayFunc: (item: TListItem) => string;
        keyFunc: (item: TListItem) => string;
        label?: string | undefined;
        selectedItemFallbackDisplay?: string;
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

const computedSelectedItemDisplayFunc = computed(
    () => props.selectedItemDisplayFunc ?? props.displayFunc,
);
</script>

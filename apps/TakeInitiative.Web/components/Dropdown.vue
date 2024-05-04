<template>
    <details ref="details" class="dropdown text-base text-white">
        <summary :class="[`bg-${props.colour} hover:bg-${props.hoverColour}`]">
            Campaign:
            {{
                props.selectedItem != null
                    ? props.displayFunc(props.selectedItem)
                    : labelFallback
            }}
        </summary>
        <ul class="flex flex-col gap-1 p-2">
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
</template>
<script setup lang="ts" generic="TListItem">
import type { TakeInitColour } from "~/utils/types/HelperTypes";

const details = ref<HTMLDetailsElement | null>(null);
const props = withDefaults(
    defineProps<{
        selectedItem: TListItem | undefined | null;
        items: TListItem[];
        displayFunc: (item: TListItem) => string;
        keyFunc: (item: TListItem) => string;
        labelFallback: string;
        colour: TakeInitColour;
        hoverColour: TakeInitColour;
    }>(),
    {
        colour: "take-navy",
        hoverColour: "take-navy-dark",
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

<template>
    <li class="menu p-0">
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
                    relative: props.hoverOverContent,
                }"
            >
                <summary
                    :class="[
                        `bg-${props.colour} hover:bg-${props.hoverColour}`,
                    ]"
                >
                    {{
                        props.selectedItem != null
                            ? (props.inDropdownLabel
                                  ? props.inDropdownLabel + " "
                                  : "") + props.displayFunc(props.selectedItem)
                            : labelFallback
                    }}
                </summary>
                <ul
                    class="z-[999] flex flex-col gap-1 py-2"
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
                            `bg-${props.colour} hover:bg-${props.hoverColour} border border-${elementBorderColour}`,
                        ]"
                    >
                        <a>{{ props.displayFunc(item) }}</a>
                    </li>
                    <slot name="Footer"></slot>
                </ul>
            </details>
        </li>
    </div>
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
        inDropdownLabel?: string;
        headerLabel?: string;
        colour?: TakeInitColour;
        hoverColour?: TakeInitColour;
        hoverOverContent?: boolean;
        elementBorderColour: TakeInitColour;
    }>(),
    {
        colour: "take-navy",
        hoverColour: "take-navy-dark",
        hoverOverContent: false,
        elementBorderColour: 'take-grey-dark'
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

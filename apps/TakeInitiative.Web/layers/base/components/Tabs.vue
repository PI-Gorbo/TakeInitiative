<template>
    <main class="flex h-full w-full flex-col overflow-hidden">
        <div class="flex gap-2">
            <nav
                :class="[
                    'flex flex-row gap-6 overflow-auto rounded-lg px-2 py-2 ',
                    `bg-${props.backgroundColour}`,
                ]"
            >
                <div
                    v-for="tab in tabs.filter((x) => x.show)"
                    :key="tab.slotName"
                    :class="[
                        state.lastClickedTab.slotName == tab.slotName
                            ? `bg-${props.selectedTabColour} text-${
                                  TakeInitContrastColour[
                                      props.selectedTabColour
                                  ]
                              }`
                            : `bg-${props.notSelectedTabColour} text-take-navy-light`, // PLEASE CHANGE TO MANUALLY CONTROLLED TEXT COLOUR
                        `flex cursor-pointer select-none items-center rounded-md px-2 py-1 transition-colors md:text-lg hover:bg-${
                            props.hoveredTabColour
                        } hover:text-${
                            TakeInitContrastColour[props.hoveredTabColour]
                        }  font-NovaCut`,
                    ]"
                    @click="() => (state.lastClickedTab = tab)"
                >
                    <div>{{ tab.name }}</div>
                </div>
            </nav>

            <div
                v-if="props.negativeSectionId"
                :id="props.negativeSectionId"
                class="flex-1"
            ></div>
        </div>
        <div class="h-full w-full overflow-hidden">
            <slot :name="selectedTab.slotName" />
        </div>
    </main>
</template>
<script setup lang="ts">
import { ObjectSchema } from "yup";
import type { TakeInitColour } from "../layers/base/utils/types/HelperTypes";
import { TakeInitContrastColour } from "../layers/base/utils/types/HelperTypes";
import gsap from "gsap";

const slots = useSlots();
type Tab = {
    slotName: string;
    name: string;
    show: boolean;
};

const props = withDefaults(
    defineProps<{
        showTabs?: Record<string, () => boolean>;
        renameTabs?: Record<string, string>;
        backgroundColour?: TakeInitColour;
        notSelectedTabColour?: TakeInitColour;
        selectedTabColour?: TakeInitColour;
        hoveredTabColour?: TakeInitColour;
        negativeSectionId?: string;
    }>(),
    {
        showTabs: {},
        renameTabs: {},
        backgroundColour: "take-navy",
        hoveredTabColour: "take-yellow",
        selectedTabColour: "take-navy-light",
        notSelectedTabColour: "take-navy-medium",
    },
);

const tabs: ComputedRef<Tab[]> = computed(() => {
    return Object.keys(slots).map((slotName) => {
        const showTabFunction = props.showTabs[slotName];
        return {
            slotName,
            name: props.renameTabs[slotName] ?? slotName,
            show: showTabFunction != null ? showTabFunction() : true,
        };
    });
});

const state = reactive({
    lastClickedTab: tabs.value.filter((x) => x.show)[0] ?? {},
});

const selectedTab = computed(
    () =>
        tabs.value.find((x) => x.name == state.lastClickedTab.name) ??
        tabs.value.filter((x) => x.show)[0] ??
        {},
);
</script>
<style>
/*
  Enter and leave animations can use different
  durations and timing functions.
*/
.tabFade-enter-active,
.tabFade-leave-active {
    transition: all 0.25s ease-in-out;
}

.tabFade-enter-active {
    display: none;
}

.tabFade-enter-from,
.tabFade-leave-to {
    opacity: 0;
}
</style>

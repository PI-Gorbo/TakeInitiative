<template>
    <main class="flex h-full w-full flex-col">
        <nav
            :class="[
                'mb-2 flex w-max flex-row gap-4 overflow-x-auto overflow-y-hidden rounded-lg px-4 py-2',
                `bg-${props.backgroundColour}`,
            ]"
        >
            <div
                v-for="tab in tabs.filter((x) => x.show)"
                :key="tab.name"
                :class="[
                    ' cursor-pointer select-none rounded-md p-2 text-center transition-colors hover:bg-take-yellow hover:text-take-navy-dark',
                    state.lastClickedTab.name == tab.name
                        ? `bg-${props.selectedTabColour}`
                        : `bg-${props.notSelectedTabColour}`,
                ]"
                @click="() => (state.lastClickedTab = tab)"
            >
                <div>
                    {{ tab.name }}
                </div>
            </div>
        </nav>
        <Transition
            name="slide-fade"
            mode="out-in"
            class="flex-1 overflow-auto"
        >
            <slot :name="selectedTab.name" />
        </Transition>
    </main>
</template>
<script setup lang="ts">
import { ObjectSchema } from "yup";
import type { TakeInitColour } from "~/utils/types/HelperTypes";

const slots = useSlots();
type Tab = {
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
    }>(),
    {
        showTabs: {},
        renameTabs: {},
        backgroundColour: "take-navy",
        selectedTabColour: "take-navy-light",
        notSelectedTabColour: "take-navy-medium",
    },
);

const tabs: ComputedRef<Tab[]> = computed(() => {
    return Object.keys(slots).map((slotName) => {
        const showTabFunction = props.showTabs[slotName];
        return {
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
.slide-fade-enter-active {
    transition: all 0.2s ease-out;
}

.slide-fade-leave-active {
    transition: all 0.2s cubic-bezier(1, 0.5, 0.8, 1);
}

.slide-fade-enter-from,
.slide-fade-leave-to {
    opacity: 0;
}
</style>

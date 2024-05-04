<template>
    <header
        class="flex w-full flex-wrap py-2 text-sm sm:flex-nowrap sm:justify-start"
    >
        <nav
            class="mx-auto w-full max-w-[85rem] rounded-md bg-take-navy-light px-2 py-1 sm:flex sm:items-center sm:justify-between"
            aria-label="Global"
        >
            <div class="flex items-center justify-between">
                <div>
                    <slot name="Left" />
                </div>
                <div class="sm:hidden">
                    <button
                        type="button"
                        class="hs-collapse-toggle inline-flex items-center justify-center gap-x-2 rounded-lg bg-take-navy-light p-2 text-white shadow-sm hover:bg-take-yellow hover:text-take-navy disabled:pointer-events-none disabled:opacity-50"
                        data-hs-collapse="#navbar-collapse-with-animation"
                        aria-controls="navbar-collapse-with-animation"
                        aria-label="Toggle navigation"
                        ref="collapseButton"
                    >
                        <svg
                            class="size-4 flex-shrink-0 hs-collapse-open:hidden"
                            xmlns="http://www.w3.org/2000/svg"
                            width="24"
                            height="24"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="2"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                        >
                            <line x1="3" x2="21" y1="6" y2="6" />
                            <line x1="3" x2="21" y1="12" y2="12" />
                            <line x1="3" x2="21" y1="18" y2="18" />
                        </svg>

                        <svg
                            class="hidden size-4 flex-shrink-0 hs-collapse-open:block"
                            xmlns="http://www.w3.org/2000/svg"
                            width="24"
                            height="24"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="2"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                        >
                            <path d="M18 6 6 18" />
                            <path d="m6 6 12 12" />
                        </svg>
                    </button>
                </div>
            </div>
            <div
                id="navbar-collapse-with-animation"
                class="hs-collapse hidden grow basis-full overflow-hidden transition-all duration-300 sm:block"
            >
                <div
                    class="mt-5 flex cursor-pointer select-none flex-col gap-5 sm:mt-0 sm:flex-row sm:items-center sm:justify-end sm:ps-5"
                >
                    <a
                        v-for="(item, index) in props.menuItems"
                        :key="index"
                        class="font-medium"
                        @click="() => emit('menuItemClicked', item)"
                        >{{ item.label }}</a
                    >
                </div>
            </div>
        </nav>
    </header>
</template>
<script setup lang="ts">
const collapseButton = ref<HTMLElement | null>(null);
export type CollapsableMenuItem = {
    label: string;
    hasNotificationIcon:
        | {
              value: string;
          }
        | boolean;
};
const props = withDefaults(
    defineProps<{
        menuItems: CollapsableMenuItem[];
    }>(),
    {},
);

const emit = defineEmits<{
    (e: "menuItemClicked", item: CollapsableMenuItem): void;
}>();

defineExpose({
    hide: () => {
        const query = window.matchMedia("(max-width: 640px)");
        if (query.matches) {
            collapseButton.value!.click();
        }
    },
});
</script>

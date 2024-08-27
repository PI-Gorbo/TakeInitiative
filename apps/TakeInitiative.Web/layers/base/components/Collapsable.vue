<template>
    <main class="collapse bg-take-purple-dark">
        <input
            ref="inputRef"
            type="checkbox"
            class="min-h-fit p-2"
            @click="() => (isOpen = !isOpen)"
        />
        <div class="collapse-title min-h-fit p-2 leading-normal">
            <section class="flex items-center justify-between">
                <slot name="Header" :isOpen="isOpen" />
                <FontAwesomeIcon
                    :icon="isOpen ? faChevronUp : faChevronDown"
                    class="px-1"
                />
            </section>
        </div>
        <div class="collapse-content">
            <slot name="Content" :isOpen="isOpen" />
        </div>
    </main>
</template>
<script setup lang="ts">
import { faChevronDown, faChevronUp } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

const inputRef = ref<HTMLInputElement | null>(null);
const isOpen = ref(false);
</script>

<template>
    <div class="mx-auto w-full max-w-5xl">
        <slot />
    </div>
</template>

<script setup lang="ts">
    // Every campaign page's column: one width, so the tabs line up. The page adds its
    // own layout and padding with `class`. The template keeps the div as its only root
    // node: pages use this as theirs, and a page root must be a single element.
</script>

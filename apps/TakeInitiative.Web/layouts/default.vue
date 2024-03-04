<template>
    <body class="h-full overflow-auto bg-take-navy text-white">
        <slot />
    </body>
</template>
<script setup lang="ts">
const userStore = useUserStore();
</script>

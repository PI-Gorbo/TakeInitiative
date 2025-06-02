<template>
    <Badge variant="outline" class="flex gap-1 border-2">
        <FontAwesomeIcon :icon="faDiceD20" />
        {{ props.initiative }}
    </Badge>
</template>

<script setup lang="ts">
    import { faDiceD20 } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import Badge from "~/components/ui/badge/Badge.vue";

    const props = defineProps<{
        initiative: string;
    }>();
</script>

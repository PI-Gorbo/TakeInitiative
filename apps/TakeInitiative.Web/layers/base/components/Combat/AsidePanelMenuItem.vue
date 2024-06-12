<template>
    <li
        :class="[
            'flex cursor-pointer select-none items-center gap-2 p-2',
            'hover:text-take-navy',
            props.highlighted
                ? 'bg-take-yellow text-take-navy'
                : 'hover:bg-take-grey',
            ,
            '',
        ]"
        @click="() => emit('click')"
    >
        <FontAwesomeIcon :icon="props.icon" />
        <h3 class="px-2 text-lg">{{ props.label }}</h3>
    </li>
</template>
<script setup lang="ts">
    import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome';
    const props = defineProps<{
        highlighted: boolean
        icon: string
        label: string
    }>()

    const emit = defineEmits<{
        (e: 'click') : void
    }>()
</script>

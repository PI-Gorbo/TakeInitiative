<template>
    <form @submit.prevent="true" @submit="submit">
        <slot :submitting="state.submitting" />
    </form>
</template>

<script setup lang="ts">
const state = reactive({
    submitting: false,
});

const props = defineProps<{
    onSubmit: () => Promise<void>;
}>();

async function submit() {
    state.submitting = true;
    await Promise.resolve(props.onSubmit()).finally(() => {
        state.submitting = false;
    });
}

defineSlots<{
    default(props: { submitting: boolean }): any;
}>();
</script>

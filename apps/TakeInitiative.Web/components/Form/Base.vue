<template>
    <form @submit.prevent="true" @submit="submit">
        <slot :submitting="state.submitting" />
    </form>
</template>

<script setup lang="ts">
const state = reactive({
    submitting: null as null | SubmittingState,
});

const props = defineProps<{
    onSubmit: () => Promise<any>;
}>();

export type SubmittingState = {
    submitterId: string;
};

async function submit(event: SubmitEvent) {
    const submitter = event.submitter as HTMLButtonElement;

    console.log(event);
    state.submitting = { submitterId: submitter.id };
    await Promise.resolve(props.onSubmit()).finally(() => {
        state.submitting = null;
    });
}

defineSlots<{
    default(props: { submitting: null | SubmittingState }): any;
}>();
</script>

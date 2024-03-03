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
    onSubmit: (state: SubmittingState) => Promise<any>;
}>();

export type SubmittingState = {
    submitterId: string;
    submitterName: string;
};

async function submit(event: SubmitEvent) {
    const submitter = event.submitter as HTMLButtonElement;
    state.submitting = { submitterId: submitter.id, submitterName: submitter.name };
    await Promise.resolve(props.onSubmit(state.submitting)).finally(() => {
        state.submitting = null;
    });
}

defineSlots<{
    default(props: { submitting: null | SubmittingState }): any;
}>();
</script>

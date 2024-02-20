<template>
    <FormBase
        :onSubmit="onCreatePlannedCombat"
        v-slot="{ submitting }"
        class="flex w-1/2 flex-col gap-4"
    >
        <FormInput
            class="text-white"
            label="Combat Name"
            :value="props.campaignName"
            v-bind="props.inputProps"
            @update:value="(val) => emit('update:campaignName', val)"
            colour="take-navy-light"
        />
        <div class="flex justify-center">
            <FormButton
                :isLoading="submitting"
                label="Create"
                type="submit"
                loadingDisplay="Creating..."
            />
        </div>
    </FormBase>
</template>

<script setup lang="ts">
// TODO : Refactor to be the smae as the CreateCampaignForm
import FormInput from "~/components/Form/Input.vue";
type FormInputProps = InstanceType<typeof FormInput>["$props"];
const props = defineProps<{
    onCreatePlannedCombat: () => Promise<void | undefined>;
    campaignName: string | undefined;
    inputProps: Omit<FormInputProps, "value">;
}>();

const emit = defineEmits<{
    (e: "update:campaignName", campaignName: string | undefined): void;
}>();
</script>

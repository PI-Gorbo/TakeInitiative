<template>
    <FormBase
        :onSubmit="submit"
        v-slot="{ submitting }"
        class="flex w-1/2 flex-col gap-4"
    >
        <FormInput
            class="text-white"
            label="Combat Name"
            v-bind="createPlannedCombatForm.combatName.props.value"
            :value="createPlannedCombatForm.combatName.value.value"
            @update:value="
                (val) =>
                    (createPlannedCombatForm.combatName.value.value =
                        String(val) ?? '')
            "
            colour="take-navy-light"
            :autoFocus="true"
        />
        <div class="flex justify-center">
            <FormButton
                :isLoading="submitting"
                label="Create"
                type="submit"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Creating...',
                }"
            />
        </div>
    </FormBase>
</template>

<script setup lang="ts">
// TODO : Refactor to be the smae as the CreateCampaignForm
type FormInputProps = InstanceType<typeof FormInput>["$props"];
import FormInput from "base/components/Form/Input.vue";
import type { CreatePlannedCombatRequest } from "base/utils/api/plannedCombat/createPlannedCombatRequest";
const createPlannedCombatForm = useCreatePlannedCombatForm();

const props = defineProps<{
    onCreatePlannedCombat: (
        input: void | Omit<CreatePlannedCombatRequest, "campaignId">,
    ) => Promise<any>;
}>();

const emit = defineEmits<{
    (e: "update:campaignName", campaignName: string | undefined): void;
}>();

async function submit(): ReturnType<typeof createPlannedCombatForm.submit> {
    return await createPlannedCombatForm
        .submit()
        .then(props.onCreatePlannedCombat)
        .catch(async (error) =>
            createPlannedCombatForm.setError(await parseAsApiError(error)),
        );
}
</script>

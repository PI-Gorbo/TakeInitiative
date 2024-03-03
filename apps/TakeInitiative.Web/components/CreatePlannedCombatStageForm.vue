<template>
    <FormBase
        :onSubmit="submit"
        v-slot="{ submitting }"
        class="flex w-full flex-col gap-4"
    >
        <FormInput
            class="text-white"
            label="Stage Name"
            v-bind="createPlannedCombatStageForm.name.props.value"
            :value="createPlannedCombatStageForm.name.value.value"
            @update:value="
                (val) =>
                    (createPlannedCombatStageForm.name.value.value = String(val) ?? '')
            "
            colour="take-navy-light"
            :autoFocus="true"
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
import FormInput from "~/components/Form/Input.vue";
import type { CreatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
const createPlannedCombatStageForm = useCreatePlannedCombatStageForm();
const props = defineProps<{
    onSubmit: (
        input: void | Omit<CreatePlannedCombatStageRequest, "combatId">
    ) => Promise<any>;
}>();

async function submit(): ReturnType<typeof createPlannedCombatStageForm.submit> {
    return await createPlannedCombatStageForm
        .submit()
        .then(props.onSubmit)
        .catch(async (error) => {
            createPlannedCombatStageForm.setError(await parseAsApiError(error));
        });
}
</script>

<template>
    <form @submit.prevent="debouncedSubmit.debouncedSubmit" class="text-left">
        <FormFieldWrapper :error="form.errors.value.name">
            <template #default>
                <div class="flex items-center gap-2">
                    <Input v-model:modelValue="name" @input="debouncedSubmit.debouncedSubmit"/>
                    <AsyncSuccessIcon :state="debouncedSubmit.state.value"/>
                </div>
            </template>
        </FormFieldWrapper>
    </form>
</template>
<script setup lang="ts">
import {toTypedSchema} from "@vee-validate/zod";
import {useDebounceFn} from "@vueuse/core";
import {useForm} from "vee-validate";
import {toast} from "vue-sonner";
import {z} from "zod";

const props = defineProps<{
    initalStageName: string;
    allStageNames: string[]; // We assume this may include the current stage name.
    updateStageName: (name: string) => Promise<unknown>;
}>();

const allStageNamesExceptCurrent = computed(() => {
    return props.allStageNames.filter(
        (name) => name !== props.initalStageName
    );
});

const schema = z.object({
    name: z
        .string()
        .nonempty("Please provide a stage name")
        .refine((name) => {
            return !allStageNamesExceptCurrent.value.includes(name);
        }, "Stage name already exists"),
});

const form = useForm({
    validationSchema: toTypedSchema(schema),
    initialValues: {
        name: props.initalStageName,
    },
});

const [name] = form.defineField("name");


const _submit = form.handleSubmit(async (values) => {
    return await props.updateStageName(values.name)
        .then(() => toast.success("Updated stage name"))
});
const debouncedSubmit = useDebouncedAsyncFn(
    _submit,
    {
        isSuccess: (r) => r != undefined
    }
);
</script>

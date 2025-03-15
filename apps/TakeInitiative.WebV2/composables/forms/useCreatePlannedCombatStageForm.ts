import { useForm } from "vee-validate";
import type { CreatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import { toTypedSchema } from "@vee-validate/zod";
import { z } from "zod";

export const useCreatePlannedCombatStageForm = () => {
    const formState = reactive({
        error: null as ApiError<CreatePlannedCombatStageRequest> | null,
    });

    // Form Definition
    const { values, errors, defineField, validate, setFieldError } = useForm({
        validationSchema: toTypedSchema(
            z
                .object({
                    name: z.string(),
                })
                .required()
        ),
    });

    const [name, nameInputProps] = defineField("name", {
        props: (state) => ({
            errorMessage:
                formState.error?.errors?.name ?? state.errors[0],
        }),
    });

    // Form Submit
    async function submit(): Promise<void | Omit<
        CreatePlannedCombatStageRequest,
        "combatId"
    >> {
        formState.error = null;
        return await validate()
            .then((result) => {
                if (!result.valid) {
                    Promise.reject(result.errors);
                }
            })
            .then(() => ({ name: name.value! }));
    }

    return {
        validate,
        errors,
        hasError: computed(() => formState.error != null),
        name: {
            value: name,
            props: nameInputProps,
        },
        submit,
        setError(error: ApiError<CreatePlannedCombatStageRequest>) {
            formState.error = error;
        },
    };
};

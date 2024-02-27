import { toTypedSchema } from "@vee-validate/yup";
import type { AxiosError } from "axios";
import { useForm } from "vee-validate";
import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
import type { CreatePlannedCombatStageRequest } from "~/utils/api/plannedCombat/stages/createPlannedCombatStageRequest";
import { yup } from "~/utils/types/HelperTypes";
import type { PlannedCombat } from "~/utils/types/models";

export const useCreatePlannedCombatStageForm = () => {
    const formState = reactive({
        error: null as ApiError<CreatePlannedCombatStageRequest> | null,
    });

    // Form Definition
    const { values, errors, defineField, validate, setFieldError } = useForm({
        validationSchema: toTypedSchema(
            yup.object({
                name: yup.string().required(),
            }),
        ),
    });

    const [name, nameInputProps] = defineField("name", {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("name") ?? state.errors[0],
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
            .then(() => ({ name: name.value! }))
    }

    return {
        validate,
        errors,
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

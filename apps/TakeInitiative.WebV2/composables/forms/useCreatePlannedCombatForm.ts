import type { AxiosError } from "axios";
import { useForm } from "vee-validate";
import type { CreatePlannedCombatRequest } from "../../utils/api/plannedCombat/createPlannedCombatRequest";
import { toTypedSchema } from "@vee-validate/zod";
import { z } from "zod";

export const useCreatePlannedCombatForm = () => {
    const formState = reactive({
        error: null as ApiError<CreatePlannedCombatRequest> | null,
    });

    // Form Definition
    const { values, errors, defineField, validate } = useForm({
        validationSchema: toTypedSchema(
            z
                .object({
                    combatName: z.string({
                        required_error: "Please provide a name for the combat.",
                    }),
                })
                .required()
        ),
    });

    const [combatName, combatNameInputProps] = defineField("combatName", {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorObjectFor("combatName") ?? state.errors[0],
        }),
    });

    // Form Submit
    const campaignStore = useCampaignStore();

    function submit(
        startCombatImmediately: boolean
    ): Promise<void | Omit<CreatePlannedCombatRequest, "campaignId">> {
        formState.error = null;
        return validate()
            .then((result) => {
                if (!result.valid) {
                    Promise.reject(result.errors);
                }
            })
            .then(() => ({
                combatName: combatName.value!,
                startCombatImmediately: startCombatImmediately,
            }));
    }

    return {
        validate,
        errors,
        combatName: {
            value: combatName,
            props: combatNameInputProps,
        },
        submit,
        setError(error: ApiError<CreatePlannedCombatRequest>) {
            formState.error = error;
        },
    };
};

import { toTypedSchema } from "@vee-validate/yup";
import type { AxiosError } from "axios";
import { useForm } from "vee-validate";
import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
import { yup } from "~/utils/types/HelperTypes";
import type { PlannedCombat } from "~/utils/types/models";

export const useCreatePlannedCombatForm = () => {
    const formState = reactive({
        error: null as ApiError<CreatePlannedCombatRequest> | null,
    });

    // Form Definition
    const { values, errors, defineField, validate } = useForm({
        validationSchema: toTypedSchema(
            yup.object({
                combatName: yup.string().required("Please provide a name for the combat."),
            }),
        ),
    });

    const [combatName, combatNameInputProps] = defineField("combatName", {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("combatName") ?? state.errors[0],
        }),
    });

    // Form Submit
    const campaignStore = useCampaignStore();
	function submit(): Promise<void | Omit<
        CreatePlannedCombatRequest,
        "campaignId"
    >> {
        formState.error = null;
        return validate()
            .then((result) => {
                if (!result.valid) {
                    Promise.reject(result.errors);
                }
            })
            .then(() => ({ combatName: combatName.value! }))
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

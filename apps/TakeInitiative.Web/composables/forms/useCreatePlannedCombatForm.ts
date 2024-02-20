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
                combatName: yup.string().required(),
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
    async function submit(): Promise<PlannedCombat> {
        formState.error = null;
        const validation = await validate();
        if (validation.valid == false) {
            Promise.reject(validation.errors);
        }

        return await campaignStore
            .createPlannedCombat({
                combatName: combatName.value!,
            })
            .catch(async (error) => {
                console.log(error);
                const parsedError =
                    await parseAsApiError<CreatePlannedCombatRequest>(
                        error as AxiosError,
                    );
                console.log(parsedError);

                formState.error = parsedError;
                throw error;
            });
    }

    return {
        validate,
        errors,
        combatName: {
            value: combatName,
            props: combatNameInputProps,
        },
        submit,
    };
};

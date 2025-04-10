<template>
    <AutoForm
        :schema="validator"
        :onSubmit="() => submit()"
        :form="form"
        :fieldConfig="{
            immidatelyOpen: {
                label: 'Open to players',
            },
        }"
        class="flex flex-col gap-2">
        <div class="flex gap-2 justify-end">
            <Button type="submit">
                <FontAwesomeIcon :icon="faPlusCircle" />
                {{ form.isSubmitting.value ? "Creating..." : "Create" }}
            </Button>
        </div>
    </AutoForm>
</template>

<script setup lang="ts">
    import { faPlusCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { toTypedSchema } from "@vee-validate/zod";
    import { isAxiosError } from "axios";
    import { useForm } from "vee-validate";
    import { z } from "zod";
    import {
        createPlannedCombatRequestValidator,
        type CreatePlannedCombatRequest,
    } from "~/utils/api/plannedCombat/createPlannedCombatRequest";

    const validator = z.object({
        combatName: z.string().nonempty(),
        immidatelyOpen: z.boolean(),
    });
    const form = useForm({
        validationSchema: toTypedSchema(validator),
        initialValues: {
            combatName: undefined,
            immidatelyOpen: false,
        },
    });

    const props = defineProps<{
        onCreateDraftCombat: (
            input: Omit<CreatePlannedCombatRequest, "campaignId">,
            startCombatImmediately: boolean
        ) => Promise<any>;
    }>();

    const submit = form.handleSubmit((req) => {
        return props
            .onCreateDraftCombat(
                { combatName: req.combatName },
                req.immidatelyOpen
            )
            .catch((err) => {
                if (isAxiosError(err)) {
                    form.setErrors({ combatName: err.message });
                }
            });
    });
</script>

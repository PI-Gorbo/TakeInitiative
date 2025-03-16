<template>
    <AutoForm :schema="schema" :onSubmit="onSubmit" v-slot="{ isSubmitting }">
        <ErrorPanel v-if="state.errorObject?.errors?.generalErrors">
            {{ state.errorObject?.errors?.generalErrors.at(0) }}
        </ErrorPanel>
        <Button type="submit" class="w-fit interactable shadow-solid-sm">
            <FontAwesomeIcon :icon="faPlusCircle" />
            {{ isSubmitting ? "Creating..." : "Create" }}
        </Button>
    </AutoForm>
</template>
<script setup lang="ts">
    import { faPlusCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { z } from "zod";
    import type {
        CreatePlayerCharacterRequest,
        PlayerCharacterDto,
    } from "~/utils/api/campaign/createPlayerCharacterRequest";
    const campaign = useCampaignStore();

    const schema = z.object({
        name: z.string().nonempty(),
    });

    const props = defineProps<{
        onSubmit: (request: PlayerCharacterDto) => Promise<unknown>;
    }>();

    const state = reactive({
        errorObject: null as null | ApiError<CreatePlayerCharacterRequest>,
    });

    async function onSubmit(form: z.infer<typeof schema>) {
        await props
            .onSubmit({
                name: form.name,
                health: {
                    "!": "None",
                },
                initiative: {
                    roll: "1d20",
                },
                armourClass: null,
            })
            .catch((err) => {
                state.errorObject = parseAsApiError(err);
            });
    }
</script>

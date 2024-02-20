<template>
    <main class="flex h-full flex-col items-center overflow-y-auto">
        <div class="grid w-full flex-1 grid-cols-8 gap-4 overflow-y-auto">
            <section class="col-span-2 flex flex-col gap-4 overflow-y-auto">
                <article
                    class="flex flex-1 flex-col rounded-xl border border-take-navy-medium px-2 py-1"
                >
                    <div class="flex-1">
                        <div class="w-full">Campaign Name</div>
                        <div
                            class="px-3 py-2 font-NovaCut text-lg text-take-yellow"
                        >
                            {{ campaign?.campaignName }}
                        </div>
                    </div>
                    <div class="flex-1">
                        <div class="w-full">Creation Date</div>
                        <div
                            class="px-3 py-2 font-NovaCut text-lg text-take-yellow"
                        >
                            {{
                                format(
                                    parseISO(campaign?.createdTimestamp ?? ""),
                                    "PPPP",
                                )
                            }}
                        </div>
                    </div>
                    <div class="flex-1">
                        <div class="w-full">Last Fight Date</div>
                        <div
                            class="px-3 py-2 font-NovaCut text-lg text-take-yellow"
                        >
                            ...
                        </div>
                    </div>
                </article>
                <section
                    class="flex flex-1 flex-col overflow-y-auto rounded-xl border border-take-navy-medium px-2 py-1"
                >
                    <label class="block w-full"> Players </label>
                    <div class="overflow-y-auto px-3 py-2">
                        <IndexPlayersDisplay
                            :campaignMemberDtos="campaignStore.memberDtos"
                        />
                    </div>
                </section>
            </section>
            <FormBase
                class="col-span-6 flex flex-col"
                :onSubmit="submitDetails"
                v-slot="{ submitting }"
            >
                <div class="flex w-full flex-1 flex-col overflow-y-auto">
                    <div class="w-full border-b border-take-navy-light">
                        Campaign Description
                    </div>
                    <div class="flex-1 px-3 py-2">
                        <textarea
                            class="h-full w-full rounded-lg bg-take-navy-light p-1"
                            :value="description"
                            @input="
                                (e) =>
                                    (description = (
                                        e.target as HTMLTextAreaElement
                                    ).value)
                            "
                        />
                    </div>
                </div>
                <div class="flex w-full flex-1 flex-col overflow-y-auto">
                    <div class="w-full border-b border-take-navy-light">
                        Resources
                    </div>
                    <div class="flex-1 px-3 py-2">
                        <textarea
                            :value="resources"
                            class="h-full w-full rounded-lg bg-take-navy-light p-1"
                            @input="
                                (e) =>
                                    (resources = (
                                        e.target as HTMLTextAreaElement
                                    ).value)
                            "
                        />
                    </div>
                </div>
                <div class="flex w-full justify-end">
                    <div>
                        <FormButton
                            type="submit"
                            class="h-3/4 w-3/4 text-sm"
                            icon="floppy-disk"
                            :loadingDisplay="{ showSpinner: true }"
                            :disabled="!hasChanges"
                            :isLoading="submitting"
                        />
                    </div>
                </div>
            </FormBase>
        </div>
    </main>
</template>
<script setup lang="ts">
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import { yup } from "~/utils/types/HelperTypes";
import { parseISO, format } from "date-fns";

const campaignStore = useCampaignStore();
const campaign = computed(() => campaignStore.state.campaign);
const formState = reactive({
    isSaving: false,
});

// Form Definition for updating campaign description / resources.
// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            description: yup.string(),
            resources: yup.string(),
        }),
    ),
});
const [description, descriptionInputProps] = defineField("description");
const [resources, resourcesInputProps] = defineField("resources");

onMounted(() => {
    description.value = campaign.value?.campaignDescription ?? "";
    resources.value = campaign.value?.campaignResources ?? "";
});

const hasChanges = computed(() => {
    return (
        campaignStore.state.campaign?.campaignDescription !=
            description.value ||
        campaignStore.state.campaign?.campaignResources != resources.value
    );
});

async function submitDetails(): Promise<void> {
    return await campaignStore.updateCampaignDetails({
        campaignDescription: description.value ?? "",
        campaignResources: resources.value ?? "",
    });
}
</script>

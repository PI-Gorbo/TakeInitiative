<template>
    <main class="flex h-full flex-col items-center overflow-y-auto">
        <div class="grid w-full flex-1 grid-cols-9 gap-4 overflow-y-auto">
            <section class="col-span-3 flex flex-col gap-4 overflow-y-auto">
                <article
                    class="flex flex-1 flex-col rounded-xl border border-take-navy-medium px-2 py-1"
                >
                    <div class="flex-1">
                        <div class="w-full font-NovaCut">Campaign Name</div>
                        <div class="px-3 py-2 text-lg text-take-yellow">
                            {{ campaign?.campaignName }}
                        </div>
                    </div>
                    <div class="flex-1">
                        <div class="w-full font-NovaCut">Creation Date</div>
                        <div class="px-3 py-2 text-lg text-take-yellow">
                            {{
                                format(parseISO(campaign?.createdTimestamp ?? ""), "PPPP")
                            }}
                        </div>
                    </div>
                    <div class="flex-1">
                        <div class="w-full font-NovaCut">Last Fight Date</div>
                        <div class="px-3 py-2 text-lg text-take-yellow">...</div>
                    </div>
                </article>
                <section
                    class="flex flex-1 flex-col overflow-y-auto rounded-xl border border-take-navy-medium px-2 py-1"
                >
                    <label class="block w-full font-NovaCut"> Players </label>
                    <div class="overflow-y-auto px-3 py-2">
                        <IndexPlayersDisplay
                            :campaignMemberDtos="campaignStore.memberDtos"
                        />
                    </div>
                </section>
            </section>
            <section class="col-span-6 flex h-full flex-col gap-4">
                <FormBase
                    class="col-span-6 flex flex-1 flex-col rounded-xl border border-take-navy-medium px-2 py-1"
                    :onSubmit="submitDetails"
                    v-slot="{ submitting }"
                >
                    <div class="flex w-full font-NovaCut">
                        <label>Description</label>
                    </div>
                    <div class="flex w-full flex-1 flex-col overflow-y-auto px-3 py-2">
                        <div
                            class="flex flex-1 gap-2 flex-row rounded-xl bg-take-navy-medium p-1"
                        >
                            <textarea
                                :value="description"
                                class="h-full w-full rounded-xl bg-take-navy-medium p-1 ring-0"
                                @input="
                                    (e) =>
                                        (description = (
                                            e.target as HTMLTextAreaElement
                                        ).value)
                                "
                            />
                            <div>
                                <FormButton
                                    type="submit"
                                    class="text-sm disabled:bg-take-navy-medium"
                                    icon="floppy-disk"
                                    :loadingDisplay="{ showSpinner: true }"
                                    :disabled="!descriptionHasChanges"
                                    :isLoading="submitting"
                                />
                            </div>
                        </div>
                    </div>
                </FormBase>
                <FormBase
                    class="col-span-6 flex flex-1 flex-col rounded-xl border border-take-navy-medium px-2 py-1"
                    :onSubmit="submitDetails"
                    v-slot="{ submitting }"
                >
                    <div class="flex w-full font-NovaCut">
                        <label>Resources</label>
                    </div>
                    <div class="flex w-full flex-1 flex-col overflow-y-auto px-3 py-2">
                        <div
                            class="flex flex-1 gap-2 flex-row rounded-xl bg-take-navy-medium p-1"
                        >
                            <textarea
                                :value="resources"
                                class="h-full w-full rounded-xl bg-take-navy-medium p-1 ring-0"
                                @input="
                                    (e) =>
                                        (resources = (
                                            e.target as HTMLTextAreaElement
                                        ).value)
                                "
                            />
                            <div>
                                <FormButton
                                    type="submit"
                                    class="text-sm disabled:bg-take-navy-medium"
                                    icon="floppy-disk"
                                    :loadingDisplay="{ showSpinner: true }"
                                    :disabled="!resourcesHasChanges"
                                    :isLoading="submitting"
                                />
                            </div>
                        </div>
                    </div>
                </FormBase>
            </section>
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
        })
    ),
});
const [description, descriptionInputProps] = defineField("description");
const [resources, resourcesInputProps] = defineField("resources");

onMounted(() => {
    description.value = campaign.value?.campaignDescription ?? "";
    resources.value = campaign.value?.campaignResources ?? "";
});

const descriptionHasChanges = computed(() => {
    return campaignStore.state.campaign?.campaignDescription != description.value;
});

const resourcesHasChanges = computed(() => {
    return campaignStore.state.campaign?.campaignResources != resources.value;
});

async function submitDetails(): Promise<void> {
    return await campaignStore.updateCampaignDetails({
        campaignDescription: description.value ?? "",
        campaignResources: resources.value ?? "",
    });
}
</script>

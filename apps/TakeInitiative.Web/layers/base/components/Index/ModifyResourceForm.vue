<template>
    <FormBase
        class="flex flex-col gap-4"
        :onSubmit="submit"
        v-slot="{ submitting }"
    >
        <FormInput
            label="Name"
            placeholder="Link name"
            v-model:value="name"
            v-bind="nameProps"
        />
        <FormInput
            label="Link"
            placeholder="URL"
            v-model:value="link"
            v-bind="linkProps"
        />
        <div>
            <label class="text-sm">Visibility</label>
            <Dropdown
                :items="[...resourceVisibilityKeys]"
                :keyFunc="(i) => i"
                :displayFunc="(i) => i"
                :selectedItem="ResourceVisibilityOptions[visibility ?? 0]"
                colour="take-navy-light"
                @update:selectedItem="
                    //@ts-ignore
                    (item) => (visibility = ResourceVisibilityOptions[item])
                "
            />
        </div>
        <div
            v-if="props.resource == null"
            class="flex w-full items-center justify-end"
        >
            <FormButton
                type="submit"
                label="Add"
                icon="plus"
                buttonColour="take-purple-light"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Adding...',
                }"
                :isLoading="submitting"
            />
        </div>
        <div v-else class="flex w-full items-center justify-between">
            <FormButton
                type="submit"
                label="Edit"
                icon="pencil"
                buttonColour="take-purple-light"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Editing...',
                }"
                :isLoading="submitting && submitting.submitterName == 'Edit'"
            />
            <FormButton
                type="submit"
                label="Delete"
                icon="trash"
                buttonColour="take-red"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Deleting...',
                }"
                :isLoading="submitting && submitting.submitterName == 'Delete'"
            />
        </div>
    </FormBase>
</template>
<script setup lang="ts">
import {
    ResourceVisibilityOptions,
    campaignMemberResourceValidator,
    resourceVisibilityKeys,
    type CampaignMemberResource,
} from "base/utils/types/models";
import { useForm } from "vee-validate";
import type { SubmittingState } from "../Form/Base.vue";
import { toTypedSchema } from "@vee-validate/zod";

const props = defineProps<{
    addResource: (resource: CampaignMemberResource) => Promise<unknown>;
    resource: CampaignMemberResource | null;
    deleteResource: () => Promise<unknown>;
    editResource: (resource: CampaignMemberResource) => Promise<unknown>;
}>();

// Form
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(campaignMemberResourceValidator),
    initialValues: {
        name: props.resource?.name ?? "",
        link: props.resource?.link ?? "",
        visibility: props.resource?.visibility ?? 1,
    },
});
const [name, nameProps] = defineField("name", {
    props: (formState) => ({ errorMessage: formState.errors[0] }),
});
const [link, linkProps] = defineField("link", {
    props: (formState) => ({ errorMessage: formState.errors[0] }),
});
const [visibility, visibilityProps] = defineField("visibility", {
    props: (formState) => ({ errorMessage: formState.errors[0] }),
});

async function submit(submittingState: SubmittingState) {
    if (submittingState.submitterName == "Delete") {
        return await props.deleteResource();
    }

    const result = await validate();
    if (!result.valid) {
        return;
    }

    if (submittingState.submitterName == "Edit") {
        return await props.editResource({
            link: link.value!,
            name: name.value!,
            visibility: visibility.value!,
        });
    } else {
        return await props.addResource({
            link: link.value!,
            name: name.value!,
            visibility: visibility.value!,
        });
    }
}
</script>

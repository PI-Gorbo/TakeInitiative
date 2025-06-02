<template>
    <form @submit.prevent="submit" class="flex flex-col gap-4">
        <FormFieldWrapper label="Name" :error="form.errors.value.name">
            <Input v-model:modelValue="name" placeholder="Resource Name" />
        </FormFieldWrapper>

        <FormFieldWrapper label="Link" :error="form.errors.value.link">
            <Input v-model:modelValue="link" placeholder="URL" />
        </FormFieldWrapper>

        <FormFieldWrapper
            label="Visibility"
            :error="form.errors.value.visibility">
            <Select v-model:modelValue="visibility">
                <SelectTrigger>
                    <SelectValue placeholder="Select a mode..." />
                </SelectTrigger>
                <SelectContent>
                    <SelectGroup>
                        <SelectLabel>Visibility</SelectLabel>
                        <SelectItem :value="ResourceVisibilityOptions.DMsOnly" v-if="!props.isDm">
                            {{
                                resourceVisibilityOptionNameMap[
                                    ResourceVisibilityOptions.DMsOnly
                                ]
                            }}
                        </SelectItem>
                        <SelectItem :value="ResourceVisibilityOptions.Public">
                            {{
                                resourceVisibilityOptionNameMap[
                                    ResourceVisibilityOptions.Public
                                ]
                            }}
                        </SelectItem>
                        <SelectItem :value="ResourceVisibilityOptions.Private">
                            {{
                                resourceVisibilityOptionNameMap[
                                    ResourceVisibilityOptions.Private
                                ]
                            }}
                        </SelectItem>
                    </SelectGroup>
                </SelectContent>
            </Select>
        </FormFieldWrapper>

        <div v-if="props.type === 'Add'">
            <Button
                type="submit"
                :disabled="!form.meta.value.valid || !form.meta.value.dirty">
                <FontAwesomeIcon :icon="faPlusCircle" />
                {{ form.isSubmitting.value ? "Adding..." : "Add Resource" }}
            </Button>
        </div>
        <div v-else class="flex justify-between">
            <Button
                type="button"
                size="icon"
                variant="destructive"
                @click="props.deleteResource">
                <FontAwesomeIcon :icon="faTrashCan" />
            </Button>
            <Button type="submit">
                <FontAwesomeIcon
                    :icon="faPen"
                    :disabled="
                        !form.meta.value.valid || !form.meta.value.dirty
                    ">
                    {{
                        form.isSubmitting.value
                            ? "Updating..."
                            : "Update Resource"
                    }}
                </FontAwesomeIcon>
            </Button>
        </div>
    </form>
</template>
<script setup lang="ts">
    import {
        faPen,
        faPlusCircle,
        faSpinner,
        faTrashCan,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { toTypedSchema } from "@vee-validate/zod";
    import { useForm } from "vee-validate";
    import {
        campaignMemberResourceValidator,
        resourceVisibilityOptionNameMap,
        ResourceVisibilityOptions,
        type CampaignMemberResource,
    } from "~/utils/types/models";

    const props = defineProps<
        (| {
            type: "Add";
            addResource: (
                resource: CampaignMemberResource
            ) => Promise<unknown>;
        }
            | {
                type: "Edit";
                resource: CampaignMemberResource | null;
                deleteResource: () => Promise<unknown>;
                editResource: (
                    resource: CampaignMemberResource
                ) => Promise<unknown>;
        }) & {
        isDm: boolean}
    >();

    // Form
    const form = useForm({
        validationSchema: toTypedSchema(campaignMemberResourceValidator),
        initialValues:
            props.type === "Add"
                ? {
                      name: "",
                      link: "",
                      visibility: ResourceVisibilityOptions.DMsOnly,
                  }
                : {
                      name: props.resource?.name,
                      link: props.resource?.link,
                      visibility: props.resource?.visibility,
                  },
    });
    const [name] = form.defineField("name");
    const [link] = form.defineField("link");
    const [visibility] = form.defineField("visibility");
    const submit = form.handleSubmit(async (newResource) => {
        if (props.type === "Add") {
            return await props.addResource(newResource);
        }

        return props.editResource(newResource);
    });
</script>

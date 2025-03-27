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
                        <SelectItem :value="ResourceVisibilityOptions.DMsOnly">
                            You and DM
                        </SelectItem>
                        <SelectItem :value="ResourceVisibilityOptions.Public">
                            Everyone
                        </SelectItem>
                        <SelectItem :value="ResourceVisibilityOptions.Private">
                            Private
                        </SelectItem>
                    </SelectGroup>
                </SelectContent>
            </Select>
        </FormFieldWrapper>

        <div v-if="'addResource' in props">
            <Button type="submit">
                <FontAwesomeIcon :icon="faPlusCircle" />
                {{ form.isSubmitting.value ? "Adding..." : "Add Resource" }}
            </Button>
        </div>
        <div v-else>
            <Button type="button" size="icon" variant="destructive">
                <FontAwesomeIcon :icon="faTrashCan" />
            </Button>
            <Button type="submit">
                <FontAwesomeIcon :icon="faPen" />
                {{
                    form.isSubmitting.value ? "Updating..." : "Update Resource"
                }}
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
        ResourceVisibilityOptions,
        type CampaignMemberResource,
    } from "~/utils/types/models";

    const props = defineProps<
        | {
              addResource: (
                  resource: CampaignMemberResource
              ) => Promise<unknown>;
          }
        | {
              resource: CampaignMemberResource | null;
              deleteResource: () => Promise<unknown>;
              editResource: (
                  resource: CampaignMemberResource
              ) => Promise<unknown>;
          }
    >();

    // Form
    const form = useForm({
        validationSchema: toTypedSchema(campaignMemberResourceValidator),
        initialValues:
            "addResource" in props
                ? {
                      name: "",
                      link: "",
                      visibility: ResourceVisibilityOptions.DMsOnly,
                  }
                : { ...props.resource },
    });
    const [name] = form.defineField("name");
    const [link] = form.defineField("link");
    const [visibility] = form.defineField("visibility");
    const submit = form.handleSubmit(async (newResource) => {
        if ("addResource" in props) {
            return await props.addResource(newResource);
        }

        return props.editResource(newResource);
    });
</script>

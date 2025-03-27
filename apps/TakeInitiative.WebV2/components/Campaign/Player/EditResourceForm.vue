<template></template>
<script setup lang="ts">
    // const props = defineProps<{
    //     resource: CampaignMemberResource | null;
    //     deleteResource: () => Promise<unknown>;
    //     editResource: (resource: CampaignMemberResource) => Promise<unknown>;
    // }>();

    // // Form
    // const { values, errors, defineField, validate } = useForm({
    //     validationSchema: toTypedSchema(campaignMemberResourceValidator),
    //     initialValues: {
    //         name: props.resource?.name ?? "",
    //         link: props.resource?.link ?? "",
    //         visibility: props.resource?.visibility ?? 1,
    //     },
    // });
    // const [name, nameProps] = defineField("name", {
    //     props: (formState) => ({ errorMessage: formState.errors[0] }),
    // });
    // const [link, linkProps] = defineField("link", {
    //     props: (formState) => ({ errorMessage: formState.errors[0] }),
    // });
    // const [visibility, visibilityProps] = defineField("visibility", {
    //     props: (formState) => ({ errorMessage: formState.errors[0] }),
    // });

    // async function submit(submittingState: SubmittingState) {
    //     if (submittingState.submitterName == "Delete") {
    //         return await props.deleteResource();
    //     }

    //     const result = await validate();
    //     if (!result.valid) {
    //         return;
    //     }

    //     if (submittingState.submitterName == "Edit") {
    //         return await props.editResource({
    //             link: link.value!,
    //             name: name.value!,
    //             visibility: visibility.value!,
    //         });
    //     } else {
    //         return await props.addResource({
    //             link: link.value!,
    //             name: name.value!,
    //             visibility: visibility.value!,
    //         });
    //     }
    // }
</script>

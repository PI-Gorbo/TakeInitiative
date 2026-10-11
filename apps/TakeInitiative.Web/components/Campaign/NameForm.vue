<template>
    <!-- Renaming the campaign (SAM-22). Settings management is a DM's (design §1). -->
    <Card class="flex flex-col gap-3 p-3">
        <div class="flex flex-col gap-1">
            <h3
                :id="`${id}-heading`"
                class="font-medium">
                Name
            </h3>
            <p class="text-sm text-muted-foreground">
                What this campaign is called, for everyone in it.
            </p>
        </div>
        <form
            class="flex flex-col gap-3"
            :aria-labelledby="`${id}-heading`"
            @submit.prevent="submit">
            <div class="flex flex-col gap-1.5">
                <Label
                    :for="`${id}-name`"
                    class="sr-only">
                    Campaign name
                </Label>
                <Input
                    :id="`${id}-name`"
                    v-model="name"
                    :maxlength="NAME_MAX_LENGTH"
                    autocomplete="off"
                    enterkeyhint="done"
                    class="h-11 text-base md:h-9 md:text-sm"
                    :aria-invalid="!!error || undefined"
                    :aria-describedby="error ? `${id}-error` : undefined" />
                <p
                    v-if="error"
                    :id="`${id}-error`"
                    class="text-sm text-destructive-tint"
                    role="alert">
                    {{ error }}
                </p>
            </div>
            <div class="flex justify-end">
                <Button
                    type="submit"
                    class="h-11 md:h-9"
                    :disabled="!changed || rename.isPending.value">
                    Save
                </Button>
            </div>
        </form>
    </Card>
</template>

<script setup lang="ts">
    import { toast } from "vue-sonner";
    import type { Campaign } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import { NAME_MAX_LENGTH } from "~/utils/campaign";
    import { putCampaignNameMutation } from "~/utils/queries/campaign";

    const props = defineProps<{ campaign: Campaign }>();

    const id = useId();
    const name = ref(props.campaign.name);
    const error = ref<string | null>(null);

    // A rename from another member arrives over the hub; don't fight the draft being typed.
    watch(
        () => props.campaign.name,
        (renamed) => {
            if (!rename.isPending.value) name.value = renamed;
        }
    );
    watch(name, () => (error.value = null));

    const changed = computed(() => {
        const trimmed = name.value.trim();
        return trimmed.length > 0 && trimmed !== props.campaign.name;
    });

    const rename = putCampaignNameMutation();
    async function submit() {
        if (!changed.value) return;
        try {
            await rename.mutateAsync({ campaignId: props.campaign.id, name: name.value.trim() });
            toast.success("Campaign renamed");
        } catch (err) {
            error.value = apiErrorMessage(err, "Could not rename the campaign.");
        }
    }
</script>

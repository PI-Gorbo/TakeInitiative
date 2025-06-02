<template>
    <FormFieldWrapper label="Armor Class" :error="props.error">
        <div class="flex gap-2">
            <Input
                type="number"
                placeholder="Optional"
                :modelValue="ac ?? undefined"
                @update:modelValue="
                    (v) => {
                        if (v === '') {
                            ac = null;
                        } else {
                            //@ts-ignore
                            ac = Number(v);
                        }
                    }
                " />
            <Button
                size="icon"
                type="button"
                variant="outline"
                @click="() => (ac = null)">
                <FontAwesomeIcon :icon="faArrowRotateLeft" />
            </Button>
        </div>
    </FormFieldWrapper>
</template>

<script setup lang="ts">
    import { faArrowRotateLeft } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const ac = defineModel<number | null | undefined>("ac", {
        required: true,
    });
    const props = defineProps<{
        error: string | undefined;
    }>();
</script>

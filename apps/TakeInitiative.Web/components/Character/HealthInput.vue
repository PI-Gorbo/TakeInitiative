<template>
    <section>
        <label class="text-sm text-white">Health (Optional)</label>
        <div :class="['flex items-center gap-1']">
            <div class="flex items-center pt-5">
                <input
                    type="checkbox"
                    class="h-8 w-8"
                    :checked="props.hasHealth"
                    @input="onHasHealthToggled"
                />
            </div>
            <FormInput
                label="Current"
                :value="props.currentHealth"
                type="number"
                class="flex-1"
                @input="
                    (e: Event) =>
                        onInputCurrentHealth(
                            (e.target as HTMLInputElement).value,
                        )
                "
                :disabled="!props.hasHealth"
            />
            <span class="pt-4">/</span>
            <FormInput
                label="Max"
                :value="props.maxHealth"
                type="number"
                class="flex-1"
                @input="
                    (e: Event) =>
                        onInputMaxHealth((e.target as HTMLInputElement).value)
                "
                :disabled="!props.hasHealth"
            />
        </div>
        <label class="text-red-500" v-if="props.error">{{ props.error }}</label>
    </section>
</template>
<script setup lang="ts">
const props = defineProps<{
    hasHealth: boolean | undefined;
    currentHealth: number | undefined | null;
    maxHealth: number | undefined | null;
    error: string | undefined | null;
}>();

const emit = defineEmits<{
    (e: "update:hasHealth", value: boolean): void;
    (e: "update:currentHealth", value: number | null): void;
    (e: "update:maxHealth", value: number | null): void;
}>();

function onHasHealthToggled() {
    if (props.hasHealth) {
        emit("update:hasHealth", false);
    } else {
        emit("update:hasHealth", true);
    }
}

function onInputMaxHealth(value: string | undefined | null) {
    try {
        emit("update:maxHealth", parseInt(value ?? "0"));
    } catch {
    } finally {
        if (!props.hasHealth) {
            emit("update:hasHealth", true);
        }
    }
}

function onInputCurrentHealth(value: string | undefined | null) {
    try {
        emit("update:currentHealth", parseInt(value ?? "0"));
    } catch {
    } finally {
        if (!props.hasHealth) {
            emit("update:hasHealth", true);
        }
    }
}
</script>

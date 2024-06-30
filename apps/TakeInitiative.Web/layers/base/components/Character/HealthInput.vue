<template>
    <section>
        <label class="text-sm text-white">Health (Optional)</label>
        <div :class="['flex items-center gap-2']">
            <section
                class="flex items-center gap-2 rounded-md bg-take-navy-light"
            >
                <div class="flex-1">
                    <input
                        class="w-full rounded-md bg-take-navy-light p-2"
                        placeholder="Current"
                        :value="props.currentHealth"
                        @blur="
                            (e: Event) =>
                                onInputCurrentHealth(
                                    (e.target as HTMLInputElement).value,
                                )
                        "
                    />
                </div>
                <span>/</span>
                <div class="flex-1">
                    <input
                        class="w-full rounded-md bg-take-navy-light p-2"
                        placeholder="Max"
                        :value="props.maxHealth"
                        @blur="
                            (e: Event) =>
                                onInputMaxHealth(
                                    (e.target as HTMLInputElement).value,
                                )
                        "
                    />
                </div>
            </section>
            <div>
                <FormIconButton
                    class="p-3"
                    icon="arrow-rotate-left"
                    buttonColour="take-charcoal"
                    @clicked="reset"
                />
            </div>
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

function reset() {
    emit("update:hasHealth", false);
    emit("update:currentHealth", null);
    emit("update:maxHealth", null);
}

function onInputMaxHealth(value: string | undefined | null) {
    try {
        if (!value) {
            emit("update:maxHealth", null);
        } else {
            emit("update:maxHealth", parseInt(value ?? "0"));
        }
    } catch {
    } finally {
        if (!props.hasHealth) {
            emit("update:hasHealth", true);
        }
    }
}

function onInputCurrentHealth(value: string | undefined | null) {
    try {
        if (!value) {
            emit("update:currentHealth", null);
        } else {
            emit("update:currentHealth", parseInt(value ?? "0"));
        }
    } catch {
    } finally {
        if (!props.hasHealth) {
            emit("update:hasHealth", true);
        }
    }
}
</script>

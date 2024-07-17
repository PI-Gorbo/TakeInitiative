<template>
    <section>
        <header class="flex gap-2">
            <label class="text-sm text-white">Health (Optional)</label>
            <TooltipButton
                icon="circle-question"
                tooltip="If you don't provide health, it won't be displayed to others. Click the arrow button to reset. The health fields support basic arithmetic including grouping with brackets (), add (+), subtract (-), multiply (*) and divide (/)."
            />
        </header>
        <div :class="['flex w-full items-center gap-2']">
            <section
                class="flex flex-1 items-center gap-2 rounded-md bg-take-navy-light"
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
import { Parser } from "expr-eval";
const healthExpressionParser = new Parser({
    operators: {
        // Only enable add, subtract, multiple and divide
        add: true,
        subtract: true,
        multiply: true,
        divide: true,
        concatenate: false,
        conditional: false,
        factorial: false,
        power: false,
        remainder: false,

        // Disable and, or, not, <, ==, !=, etc.
        logical: false,
        comparison: false,

        // Disable 'in' and = operators
        in: false,
        assignment: false,
    },
});
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
    debugger;
    try {
        if (value == null) {
            emit("update:maxHealth", null);
        } else {
            emit("update:maxHealth", healthExpressionParser.evaluate(value));
        }
    } catch {
        emit("update:maxHealth", null);
    } finally {
        if (!props.hasHealth) {
            emit("update:hasHealth", true);
        }
    }
}

function onInputCurrentHealth(value: string | undefined | null) {
    try {
        if (value == null) {
            emit("update:currentHealth", null);
        } else {
            emit(
                "update:currentHealth",
                healthExpressionParser.evaluate(value),
            );
        }
    } catch {
        emit("update:currentHealth", null);
    } finally {
        if (!props.hasHealth) {
            emit("update:hasHealth", true);
        }
    }
}
</script>

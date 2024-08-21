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
                        v-model="currentHealth"
                    />
                </div>
                <span class="cursor-default">/</span>
                <div class="flex-1">
                    <input
                        tabindex="0"
                        class="w-full rounded-md bg-take-navy-light p-2"
                        placeholder="Max"
                        v-model="maxHealth"
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
        {{ state }}
        <!-- <label class="text-red-500" v-if="errors"> {{ errors }}</label> -->
    </section>
</template>
<script setup lang="ts">
import { toTypedSchema } from "@vee-validate/zod";
import type { FormContext } from "base/composables/forms/useFormContext";
import {
    characterHealthValidator,
    type CharacterHealth,
} from "base/utils/types/models";
import { Parser } from "expr-eval";
import { useForm } from "vee-validate";
import { boolean, z } from "zod";
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
}>();

const state = reactive<{
    hasHealth: boolean | undefined;
    currentHealth: string | undefined | null;
    maxHealth: string | undefined | null;
}>({
    hasHealth: props.hasHealth,
    currentHealth: props.currentHealth?.toString() ?? null,
    maxHealth: props.maxHealth?.toString() ?? null,
});

// function onSubmitMaxHealth() {
//     try {
//         if (state.maxHealth == null) {
//             emit("update:maxHealth", null);
//         } else {
//             emit(
//                 "update:maxHealth",
//                 healthExpressionParser.evaluate(state.maxHealth),
//             );
//         }
//     } catch {
//         emit("update:maxHealth", null);
//     } finally {
//         if (!props.hasHealth) {
//             emit("update:hasHealth", true);
//         }
//     }
// }

// function onInputMaxHealth(value: string | undefined | null) {
//     state.maxHealth = value;
// }

// function onSubmitCurrentHealth() {
//     try {
//         if (state.currentHealth == null) {
//             emit("update:currentHealth", null);
//         } else {
//             emit(
//                 "update:currentHealth",
//                 healthExpressionParser.evaluate(state.currentHealth),
//             );
//         }
//     } catch {
//         emit("update:currentHealth", null);
//     } finally {
//         if (!props.hasHealth) {
//             emit("update:hasHealth", true);
//         }
//     }
// }

// function onInputCurrentHealth(value: string | undefined | null) {
//     state.currentHealth = value;
// }

const tryParseNumber = (num: string | null): string | number | null => {
    if (num == null) return null;

    try {
        return healthExpressionParser.evaluate(num);
    } catch (e) {
        return JSON.stringify(e);
    }
};

const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        z
            .object({
                currentHealth: z.string().nullable(),
                maxHealth: z.string().nullable(),
            })
            .transform((healthValues, ctx) => {
                const parsedCurrentHealth = tryParseNumber(
                    healthValues.currentHealth,
                );

                return {
                    hasHealth:
                        healthValues.currentHealth == null &&
                        healthValues.maxHealth == null,
                };
            }),
    ),
});
const [currentHealth, currentHealthAttrs] = defineField("currentHealth");
const [maxHealth, maxHealthAttrs] = defineField("maxHealth");

defineExpose({
    getHealth: () => {
        if (state.currentHealth == null && state.maxHealth == null) {
            const model: CharacterHealth = {
                hasHealth: false,
                currentHealth: null,
                maxHealth: null,
            };

            return model;
        }

        // const parsedMaxHealth = tryParseNumber(state.maxHealth);
    },
});
</script>

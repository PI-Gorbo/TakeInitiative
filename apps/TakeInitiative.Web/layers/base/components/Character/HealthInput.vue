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
                        v-model="state.currentHealth"
                    />
                </div>
                <span class="cursor-default">/</span>
                <div class="flex-1">
                    <input
                        tabindex="0"
                        class="w-full rounded-md bg-take-navy-light p-2"
                        placeholder="Max"
                        v-model="state.maxHealth"
                    />
                </div>
            </section>
            <div>
                <FormIconButton
                    class="p-3"
                    icon="arrow-rotate-left"
                    buttonColour="take-purple-dark"
                    @clicked="reset"
                />
            </div>
        </div>
        <label class="text-red-500" v-if="formState.error">
            {{ formState.error }}</label
        >
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
    currentHealth: string | undefined | null;
    maxHealth: string | undefined | null;
}>({
    currentHealth: null,
    maxHealth: null,
});

watch(
    () => [{ ...props }],
    () => {
        if (props.hasHealth == true) {
            state.currentHealth = props.currentHealth?.toString() ?? null;
            state.maxHealth = props.maxHealth?.toString() ?? null;
        } else {
            state.currentHealth = null;
            state.maxHealth = null;
        }
    },
);

function reset() {
    state.currentHealth = null;
    state.maxHealth = null;
}

const tryParseNumber = (
    num: string | null,
):
    | { isSuccess: true; value: number | null }
    | { isSuccess: false; error: string } => {
    if (num == null) return { isSuccess: true, value: null };

    try {
        return { isSuccess: true, value: healthExpressionParser.evaluate(num) };
    } catch (e) {
        return {
            isSuccess: false,
            error: `failed to evaluate expression '${num}'`,
        };
    }
};

const schema = z
    .object({
        currentHealth: z.string().nullable(),
        maxHealth: z.string().nullable(),
    })
    .transform((healthValues, ctx) => {
        const parsedCurrentHealth = tryParseNumber(healthValues.currentHealth);
        if (!parsedCurrentHealth.isSuccess) {
            ctx.addIssue({
                code: z.ZodIssueCode.custom,
                message: parsedCurrentHealth.error + " for current health",
            });
            return z.NEVER;
        }

        const parsedMaxHealth = tryParseNumber(healthValues.maxHealth);
        if (!parsedMaxHealth.isSuccess) {
            ctx.addIssue({
                code: z.ZodIssueCode.custom,
                message: parsedMaxHealth.error + " for max health",
            });
            return z.NEVER;
        }

        return {
            hasHealth:
                healthValues.currentHealth != null &&
                healthValues.maxHealth != null,
            currentHealth: parsedCurrentHealth.value,
            maxHealth: parsedMaxHealth.value,
        };
    });

const formState = reactive<{ error: string | null }>({
    error: null,
});
defineExpose({
    getHealth: (): CharacterHealth | false => {
        if (state.currentHealth == null && state.maxHealth == null) {
            const model: CharacterHealth = {
                hasHealth: false,
                currentHealth: null,
                maxHealth: null,
            };

            return model;
        }

        const model = {
            ...state,
        };
        const paredModel = schema.safeParse(model);
        if (paredModel.error) {
            formState.error = paredModel.error.issues[0].message;
            return false;
        }

        return paredModel.data;
    },
});
</script>

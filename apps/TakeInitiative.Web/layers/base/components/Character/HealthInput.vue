<template>
    <section>
        <header class="flex gap-2">
            <label class="text-sm text-white">Health</label>
            <TooltipButton
                icon="circle-question"
                tooltip="If you don't provide health, it won't be displayed to others. Click the arrow button to reset. The health fields support basic arithmetic including grouping with brackets (), add (+), subtract (-), multiply (*) and divide (/)."
            />
        </header>
        <div :class="['flex w-full items-center gap-2']">
            <select
                class="rounded bg-take-navy-light p-2"
                v-model="state.healthStrategy"
            >
                <option class="font-Overpass">None</option>
                <option class="font-Overpass">Fixed</option>
                <option class="font-Overpass">Roll</option>
            </select>
            <section
                class="flex flex-1 items-center gap-2 rounded-md bg-take-navy-light"
                v-if="state.healthStrategy != 'Roll'"
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
            <section class="flex w-full items-center" v-else>
                <FormInput
                    class="flex-1"
                    v-model:value="state.roll"
                    placeholder="10d20 + 40"
                />
            </section>
            <div>
                <FormIconButton
                    class="p-3"
                    icon="arrow-rotate-left"
                    buttonColour="take-purple"
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
import { type UnevaluatedCharacterHealth } from "base/utils/types/models";
import { Parser } from "expr-eval";
import { z } from "zod";
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
    health: UnevaluatedCharacterHealth;
}>();

const state = reactive<{
    healthStrategy: "None" | "Fixed" | "Roll";
    currentHealth: string | undefined | null;
    maxHealth: string | undefined | null;
    roll: string | null;
}>({
    healthStrategy: "None",
    currentHealth: null,
    maxHealth: null,
    roll: null,
});

watch(
    () => ({ ...state }),
    (after, before) => {
        if (before.healthStrategy != "None" && after.healthStrategy == "None") {
            state.currentHealth = null;
            state.maxHealth = null;
        }

        if (
            (before.currentHealth == null && after.currentHealth != null) ||
            (before.maxHealth == null && after.maxHealth != null)
        ) {
            state.healthStrategy = "Fixed";
        }

        if (after.currentHealth == null && after.maxHealth == null) {
            state.healthStrategy = "None";
        }
    },
);

watch(
    () => ({ ...props }),
    () => {
        if (props.health["!"] == "None") {
            state.healthStrategy = "None";
            state.currentHealth = null;
            state.maxHealth = null;
            state.roll = null;
        }

        if (props.health["!"] == "Fixed") {
            state.healthStrategy = "Fixed";
            state.currentHealth = props.health.currentHealth.toString();
            state.maxHealth = props.health.maxHealth.toString();
            state.roll = null;
        }

        if (props.health["!"] == "Roll") {
            state.healthStrategy = "Roll";
            state.currentHealth = null;
            state.maxHealth = null;
            state.roll = props.health.rollString;
        }
    },
);

function reset() {
    state.healthStrategy = "None";
    state.currentHealth = null;
    state.maxHealth = null;
    state.roll = null;
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
    getHealth: (): UnevaluatedCharacterHealth | false => {
        if (state.currentHealth == null && state.maxHealth == null) {
            return {
                "!": "None",
            } satisfies UnevaluatedCharacterHealth;
        }

        const model = {
            ...state,
        };
        const paredModel = schema.safeParse(model);
        if (paredModel.error) {
            formState.error = paredModel.error.issues[0].message;
            return false;
        }

        return {
            "!": "Fixed",
            currentHealth: paredModel.data.currentHealth!,
            maxHealth: paredModel.data.maxHealth!,
        };
    },
});
</script>

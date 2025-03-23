<template>
    <fieldset class="border rounded-md p-2 flex flex-col gap-2">
        <legend class="mx-2 p-2 flex items-center gap-2">
            <label class="text-sm text-white">Health</label>
            <TooltipProvider>
                <Tooltip>
                    <TooltipTrigger>
                        <FontAwesomeIcon :icon="faCircleQuestion" />
                    </TooltipTrigger>
                    <TooltipContent class="text-wrap">
                        If you don't provide health, it won't be displayed to
                        others. Click the arrow button to reset. The health
                        fields support basic arithmetic including grouping with
                        brackets (), add (+), subtract (-), multiply (*) and
                        divide (/).
                    </TooltipContent>
                </Tooltip>
            </TooltipProvider>
            <Select v-model:modelValue="state.healthStrategy">
                <SelectTrigger>
                    <SelectValue placeholder="Select a mode..." />
                </SelectTrigger>
                <SelectContent>
                    <SelectGroup>
                        <SelectLabel>Mode</SelectLabel>
                        <SelectItem value="None"> None </SelectItem>
                        <SelectItem value="Fixed"> Fixed </SelectItem>
                        <SelectItem value="Roll"> Roll </SelectItem>
                    </SelectGroup>
                </SelectContent>
            </Select>
        </legend>
        <div :class="['flex w-full justify-center items-center gap-2']">
            <section
                v-if="state.healthStrategy != 'Roll'"
                class="flex flex-1 items-center gap-2 rounded-md bg-take-navy-light justify-center border"
                :class="{
                    'brightness-75': state.healthStrategy == 'None',
                }">
                <div class="flex-1">
                    <Input
                        placeholder="Current"
                        :modelValue="state.currentHealth ?? undefined"
                        @update:modelValue="
                            (h) => (state.currentHealth = String(h))
                        "
                        class="border-none"
                        @blur="onBlurOfInput" />
                </div>
                <span class="cursor-default">/</span>
                <div class="flex-1">
                    <Input
                        class="border-none"
                        tabindex="0"
                        placeholder="Max"
                        :modelValue="state.maxHealth ?? undefined"
                        @update:modelValue="
                            (h) => (state.maxHealth = String(h))
                        "
                        @blur="onBlurOfInput" />
                </div>
            </section>
            <section class="flex w-full items-center" v-else>
                <Input
                    class="flex-1"
                    :modelValue="state.roll ?? undefined"
                    @update:modelValue="(roll) => (state.roll = String(roll))"
                    placeholder="10d20 + 4" />
            </section>
            <div>
                <Button
                    size="icon"
                    type="button"
                    variant="outline"
                    @click="reset">
                    <FontAwesomeIcon :icon="faArrowRotateLeft" />
                </Button>
            </div>
        </div>
        <ErrorPanel v-if="formState.error">
            {{ formState.error }}
        </ErrorPanel>
    </fieldset>
</template>
<script setup lang="ts">
    import {
        faArrowRotateLeft,
        faCircleQuestion,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { z } from "zod";
    import type {
        CharacterHealth,
        UnevaluatedCharacterHealth,
    } from "~/utils/types/models";

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
            if (
                before.healthStrategy != "None" &&
                after.healthStrategy == "None"
            ) {
                state.currentHealth = null;
                state.maxHealth = null;
            }

            if (
                (before.currentHealth == null && after.currentHealth != null) ||
                (before.maxHealth == null && after.maxHealth != null)
            ) {
                state.healthStrategy = "Fixed";
            }
        }
    );

    watch(
        () => ({ ...props }),
        () => {
            const health = props?.health?.value ?? { "!": "None" };

            if (health["!"] == "None") {
                state.healthStrategy = "None";
                state.currentHealth = null;
                state.maxHealth = null;
                state.roll = null;
                return;
            }

            if (health["!"] == "Fixed") {
                state.healthStrategy = "Fixed";
                state.currentHealth = health.currentHealth?.toString() ?? null;
                state.maxHealth = health.maxHealth?.toString() ?? null;
                state.roll = null;
                return;
            }

            if (health["!"] == "Roll") {
                state.healthStrategy = "Roll";
                state.currentHealth = null;
                state.maxHealth = null;
                state.roll = health.rollString;
                return;
            }
        }
    );

    function reset() {
        state.healthStrategy = "None";
        state.currentHealth = null;
        state.maxHealth = null;
        state.roll = null;
        formState.error = null;
    }

    function onBlurOfInput() {
        if (state.healthStrategy == "Fixed") {
            const health = getHealth();
            if (health == false) {
                return;
            }
            if (health["!"] != "Fixed") {
                return;
            }

            state.currentHealth = health.currentHealth?.toString();
            state.maxHealth = health.maxHealth?.toString();
        }
    }

    const schema = z
        .object({
            currentHealth: z.string().nullable(),
            maxHealth: z.string().nullable(),
        })
        .transform((healthValues, ctx) => {
            const parsedCurrentHealth = tryParseNumber(
                healthValues.currentHealth
            );
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

    function getHealth(): CharacterHealth | UnevaluatedCharacterHealth | false {
        formState.error = null;
        if (state.healthStrategy == "None") {
            return {
                "!": "None",
            } satisfies UnevaluatedCharacterHealth;
        }

        if (state.healthStrategy == "Roll") {
            return {
                "!": "Roll",
                rollString: state.roll!,
            };
        }

        const model = {
            ...state,
        };
        const paredModel = schema.safeParse(model);
        if (paredModel.error) {
            formState.error = paredModel.error.issues[0].message;
            return false;
        }

        if (!props.health.isUnevaluated && props.health.value["!"] == "Fixed") {
            return {
                "!": "Fixed",
                currentHealth: paredModel.data.currentHealth!,
                maxHealth: paredModel.data.maxHealth!,
                diceRoll: props.health.value.diceRoll,
            } satisfies CharacterHealth;
        }

        return {
            "!": "Fixed",
            currentHealth: paredModel.data.currentHealth!,
            maxHealth: paredModel.data.maxHealth!,
        };
    }

    defineExpose({
        getHealth,
    });
</script>

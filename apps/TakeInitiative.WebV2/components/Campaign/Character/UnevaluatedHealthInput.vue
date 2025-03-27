<template>
    <div>
        <TooltipWrapper>
            <template #Trigger>
                <label class="text-white pr-2">Health</label>
                <FontAwesomeIcon :icon="faCircleQuestion" />
            </template>
            <template #Content>
                Health is Optional and you can click the arrow to clear it.
                <br />
                The health fields support basic arithmetic including grouping
                with brackets (), add (+), subtract (-), multiply (*) and divide
                (/).
            </template>
        </TooltipWrapper>

        <fieldset class="border rounded-md flex flex-col gap-2 p-2">
            <legend class="mx-2 p-2 flex items-center gap-2">
                <Select
                    :key="props.health['!']"
                    :modelValue="props.health['!']"
                    @update:modelValue="
                        (type) => onChangeType(type as FormHealthInput['!'])
                    ">
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
                    v-if="props.health['!'] != 'Roll'"
                    class="flex flex-1 items-center gap-2 rounded-md bg-take-navy-light justify-center border"
                    :class="{
                        'brightness-75': props.health['!'] === 'None',
                    }">
                    <div class="flex-1">
                        <Input
                            placeholder="Current"
                            :modelValue="
                                props.health['!'] === 'Fixed' &&
                                props.health.currentHealth != null
                                    ? props.health.currentHealth
                                    : undefined
                            "
                            @update:modelValue="onSetCurrentHealth"
                            class="border-none"
                            @blur="
                                (e: Event) => {
                                    onSetCurrentHealth(
                                        (e.target as HTMLInputElement).value
                                    );
                                    emit('evaluateExpression');
                                }
                            " />
                    </div>
                    <span class="cursor-default">/</span>
                    <div class="flex-1">
                        <Input
                            class="border-none"
                            placeholder="Max"
                            :modelValue="
                                props.health['!'] === 'Fixed' &&
                                props.health.maxHealth != null
                                    ? props.health.maxHealth
                                    : undefined
                            "
                            @update:modelValue="onSetMaxHealth"
                            @blur="
                                (e: Event) => {
                                    onSetMaxHealth(
                                        (e.target as HTMLInputElement).value
                                    );
                                    emit('evaluateExpression');
                                }
                            " />
                    </div>
                </section>
                <section class="flex w-full items-center" v-else>
                    <Input
                        class="flex-1"
                        :modelValue="props.health.rollString"
                        @update:modelValue="onSetRoll"
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
            <ErrorPanel v-if="props.error">
                {{ props.error }}
            </ErrorPanel>
        </fieldset>
    </div>
</template>
<script setup lang="ts">
    import {
        faArrowRotateLeft,
        faCircleQuestion,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { z } from "zod";
    import type { FormHealthInput } from "~/utils/forms/healthFormValidator";
    import type { CharacterHealth } from "~/utils/types/models";

    const props = defineProps<{
        health: FormHealthInput;
        error: string | undefined;
    }>();

    const emit = defineEmits<{
        "update:health": [health: FormHealthInput];
        evaluateExpression: [];
    }>();

    function onChangeType(newType: FormHealthInput["!"]) {
        if (newType == "None") {
            emit("update:health", {
                "!": newType,
            });
            return;
        } else if (newType == "Fixed") {
            emit("update:health", {
                "!": newType,
                currentHealth: 0,
                maxHealth: 0,
            });
            return;
        }

        if (newType == "Roll") {
            emit("update:health", {
                "!": newType,
                rollString: "",
            });
            return;
        }
    }

    function onSetCurrentHealth(value: string | number) {
        const newHealthValue: FormHealthInput = {
            "!": "Fixed",
            currentHealth:
                props.health["!"] === "Fixed" ? props.health.currentHealth : 0,
            maxHealth:
                props.health["!"] === "Fixed" ? props.health.maxHealth : 0,
        };

        emit("update:health", {
            ...newHealthValue,
            currentHealth: value,
        });
    }

    function onSetMaxHealth(value: string | number) {
        const newHealthValue: FormHealthInput = {
            "!": "Fixed",
            currentHealth:
                props.health["!"] === "Fixed" ? props.health.currentHealth : 0,
            maxHealth:
                props.health["!"] === "Fixed" ? props.health.maxHealth : 0,
        };

        if (newHealthValue.currentHealth === newHealthValue.maxHealth) {
            emit("update:health", {
                ...newHealthValue,
                currentHealth: value,
                maxHealth: value,
            });
        } else {
            emit("update:health", {
                ...newHealthValue,
                maxHealth: value,
            });
        }
    }

    function onSetRoll(value: string | number) {
        const newHealthValue: FormHealthInput = {
            "!": "Roll",
            rollString: String(value),
        };

        emit("update:health", newHealthValue);
    }

    function reset() {
        emit("update:health", {
            "!": "None",
        });
    }
</script>

<template>
    <FormFieldWrapper label="Initiative" :error="props.error">
        <div class="flex gap-2">
            <Input v-model="initiative" placeholder="1d20 + 2" />
            <TooltipWrapper>
                <template #Trigger>
                    <FontAwesomeIcon :icon="faQuestionCircle" />
                </template>
                <template #Content>
                    <div class="w-[200px] text-wrap">
                        Initiative is rolled when the character enters combat.
                        The higher a character's initiative, the earlier they
                        act in a round of combat. Supports a fixed number, or a
                        dice roll. An example of a roll is `1d20 + 2d4 + 3`,
                        which sums one 20 sided die, two 4 sided dice and adds
                        3. For advantage use `adv(1d20)`, and for disadvantage
                        `dis(1d20)`. To keep the highest of several dice use
                        `kh1(2d20)` (or `kl1` for the lowest).
                    </div>
                </template>
            </TooltipWrapper>
        </div>
    </FormFieldWrapper>
</template>

<script setup lang="ts">
    import { faQuestionCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const initiative = defineModel<string | number | undefined>("initiative");
    const props = defineProps<{
        error: string | undefined;
    }>();
</script>

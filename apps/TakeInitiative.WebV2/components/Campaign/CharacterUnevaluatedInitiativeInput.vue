<template>
    <FormField v-slot="slotProps" :name="fieldName">
        <FormItem v-bind="$attrs">
            <AutoFormLabel v-if="!config?.hideLabel" :required="required">
                {{ config?.label }}
            </AutoFormLabel>
            <FormControl>
                {{ slotProps }}
                <!-- <section>
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
                               :value="state.healthStrategy"
                               @input="
                                   (event: Event) =>
                                       (state.healthStrategy = (
                                           event.target as HTMLSelectElement
                                       ).value as 'None' | 'Fixed' | 'Roll')
                               "
                           >
                               <option class="font-Overpass" value="None">None</option>
                               <option class="font-Overpass" value="Fixed">Fixed</option>
                               <option
                                   class="font-Overpass"
                                   value="Roll"
                                   v-if="props.health.isUnevaluated"
                               >
                                   Roll
                               </option>
                           </select>
                           <section
                               class="flex flex-1 items-center gap-2 rounded-md bg-take-navy-light"
                               :class="{
                                   'brightness-75': state.healthStrategy == 'None',
                               }"
                               v-if="state.healthStrategy != 'Roll'"
                           >
                               <div class="flex-1">
                                   <input
                                       class="w-full rounded-md bg-take-navy-light p-2"
                                       placeholder="Current"
                                       v-model="state.currentHealth"
                                       @blur="onBlurOfInput"
                                   />
                               </div>
                               <span class="cursor-default">/</span>
                               <div class="flex-1">
                                   <input
                                       tabindex="0"
                                       class="w-full rounded-md bg-take-navy-light p-2"
                                       placeholder="Max"
                                       v-model="state.maxHealth"
                                       @blur="onBlurOfInput"
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
                                   buttonColour="take-purple-light"
                                   @clicked="reset"
                               />
                           </div>
                       </div>
                       <label class="text-take-red" v-if="formState.error">
                           {{ formState.error }}</label
                       >
                   </section> -->
            </FormControl>
            <FormDescription v-if="config?.description">
                {{ config.description }}
            </FormDescription>
            <FormMessage />
        </FormItem>
    </FormField>
</template>
<script setup lang="ts">
    import type { FieldProps } from "../ui/auto-form";

    const props = defineProps<FieldProps>();
</script>

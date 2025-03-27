<template>
    <Badge
        v-if="
            props.armourClass &&
            props.armourClassDisplayMethod ==
                ArmourClassDisplayOptionsEnum.RealValue
        "
        variant="outline"
        class="flex gap-1 border-2">
        <FontAwesomeIcon :icon="faShieldHalved" />
        <div class="ws-nowrap min-w-fit">
            {{ props.armourClass }}
        </div>
    </Badge>
</template>
<script setup lang="ts">
    import {
        ArmourClassDisplayOptionsEnum,
        type ArmourClassDisplayOptionValues,
    } from "~/utils/types/models";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import Badge from "~/components/ui/badge/Badge.vue";
    import { faShieldHalved } from "@fortawesome/free-solid-svg-icons";

    const props = withDefaults(
        defineProps<{
            armourClass: number | null | undefined;
            armourClassDisplayMethod?: ArmourClassDisplayOptionValues;
        }>(),
        {
            armourClassDisplayMethod: ArmourClassDisplayOptionsEnum.RealValue,
        }
    );
</script>

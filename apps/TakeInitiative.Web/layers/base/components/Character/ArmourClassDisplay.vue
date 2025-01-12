<template>
    <div
        v-if="
            props.armourClass &&
            props.armourClassDisplayMethod ==
                ArmourClassDisplayOptionsEnum.RealValue
        "
        class="flex select-none items-center gap-2"
    >
        <FontAwesomeIcon icon="shield-halved" />
        <div class="ws-nowrap min-w-fit">
            {{ props.armourClass }}
        </div>
    </div>
</template>
<script setup lang="ts">
import {
    ArmourClassDisplayOptionsEnum,
    type ArmourClassDisplayOptionValues,
} from "base/utils/types/models";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

const props = withDefaults(
    defineProps<{
        armourClass: number | null | undefined;
        armourClassDisplayMethod?: ArmourClassDisplayOptionValues;
    }>(),
    {
        armourClassDisplayMethod: ArmourClassDisplayOptionsEnum.RealValue,
    },
);
</script>

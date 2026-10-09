<template>
    <!-- Damage and heal (18d.2): one amount with Damage and Heal, Enter applies damage.
         Below it, HP and Max HP as plain fields. Heal stops at Max HP; damage goes as
         low as −999 (`applyHpDelta`). -->
    <section
        aria-label="Hit points"
        class="flex flex-col gap-3">
        <div class="flex items-end gap-2">
            <label class="flex min-w-0 flex-1 flex-col gap-1">
                <span class="text-xs font-medium text-muted-foreground"
                    >Amount</span
                >
                <input
                    ref="amountInput"
                    v-model="amount"
                    type="text"
                    inputmode="numeric"
                    enterkeyhint="done"
                    autocomplete="off"
                    placeholder="0"
                    :aria-invalid="amountError ? 'true' : undefined"
                    class="h-11 w-full rounded-md border bg-background px-3 text-base tabular-nums outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm"
                    @keydown.enter.prevent="apply('damage')" />
            </label>
            <Button
                variant="destructive"
                class="h-11 gap-1.5 md:h-9"
                :disabled="disabled"
                @click="apply('damage')">
                <Swords
                    class="size-4"
                    aria-hidden="true" />
                Damage
            </Button>
            <Button
                variant="outline"
                class="h-11 gap-1.5 border-success/50 text-success hover:text-success md:h-9"
                :disabled="disabled"
                @click="apply('heal')">
                <Heart
                    class="size-4"
                    aria-hidden="true" />
                Heal
            </Button>
        </div>
        <p
            v-if="amountError"
            class="-mt-2 text-xs text-destructive-tint">
            {{ amountError }}
        </p>
        <div class="grid grid-cols-2 gap-3">
            <CombatNumberField
                v-model="hp"
                label="HP"
                nullable
                :min="COMBAT_HP_MIN"
                :max="COMBAT_HP_MAX"
                placeholder="–" />
            <CombatNumberField
                v-model="maxHp"
                label="Max HP"
                nullable
                :min="COMBAT_MAX_HP_MIN"
                :max="COMBAT_HP_MAX"
                placeholder="–" />
        </div>
    </section>
</template>

<script setup lang="ts">
    import { Heart, Swords } from "lucide-vue-next";
    import {
        COMBAT_HP_MAX,
        COMBAT_HP_MIN,
        COMBAT_MAX_HP_MIN,
        applyHpDelta,
        parseWholeNumber,
    } from "~/utils/combat";

    defineProps<{ disabled?: boolean }>();
    const hp = defineModel<number | null>("hp", { required: true });
    const maxHp = defineModel<number | null>("maxHp", { required: true });

    const amount = ref("");
    const amountError = ref<string | null>(null);
    const amountInput = ref<HTMLInputElement | null>(null);

    function apply(kind: "damage" | "heal") {
        const { value, error } = parseWholeNumber(
            amount.value,
            0,
            COMBAT_HP_MAX - COMBAT_HP_MIN
        );
        if (error || value === null) {
            amountError.value = error ?? "Type an amount.";
            return;
        }
        amountError.value = null;
        amount.value = "";
        if (value === 0) return;
        hp.value = applyHpDelta(hp.value, maxHp.value, value, kind);
    }

    defineExpose({ focus: () => amountInput.value?.focus() });
</script>

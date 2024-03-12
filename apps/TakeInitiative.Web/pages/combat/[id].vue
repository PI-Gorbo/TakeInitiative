<template>
    <div class="flex h-full w-full flex-col p-2">
        <header class="pb-2 text-center font-NovaCut text-xl text-take-yellow">
            {{ combat?.combatName }}
        </header>
        <main :class="['h-full flex-1 flex flex-row justify-center']">
            <section
                class="flex-1 flex h-full max-w-[1200px] flex-col overflow-y-auto py-4"
            >
                <div>
                    <label>Staged</label>
                    <div class="rounded-lg border-2 border-take-yellow">
                        <div
                            v-for="stagedChar in combat?.stagedList"
                            :key="stagedChar.id"
                        >
                            {{ stagedChar }}
                        </div>
                    </div>
                </div>
                <div class="flex flex-1 flex-col">
                    <label>Initiative List</label>
                    <ul class="flex-1 rounded-lg border-2 border-take-navy-light">
                        <li
                            v-for="activeChar in combat?.initiativeList"
                            :key="activeChar.id"
                        >
                            {{ activeChar }}
                        </li>
                    </ul>
                </div>
            </section>
            <aside class="col-span-3 p-2 h-full flex flex-col items-end">
                <div class="flex-1"></div>
                <textarea
                    class="bg-take-navy-medium p-2 rounded-lg w-full overflow-y-auto"
                    ref="combatLogs"
                    :value="joinedCombatLogs"
                    rows="5"
                    cols="50"
                />
            </aside>
        </main>
        {{ combat?.combatLogs.length }}
    </div>
</template>
<script setup lang="ts">
const { isMobile } = useDevice();
const combatLogs = ref<HTMLDivElement | null>(null);
const route = useRoute();
const combatId = route.params.id;
const combatStore = useCombatStore();
const combat = computed(() => {
    return combatStore.state.combat;
});
const combatStringified = computed(() => JSON.stringify(combatStore.state, null, "\t"));

const { refresh, pending, error } = await useAsyncData("Combat", async () => {
    return await combatStore.setCombat(combatId as string).then(() => true);
});

watch(
    () => combat.value?.combatLogs,
    (before, after) => {
        if (!combatLogs.value) return;
        combatLogs.value.scrollTop = 0;
        // combatLogs.value?.offsetHeight + combatLogs.value?.scrollHeight;
    }
);

const joinedCombatLogs = computed(() =>
    combat.value?.combatLogs
        .reverse()
        .map((log) => `> ${log}`)
        .join("\n")
);

definePageMeta({
    requiresAuth: true,
    middleware: [
        function (to, from) {
            console.log("Navigated to this page");
            if (process.client) {
                const combatStore = useCombatStore();
                combatStore.joinCombat();
            }
        },
    ],
});
</script>

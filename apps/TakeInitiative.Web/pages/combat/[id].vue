<template>
    <div class="flex h-full w-full flex-col">
        <header
            class="text-center font-NovaCut text-xl text-take-yellow flex items-center justify-center py-2"
        >
            {{ combat?.combatName }}
        </header>
        <main :class="['flex h-full flex-1 flex-row justify-center']">
            <section
                class="flex h-full max-w-[1200px] flex-1 flex-col overflow-y-auto gap-2"
            >
                <div
                    class="flex flex-1 flex-col px-2"
                    v-if="combat?.initiativeList.length != 0"
                >
                    <Drop
                        tag="ol"
                        v-if="combat"
                        class="flex-1 select-none rounded-lg border-2 border-take-navy-light h-full"
                        @drop="onActiveCharacter"
                    >
                        <li v-for="item in combat?.initiativeList" :key="item.id">
                            <idv class="item">{{ item }}</idv>
                        </li>
                    </Drop>
                </div>
                <div class="px-2 flex-1">
                    <ul
                        class="flex-1 select-none rounded-lg border-2 border-take-navy-light h-full p-2"
                    >
                        <Drag
                            v-for="stagedChar in combat?.stagedList"
                            :key="stagedChar.id"
                            tag="li"
                        >
                            {{ stagedChar }}
                        </Drag>
                        <li>
                            <div
                                :class="[
                                    'group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light transition-colors hover:border-take-yellow',
                                ]"
                            >
                                <FormButton
                                    class="group-hover:text-take-yellow"
                                    buttonColour="take-navy"
                                    hoverButtonColour="take-navy"
                                    textColour="take-grey"
                                    hoverTextColour="take-yellow"
                                    icon="plus"
                                    label="Stage character"
                                    size="sm"
                                />
                            </div>
                        </li>
                    </ul>
                </div>
                <aside class="flex flex-col p-2">
                    <textarea
                        class="w-full overflow-y-auto rounded-lg bg-take-navy-medium p-2"
                        ref="combatLogs"
                        :value="joinedCombatLogs"
                        rows="5"
                        cols="50"
                    />
                </aside>
            </section>
        </main>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import { Drag, DropList, Drop } from "vue-easy-dnd";
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
    }
);

const joinedCombatLogs = computed(() =>
    combat.value?.combatLogs
        .reverse()
        .map((log) => `> ${log}`)
        .join("\n")
);

async function onActiveCharacter() {}

definePageMeta({
    requiresAuth: true,
    middleware: [
        function (to, from) {
            console.log("Navigated to this page, (to, from)");
            if (!process.client || to.name !== "combat-id") {
                return;
            }

            const combatStore = useCombatStore();
            combatStore.joinCombat();
        },
        function (to) {
            console.log("Navigated to this page, (to)");
            if (!process.client || to.name !== "combat-id") {
                return;
            }

            const combatStore = useCombatStore();
            combatStore.joinCombat();
        },
    ],
});
</script>

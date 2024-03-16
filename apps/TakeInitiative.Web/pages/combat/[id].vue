<template>
    <div class="flex h-full w-full flex-col">
        <header
            class="flex items-center justify-center py-2 text-center font-NovaCut text-xl text-take-yellow"
        >
            {{ combat?.combatName }}
        </header>
        <main :class="['flex h-full flex-1 flex-row justify-center']">
            <section
                class="flex h-full max-w-[1200px] flex-1 flex-col gap-2 overflow-y-auto"
            >
                <div class="flex-1 px-2">
                    <ul
                        class="flex h-full flex-1 select-none flex-col gap-2 rounded-lg border-2 border-take-navy-light p-2"
                    >
                        <li
                            v-for="character in characterList"
                            :key="character.id"
                            class="rounded-xl border-2 border-take-navy-light p-2 grid grid-cols-2 hover:border-take-yellow transition-colors cursor-pointer"
                            @click="() => onClickCharacter(character)"
                        >
                            <header class="flex items-center">
                                <label>{{ character.name }}</label>
                            </header>
                            <body>
                                <ol class="flex flex-row justify-end">
                                    <li>
                                        <FontAwesomeIcon icon="shoe-prints" />
                                        {{ character.initiative.value }}
                                    </li>
                                </ol>
                            </body>
                        </li>
                        <li>
                            <div
                                :class="[
                                    'group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light transition-colors hover:border-take-yellow',
                                ]"
                                @click="onShowNewStagedCharacterModal"
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
                                    :preventClickBubbling="false"
                                />
                            </div>
                        </li>
                    </ul>
                    <Modal ref="stageNewCharacterModal" title="Stage your Character">
                        <CombatStageCharacterForm :onCreate="onUpsertStagedCharacter" />
                    </Modal>
                    <Modal ref="editStagedCharacterModal" title="Edit staged Character">
                        <CombatStageCharacterForm
                            :onEdit="onUpsertStagedCharacter"
                            :onDelete="onDeleteStagedCharacter"
                            :character="lastClickedStagedCharacter!"
                        />
                    </Modal>
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
import Modal from "~/components/Modal.vue";
import type { DeleteStagedCharacterRequest } from "~/utils/api/combat/deleteStagedCharacterRequest";
import type {
    StagedCharacterDTO,
    UpsertStagedCharacterRequest,
} from "~/utils/api/combat/putUpsertStagedCharacter";
import { CombatState, type Combat, type CombatCharacter } from "~/utils/types/models";

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

// Route
const route = useRoute();
const combatId = route.params.id;

// Combat data
const combatStore = useCombatStore();
const combat = computed(() => {
    return combatStore.state.combat;
});

// Main fetch
const { refresh, pending, error } = await useAsyncData("Combat", async () => {
    return await combatStore.setCombat(combatId as string).then(() => true);
});

// Combat logs
const combatLogs = ref<HTMLDivElement | null>(null);
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


const characterList = computed(() =>
combat?.value?.stagedList.sort((a, b) => {
    let fa = a.id.toLowerCase(),
    fb = b.id.toLowerCase();

    if (fa < fb) {
        return -1;
    }
    if (fa > fb) {
        return 1;
    }
    return 0;
})
);

const editStagedCharacterModal = ref<typeof Modal | null>(null);
const lastClickedStagedCharacter = ref<CombatCharacter | undefined>(undefined);
function onClickCharacter(character: CombatCharacter) {
    if (combat.value?.state == CombatState.Open) {
        lastClickedStagedCharacter.value = character;
        editStagedCharacterModal.value?.show();
    }
}

const stageNewCharacterModal = ref<typeof Modal | null>(null);
function onShowNewStagedCharacterModal() {
    stageNewCharacterModal.value?.show();
}

async function onDeleteStagedCharacter(
    req: Omit<DeleteStagedCharacterRequest, "combatId">
) {
    return await useCombatStore()
        .deleteStagedCharacter(req)
        .then(() => {
            editStagedCharacterModal.value?.hide();
        });
}

async function onUpsertStagedCharacter(req: StagedCharacterDTO) {
    return combatStore.upsertStagedCharacter(req)
    .then(() => {
        stageNewCharacterModal.value?.hide();
        editStagedCharacterModal.value?.hide();
    });
}
</script>

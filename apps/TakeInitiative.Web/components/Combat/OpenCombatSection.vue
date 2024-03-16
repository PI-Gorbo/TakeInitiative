<template>
    <div class="flex-1 px-2">
        <ul
            class="flex h-full flex-1 select-none flex-col gap-2 rounded-lg border-2 border-take-navy-light p-2"
        >
            <li
                v-for="stagedChar in stagedCharacterList"
                :key="stagedChar.id"
                class="rounded-xl border-2 border-take-navy-light p-2 grid grid-cols-2 hover:border-take-yellow transition-colors cursor-pointer"
                @click="() => onShowEditStagedCharacterModal(stagedChar)"
            >
                <header class="flex items-center">
                    <label>{{ stagedChar.name }}</label>
                </header>
                <body>
                    <ol class="flex flex-row justify-end">
                        <li>
                            <FontAwesomeIcon icon="shoe-prints" />
                            {{ stagedChar.initiative.value }}
                        </li>
                    </ol>
                </body>
            </li>
            <li>
                <div
                    :class="[
                        'group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light transition-colors hover:border-take-yellow',
                    ]"
                    @click="onShowStageNewCharacterModal"
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
            <!-- <Tabs
                :renameTabs="{
                    YourCharacters: 'Your Characters',
                    CreateACharacter: 'Create a Character',
                }"
                backgroundColour="take-navy-medium"
            >
                <template #YourCharacters></template>
                <template #CreateACharacter>
                </template>
            </Tabs> -->
        </Modal>
        <Modal ref="editStagedCharacterModal" title="Edit staged Character">
            <CombatStageCharacterForm
                :onEdit="onUpsertStagedCharacter"
                :onDelete="onDeleteStagedCharacter"
                :character="lastClickedStagedCharacter!"
            />
        </Modal>
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
import type { Combat, CombatCharacter } from "~/utils/types/models";

const stageNewCharacterModal = ref<typeof Modal | null>(null);
const editStagedCharacterModal = ref<typeof Modal | null>(null);
const stagedCharacterList = computed(() =>
    props.combat?.stagedList.sort((a, b) => {
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

const props = withDefaults(
    defineProps<{
        combat: Combat;
        upsertStagedCharacter: (request: UpsertStagedCharacterRequest) => Promise<any>;
    }>(),
    {}
);

const lastClickedStagedCharacter = ref<CombatCharacter | undefined>(undefined);
function onShowEditStagedCharacterModal(stagedCharacter: CombatCharacter) {
    lastClickedStagedCharacter.value = stagedCharacter;
    editStagedCharacterModal.value?.show();
}

function onShowStageNewCharacterModal(stagedCharacter: CombatCharacter | undefined) {
    stageNewCharacterModal.value?.show();
}

async function onUpsertStagedCharacter(req: StagedCharacterDTO) {
    return props
        .upsertStagedCharacter({
            combatId: props.combat.id,
            character: req,
        })
        .then(() => {
            stageNewCharacterModal.value?.hide();
            editStagedCharacterModal.value?.hide();
        });
}

async function onDeleteStagedCharacter(
    req: Omit<DeleteStagedCharacterRequest, "combatId">
) {
    return await useCombatStore()
        .deleteStagedCharacter({
            combatId: props.combat.id,
            ...req,
        })
        .then(() => {
            editStagedCharacterModal.value?.hide();
        });
}
</script>

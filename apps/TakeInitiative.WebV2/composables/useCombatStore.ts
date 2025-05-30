import {
    CombatState,
    type InitiativeCharacter,
    type StagedCharacter,
} from "~/utils/types/models";
import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
import { getCombatQuery } from "~/utils/queries/combats";
import { useQuery } from "@tanstack/vue-query";
import { getCampaignQuery } from "~/utils/queries/campaign";
import { faCircleUser, faCrown, faUserLarge } from "@fortawesome/free-solid-svg-icons";
export type InitiativePlayerDto = {
    user: CampaignMemberDto;
    character: InitiativeCharacter;
};
export type StagedPlayerDto = {
    user: CampaignMemberDto;
    character: StagedCharacter;
};

export const useCombatStore = defineStore("combatStore", () => {

    const userStore = useUserStore()

    // Setup Campaign & Combat Query
    const campaignId = ref<string | null>(null)
    const combatId = ref<string | null>(null)

    function init(newCampaignId: string | null, newCombatId: string | null) {
        campaignId.value = newCampaignId;
        combatId.value = newCombatId;
        campaignQuery.refetch();
        combatQuery.refetch();
    }
    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const combatQuery = useQuery(getCombatQuery(campaignId, combatId))

    // Query Details
    const isLoading = computed(
        () => campaignQuery.isLoading.value || combatQuery.isLoading.value
    );

    // User Details
    const userIsDm = computed(() => {
        return (
            userStore.state.user?.userId ===
            combatQuery.data.value?.combat?.dungeonMaster
        );
    });

    // Aggregations.
    const memberDtos: ComputedRef<CampaignMemberDto[]> = computed(() => {
        if (isLoading.value) {
            return [];
        }

        const list: CampaignMemberDto[] = [
            ...campaignQuery.data.value?.campaignMembers!,
            {
                ...campaignQuery.data.value!.userCampaignMember,
                username: userStore.state.user?.username!,
            },
        ];

        return list;
    });

    const getMemberDetailsFor = (id: string): CampaignMemberDto | undefined =>
        memberDtos.value.find((x) => x.userId == id);

    const orderedStagedCharacterListWithPlayerInfo: ComputedRef<
        StagedPlayerDto[]
    > = computed(() => {
        const compareStrings = (a: string, b: string) => {
            let fa = a.toLowerCase(),
                fb = b.toLowerCase();

            if (fa < fb) {
                return -1;
            }
            if (fa > fb) {
                return 1;
            }
            return 0;
        };

        const openCombatCharacterSortFunc = (
            a: StagedPlayerDto,
            b: StagedPlayerDto
        ): number => {
            const aIsDungeonMaster = a.user?.isDungeonMaster;
            const bIsDungeonMaster = b.user?.isDungeonMaster;
            if (aIsDungeonMaster && !bIsDungeonMaster) {
                return -1;
            } else if (!aIsDungeonMaster && bIsDungeonMaster) {
                return 1;
            }

            // First sort by user,
            let result = compareStrings(a.user?.username!, b.user?.username!);
            if (result != 0) {
                return result;
            }

            // Then sort by character name
            result = compareStrings(a.character.name, b.character.name);
            if (result != 0) {
                return result;
            }

            // Sort by copy number
            result =
                (a.character.copyNumber ?? 0) < (b.character.copyNumber ?? 0)
                    ? -1
                    : 1;

            return result;
        };

        return (
            combatQuery.data.value?.combat?.stagedList
                .map(
                    (x) =>
                        ({
                            user: getMemberDetailsFor(
                                x.playerId
                            )!,
                            character: x,
                        }) satisfies StagedPlayerDto
                )
                .sort(openCombatCharacterSortFunc) ?? []
        );
    });

    return {
        campaignId,
        combatId,
        init,
        isLoading,
        campaignQuery,
        campaign: computed(() => campaignQuery.data.value?.campaign ?? null),
        combat: computed(() => combatQuery.data.value?.combat ?? null),
        combatQuery,
        userIsDm,
        memberDtos,
        orderedStagedCharacterListWithPlayerInfo,
        getMemberDetailsFor,
        combatIsOpen: computed(
            () => combatQuery.data.value?.combat?.state == CombatState.Open
        ),
        combatIsStarted: computed(
            () => combatQuery.data.value?.combat?.state == CombatState.Started
        ),
        combatIsFinished: computed(
            () => combatQuery.data.value?.combat?.state == CombatState.Finished
        ),
        isEditableForUser: (charInfo: {
            user: CampaignMemberDto;
            character: InitiativeCharacter | StagedCharacter;
        }) => {
            return (
                userStore.state.user?.userId == combatQuery.data.value?.combat.dungeonMaster ||
                charInfo.user?.userId == userStore.state.user?.userId
            );
        },
        getIconForUser: (charInfo: {
            user: CampaignMemberDto;
            character: InitiativeCharacter | StagedCharacter;
        }) => {
            const currentUserId = userStore.state.user?.userId;

            if (charInfo.user?.userId == combatQuery.data.value?.combat.dungeonMaster) {
                return faCrown;
            }

            if (charInfo.user?.userId == currentUserId) {
                return faCircleUser;
            }

            return faUserLarge;
        },
    };
});

import type { NavigateToOptions } from "#app/composables/router";

export const useNavigator = () => {
    return {
        toCampaignTab: (
            id: string,
            tab: "summary" | "character" | "combats" | "settings",
        ) => navigateTo(`/campaign/${id}/${tab}`),
        toCombatHistory: (campaignId: string, combatId: string) =>
            navigateTo(
                `/campaign/${campaignId}/summary/combatHistory/${combatId}`,
            ),
        toCreateOrJoinCampaign: () => navigateTo("/createOrJoinCampaign"),
        confirmEmail: () => navigateTo("/confirm"),
        toLogin: (opts: NavigateToOptions | undefined = undefined) =>
            navigateTo("/login", opts),
        toSignUp: () => navigateTo("/signup"),
        toCombat: (id: string) => navigateTo(`/combat/${id}`),
        toHomePage: () => navigateTo("/"),
    };
};

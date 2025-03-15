import type { NavigateToOptions } from "#app/composables/router";

export const useNavigator = () => {
    return {
        toCampaignsList: () => navigateTo('/app/campaigns'),
        toCampaign: (
            id: string,
        ) => navigateTo(`/app/campaigns/${id}`),
        toCampaignTab: (
            id: string,
            tab: "characters" | "combats" | "settings",
        ) => navigateTo(`/app/campaigns/${id}/${tab}`),
        // toCombatHistory: (campaignId: string, combatId: string) =>
        //     navigateTo(
        //         `/app/campaign/${campaignId}/summary/combatHistory/${combatId}`,
        //     ),
        toCreateOrJoinCampaign: () => navigateTo("/createOrJoinCampaign"),
        // confirmEmail: () => navigateTo("/confirm"),
        toLogin: (opts: NavigateToOptions | undefined = undefined) =>
            navigateTo("/login", opts),
        toSignUp: () => navigateTo("/signup"),
        // toCombat: (id: string) => navigateTo(`/combat/${id}`),
        toHomePage: () => navigateTo("/"),
    };
};

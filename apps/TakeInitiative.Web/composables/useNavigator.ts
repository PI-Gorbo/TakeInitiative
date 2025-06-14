import type { NavigateToOptions } from "#app/composables/router";

export const useNavigator = () => {
    return {
        toCampaignsList: () => navigateTo('/app/campaigns'),
        toCampaign: (
            id: string,
        ) => navigateTo(`/app/campaigns/${id}`),
        toCampaignTab: (
            id: string,
            tab: "combats" | "settings",
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
        toCombat: (campaignId: string, combatId: string) => navigateTo({
            name: 'app-campaigns-campaignId-combats-combatId',
            params: {
                campaignId: campaignId,
                combatId: combatId
            }
        }),
        toHomePage: () => navigateTo("/"),
    };
};

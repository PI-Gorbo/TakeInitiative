import type { NavigateToOptions } from "#app/composables/router";

export const useNavigator = () => {
    return {
        toCampaignTab: (
            id: string,
            tab: "summary" | "character" | "combats" | "settings",
        ) => navigateTo(`/campaign/${id}/${tab}`),
        toCreateOrJoinCampaign: () => navigateTo("/createOrJoinCampaign"),
        confirmEmail: () => navigateTo("/confirm"),
        toLogin: () => navigateTo("/login"),
        toSignUp: () => navigateTo("/signup"),
        toCombat: (id: string) => navigateTo(`/combat/${id}`),
        toHomePage: () => navigateTo("/"),
    };
};

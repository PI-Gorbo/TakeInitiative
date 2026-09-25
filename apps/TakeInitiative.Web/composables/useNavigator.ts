import type { NavigateToOptions } from "#app/composables/router";

export const useNavigator = () => {
    return {
        toCampaignsList: () => navigateTo('/app/campaigns'),
        toCampaign: (
            id: string,
        ) => navigateTo(`/app/campaigns/${id}`),
        toCampaignTab: (
            id: string,
            tab: "settings",
        ) => navigateTo(`/app/campaigns/${id}/${tab}`),
        toCreateOrJoinCampaign: () => navigateTo("/createOrJoinCampaign"),
        // confirmEmail: () => navigateTo("/confirm"),
        toLogin: (opts: NavigateToOptions | undefined = undefined) =>
            navigateTo("/login", opts),
        toSignUp: () => navigateTo("/signup"),
        toHomePage: () => navigateTo("/"),
    };
};

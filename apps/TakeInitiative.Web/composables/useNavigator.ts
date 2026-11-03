export const useNavigator = () => {
    return {
        toCampaignsList: () => navigateTo('/app/campaigns'),
        toCampaign: (
            id: string,
        ) => navigateTo(`/app/campaigns/${id}`),
        toCreateOrJoinCampaign: () => navigateTo("/createOrJoinCampaign"),
        toHomePage: () => navigateTo("/"),
    };
};

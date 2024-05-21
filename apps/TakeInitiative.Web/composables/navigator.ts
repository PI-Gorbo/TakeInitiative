export const useNavigator = () => {
    return {
        navigateToCampaignTab: (
            id: string,
            tab: "summary" | "character" | "combats" | "settings",
        ) => navigateTo(`/campaign/${id}/${tab}`),
    };
};

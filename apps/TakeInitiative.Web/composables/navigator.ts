export const useNavigator = () => {
    return {
        toCampaignTab: (
            id: string,
            tab: "summary" | "character" | "combats" | "settings",
        ) => navigateTo(`/campaign/${id}/${tab}`), 
    };
};

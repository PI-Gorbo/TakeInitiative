import { useQuery } from "@tanstack/vue-query";
export const userStoreQueryKey = "user-profile"
export function useUserProfileQuery() {
    const api = useApi();
    const cookie = useCookie(".AspNetCore.Cookies", {
        readonly: true,
    });

    return useQuery({
        queryKey: [userStoreQueryKey],
        queryFn: () => api.user.getUser(),
        enabled: !!cookie.value,
    });
}


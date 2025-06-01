import { queryOptions, useQueryClient } from "@tanstack/vue-query";

export const getUserQueryKey = () => ['userDetails']
export const getUserQuery = () => {
    const api = useApi();
    return queryOptions({
        queryKey: getUserQueryKey(),
        queryFn: api.user.getUser,
        staleTime: 1000 * 60 * 5, // 5 minutes
        initialData: null,
    });
}
import { queryOptions, useMutation, useQueryClient } from "@tanstack/vue-query";

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

export const useUpdateUsernameMutation = () => {
    const api = useApi()
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: api.user.updateUsername,
        onSuccess() {
            queryClient.invalidateQueries({
                queryKey: getUserQueryKey()
            })
        }
    });
}
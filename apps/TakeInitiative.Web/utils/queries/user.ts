import { queryOptions, useMutation, useQueryClient } from "@tanstack/vue-query";
import type { GetUserResponse } from "~/utils/api/user/getUserRequest";
import { userFromGetUserError } from "~/utils/user";

export const getUserQueryKey = () => ['userDetails']

// No `initialData`: it stamps the query fresh, so `staleTime` never lets it fetch.
export const getUserQuery = () => {
    const api = useApi();
    return queryOptions({
        queryKey: getUserQueryKey(),
        queryFn: (): Promise<GetUserResponse | null> =>
            api.user.getUser().catch(userFromGetUserError),
        staleTime: 1000 * 60 * 5, // 5 minutes
        retry: false,
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

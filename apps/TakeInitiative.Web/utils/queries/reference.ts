import { queryOptions } from "@tanstack/vue-query";
import { apiErrorStatus } from "~/utils/apiErrorParser";
import type { RefOrGetter } from "./utils";

// Reference items (20c). The data changes only with a deploy (the API sends a day's
// `Cache-Control`), so a fetched item never goes stale here.

/** One reference item: its summary, stat block and attribution. Not retried on a 404. */
export const getReferenceItemQuery = (provider: RefOrGetter<string>, itemId: RefOrGetter<string>) =>
    queryOptions({
        queryKey: computed(() => ["reference", toValue(provider), toValue(itemId)]),
        queryFn: ({ queryKey }) => {
            const [, key, id] = queryKey as [string, string, string];
            return useApi().reference.get({ provider: key, itemId: id });
        },
        enabled: () => !!toValue(provider) && !!toValue(itemId),
        staleTime: Infinity,
        retry: (failureCount, error) => apiErrorStatus(error) !== 404 && failureCount < 3,
    });

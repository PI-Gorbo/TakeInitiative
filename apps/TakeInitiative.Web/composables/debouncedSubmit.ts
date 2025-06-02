import { useDebounceFn, useTimeoutFn } from "@vueuse/core";

export type useDebouncedAsyncFnOptions<TReturnType> = {
    debounceDuration?: number;
    outcomeStateDuration?: number;
    isSuccess?: (r: TReturnType) => boolean;
};
export const useDebouncedAsyncFn = <Args extends unknown[], Return>(
    func: (...args: Args) => Promise<Return>,
    options?: useDebouncedAsyncFnOptions<Return>
) => {
    const state = ref<"Idle" | "TriggeredAndWaiting" | "Success" | "Failure">(
        "Idle"
    );
    const resetBackToIdleTimeout = useTimeoutFn(
        () => (state.value = "Idle"),
        options?.outcomeStateDuration ?? 4000
    );
    const _debouncedSubmit = useDebounceFn(
        (...args: Args) =>
            func(...args).then((res) => {
                if (options?.isSuccess == undefined) {
                    state.value = "Success";
                } else {
                    if (options?.isSuccess(res)) {
                        state.value = "Success";
                    } else {
                        state.value = "Failure";
                    }
                }
                resetBackToIdleTimeout.start();
                return res;
            }),
        options?.debounceDuration ?? 1000
    );

    return {
        state,
        debouncedSubmit: async (...args: Args) => {
            resetBackToIdleTimeout.stop();
            state.value = "TriggeredAndWaiting";
            try {
                return await _debouncedSubmit(...args);
            } catch {
                state.value = "Failure";
                resetBackToIdleTimeout.start();
                return null;
            }
        },
    };
};

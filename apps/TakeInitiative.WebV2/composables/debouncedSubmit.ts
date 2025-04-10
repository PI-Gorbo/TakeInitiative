import { useDebounceFn, useTimeoutFn } from "@vueuse/core";


export const useDebouncedAsyncFn = <Args extends unknown[], Return>(func: (...args: Args) => Promise<Return>, debounceDuration: number = 1000, successStateDuration: number = 4000) => {

    const state = ref<'Idle' | 'TriggeredAndWaiting' | 'Success' | 'Failure'>('Idle');
    const resetBackToIdleTimeout = useTimeoutFn(() => state.value = "Idle", successStateDuration)
    const _debouncedSubmit = useDebounceFn(
        (...args: Args) => func(...args).then(res => {
            state.value = 'Success';
            resetBackToIdleTimeout.start();
            return res;
        }),
        debounceDuration
    );

    return {
        state,
        debouncedSubmit: async (...args: Args) => {
            resetBackToIdleTimeout.stop();
            state.value = 'TriggeredAndWaiting';
            try {
                return await _debouncedSubmit(...args)
            } catch {
                state.value = 'Failure'
                resetBackToIdleTimeout.start();
                return null;
            }
        }
    }
}
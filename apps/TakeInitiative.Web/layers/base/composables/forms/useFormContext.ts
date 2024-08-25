export type FormContext = ReturnType<typeof useFormContext>;
export const useFormContext = () => {
    const formListeners: Record<string, () => unknown> = {};
    function onBeforeSubmit(key: string, func: () => unknown) {
        formListeners.key = func;
    }

    function triggerBeforeSubmit() {
        for (let func of Object.values(formListeners)) {
            func();
        }
    }

    return {
        onBeforeSubmit,
        triggerBeforeSubmit,
    };
};

export type FormInputProps<T> = {
    label?: string;
    value: T;
    errorMessage: string | undefined | null;
};

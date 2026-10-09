import type { AxiosError } from "axios";
import type { extendNuxtSchema } from "nuxt/kit";
import type { Path } from "vee-validate";
import { z } from "zod";

export type ApiError<TRequest extends {}> = {
    statusCode: number;
    message: string;
    errors: Partial<Record<Path<TRequest> | 'generalErrors', string[]>> | null;
    error: AxiosError<any>;
    getUntypedError: (name: string) => string[] | null
};
const apiErrorSchema = z
    .object({
        statusCode: z.number(),
        message: z.string(),
        errors: z.record(z.string(), z.array(z.string())),
    })
    .required();

export function parseAsApiError<TRequest extends {}>(
    error: AxiosError<any>,
): ApiError<TRequest> {
    try {
        const result = apiErrorSchema.parse(error?.response?.data);
        return {
            statusCode: result.statusCode,
            message: result.message,
            errors: result.errors,
            error,
            getUntypedError: (name) => result.errors[name]
        };
    } catch (err) {
        return {
            statusCode: error.status ?? 500,
            message: "Something went wrong",
            errors: null,
            error,
            getUntypedError: (name) => null
        };
    }
}

/** The first validation or general error the API sent, for a toast; `fallback` otherwise. */
export function apiErrorMessage(error: unknown, fallback: string): string {
    const parsed = apiErrorSchema.safeParse((error as AxiosError<any> | undefined)?.response?.data);
    if (!parsed.success) return fallback;
    return Object.values(parsed.data.errors).flat()[0] ?? fallback;
}

/** The HTTP status of a failed request, if it got an answer. */
export const apiErrorStatus = (error: unknown): number | undefined =>
    (error as AxiosError | undefined)?.response?.status;

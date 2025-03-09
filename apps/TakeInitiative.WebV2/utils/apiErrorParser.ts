import type { AxiosError, AxiosResponse } from "axios";
import type { extendNuxtSchema } from "nuxt/kit";
import type { Path } from "vee-validate";
import { z } from "zod";

export type ApiError<TRequest extends {}> = {
    statusCode: number;
    message: string;
    errors: Partial<Record<Path<TRequest> | 'generalErrors', string[]>> | null;
    error: AxiosError<any>;
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
        };
    } catch (err) {
        return {
            statusCode: error.status ?? 500,
            message: "Something went wrong",
            errors: null,
            error,
        };
    }
}

export function validateResponse<T extends {}>(
    resp: AxiosResponse<T, any>,
    schema: {
        parse: (data: any) => T;
    },
): T {
    try {
        const data = schema.parse(resp.data);
        return data;
    } catch (e) {
        console.error(`Failed to validate response. ${e}`);
        throw e;
    }
}

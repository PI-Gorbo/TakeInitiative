import type { AxiosError, AxiosResponse } from "axios";
import type { extendNuxtSchema } from "nuxt/kit";
import type { Path } from "vee-validate";
import { z } from "zod";

export type ApiError<TRequest extends {}> = {
    statusCode: number;
    message: string;
    errors: { [key: string]: string[] };
    getErrorFor: <TPath extends Path<TRequest>>(
        key: string | "generalErrors" | TPath,
    ) => string | null;
    error: AxiosError<any>;
};
const apiErrorSchema = z
    .object({
        statusCode: z.number(),
        message: z.string(),
        errors: z.record(z.string(), z.array(z.string())),
    })
    .required();

export async function parseAsApiError<TRequest extends {}>(
    error: AxiosError<any>,
): ApiError<TRequest> {
    try {
        const result = apiErrorSchema.parse(error?.response?.data);
        return {
            statusCode: result.statusCode,
            message: result.message,
            errors: result.errors,
            getErrorFor: (error) => {
                if (error in result.errors) {
                    //@ts-ignore
                    return result.errors[error][0];
                }

                try {
                    const accessors = error.split(".");
                    let errorValue = result.errors;
                    for (let index = 0; index < accessors.length; index++) {
                        //@ts-ignore
                        errorValue = errorValue[accessors[index]];
                    }
                    //@ts-ignore
                    if (errorValue == null || errorValue.length == 0) {
                        return null;
                    }
                    //@ts-ignore
                    return errorValue[0];
                } catch {
                    return null;
                }
            },
            error,
        };
    } catch (err) {
        const validationError: yup.ValidationError = err as yup.ValidationError;
        return {
            statusCode: error.status ?? 500,
            message: "Something went wrong",
            errors: {},
            getErrorFor: (err) => null,
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
    return schema.parse(resp.data);
}

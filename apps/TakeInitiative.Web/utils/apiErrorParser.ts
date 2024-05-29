import type { AxiosError } from "axios";
import type { extendNuxtSchema } from "nuxt/kit";
import type { Path, YupSchema } from "vee-validate";
import * as yup from "yup";

export type ApiError<TRequest extends {}> = {
    statusCode: number;
    message: string;
    errors: { [key: string]: string[] };
    getErrorFor: (
        key: string | "generalErrors",
    ) => string | null;
    error: AxiosError<any>;
};
const apiErrorSchema = yup.object({
    statusCode: yup.number().required(),
    message: yup.string().required(),
    errors: yup.object().required(),
});
export async function parseAsApiError<TRequest extends {}>(
    error: AxiosError<any>,
): Promise<ApiError<TRequest>> {
    try {
        const result = await apiErrorSchema.validate(error?.response?.data);
        return {
            statusCode: result.statusCode,
            message: result.message,
            errors: result.errors,
            getErrorFor: (error) => {
                if (error in result.errors) {
                    return result.errors[error][0]
                }

                try {
                    const accessors = error.split(".");
                    let errorValue = result.errors;
                    for (let index = 0; index < accessors.length; index++) {
                        errorValue = errorValue[accessors[index]];
                    }
                    if (errorValue == null || errorValue.length == 0) {
                        return null;
                    }
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

export async function validateWithSchema<T extends {}>(
    data: any,
    schema: {
        validate: (data: any) => Promise<T>;
    },
): Promise<T> {
    return await schema
        .validate(data)
        .catch((validationError) => {
            console.error("API VALIDATION ERROR: ", validationError);
        })
        .then((data) => data as unknown as T); // Typescript workaround.
}

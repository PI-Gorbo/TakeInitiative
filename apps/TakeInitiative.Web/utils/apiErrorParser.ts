import type { AxiosError } from "axios";
import type { extendNuxtSchema } from "nuxt/kit";
import type { Path, YupSchema } from "vee-validate";
import * as yup from "yup";

export type ApiError<TRequest extends {}> = {
    statusCode: number;
    message: string;
    errors: {
        generalErrors?: string[];
    } & Partial<Record<Path<TRequest>, string[]>>;
    getErrorFor: <TPath extends Path<TRequest>>(key: TPath | "generalErrors") => string | null;
};
const apiErrorSchema = yup.object({
    statusCode: yup.number().required(),
    message: yup.string(),
    errors: yup.object().required(),
});
export async function parseAsApiError<TRequest extends {}>(
    error: AxiosError<any>,
): Promise<ApiError<TRequest>> {
    try {
        const errorObject = error.response?.data;
        const result = await apiErrorSchema.validate(error?.response?.data);
        return {
            ...errorObject,
            getErrorFor: (error) => {
                const errorList = (errorObject as ApiError<TRequest>).errors[
                    error
                ];
                if (errorList == null || errorList.length == 0) {
                    return null;
                }
                return errorList[0];
            },
        } satisfies ApiError<TRequest>;
    } catch {
        return {
			errors: {
				generalErrors: [JSON.stringify(error.body)],
			},
            getErrorFor: (error) => {
                const errorList = (errorObject as ApiError<TRequest>).errors[
                    error
                ];
                if (errorList == null || errorList.length == 0) {
                    return null;
                }
                return errorList[0];
            },
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

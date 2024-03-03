import type { AxiosError } from "axios";
import type { extendNuxtSchema } from "nuxt/kit";
import type { Path, YupSchema } from "vee-validate";
import * as yup from "yup";




export type ApiError<TRequest extends {}> = {
    statusCode: number;
    message: string;
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
				try {
					const accessors = error.split('.');
					let errorValue = errorObject.errors
					for (let index = 0; index < accessors.length; index++) {
						errorValue = errorValue[accessors[index]]
					}
					
					if (errorValue == null || errorValue.length == 0) {
						return null;
					}
					return errorValue[0];
				} catch {
					return null;
				}
            },
        } satisfies ApiError<TRequest>;
    } catch {
        return {
			statusCode: 500,
			message: "Something went wrong.",
			getErrorFor: (error) => {
				return null
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

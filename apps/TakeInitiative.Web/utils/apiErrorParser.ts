import type { AxiosError } from 'axios';
import * as yup from 'yup';
export type ApiError<TRequest extends {}> = {
	statusCode: number,
	message: string,
	errors: {
		generalErrors?: string[]
	} & Partial<Record<keyof TRequest, string[]>>
	getErrorFor: (key: keyof TRequest | 'generalErrors') => string | null
}
const apiErrorSchema = yup.object({
	statusCode: yup.number().required(),
	message: yup.string(),
	errors: yup.object().required(),
})
export async function parseAsApiError<TRequest extends {}>(error: AxiosError<any>) : Promise<ApiError<TRequest>> {
	try {
		const errorObject = error.response?.data
		const result = await apiErrorSchema.validate(error?.response?.data) 
		return {
			...errorObject, 
			getErrorFor: (error) => {
				const errorList = (errorObject as ApiError<TRequest>).errors[error]
				if (errorList == null || errorList.length == 0) {
					return null
				}
				return errorList[0]
			}  
		} satisfies ApiError<TRequest>
	} catch {
		throw new Error("Could not parse api error")
	}
}
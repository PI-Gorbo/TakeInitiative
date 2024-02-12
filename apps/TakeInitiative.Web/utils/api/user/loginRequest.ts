import type { AxiosInstance, AxiosResponse } from "axios";
import * as yup from "yup";

export type LoginRequest = {
    email: string;
    password: string;
};
const loginResponseSchema = yup.object({
    token: yup.string().required(),
});
export type LoginResponse = yup.InferType<typeof loginResponseSchema>;
export function loginRequest(axios: AxiosInstance) {
    return async function(
        request: LoginRequest,
    ): Promise<LoginResponse> {
        return await axios
            .put("/api/login", request)
            .then((response) => loginResponseSchema.validate(response.data));
    };
}
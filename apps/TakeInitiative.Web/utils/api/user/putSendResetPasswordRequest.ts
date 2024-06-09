import type { AxiosInstance } from "axios";
import type { InferType } from "yup";
import { yup } from "~/utils/types/HelperTypes";

export const SendResetPasswordEmailRequestValidator = yup.object({
    email: yup.string().email().required(),
});
export type SendResetPasswordEmailRequest = InferType<
    typeof SendResetPasswordEmailRequestValidator
>;

export function putSendResetPasswordRequest(axios: AxiosInstance) {
    return async function (email: string) {
        return await axios.put("/api/sendResetPasswordEmail", {
            email,
        });
    };
}

import type { AxiosInstance } from "axios";
import { z } from "zod";

export const SendResetPasswordEmailRequestValidator = z
    .object({
        email: z.string().email(),
    })
    .required();
export type SendResetPasswordEmailRequest = z.infer<
    typeof SendResetPasswordEmailRequestValidator
>;

export function putSendResetPasswordRequest(axios: AxiosInstance) {
    return async function (email: string) {
        return await axios.put("/api/sendResetPasswordEmail", {
            email,
        });
    };
}

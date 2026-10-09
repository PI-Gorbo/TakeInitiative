import type { AxiosInstance } from "axios";
import type { ApiPathParams } from "../types";

// Delete Image (its uploader, while it is on no note): ✕ on an attachment. 204.
export type DeleteImageRequest = ApiPathParams<"DeleteImage">;
export function deleteImageRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        imageId,
    }: DeleteImageRequest): Promise<void> {
        await axios.delete(
            `/api/campaigns/${encodeURIComponent(campaignId)}/images/${encodeURIComponent(imageId)}`
        );
    };
}

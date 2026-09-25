import type { AxiosInstance } from "axios";
import type { ApiPathParams, ApiResponse } from "../types";

// Post Image (members): upload one file as `multipart/form-data` (16a). The image is
// on no note, and only its uploader can see it, until a note's `imageIds` takes it.
export type PostImageRequest = ApiPathParams<"PostImage"> & {
    file: Blob;
    fileName?: string;
    /** 0–1, as the bytes go up. */
    onProgress?: (progress: number) => void;
    signal?: AbortSignal;
};
export type PostImageResponse = ApiResponse<"PostImage">;
export function postImageRequest(axios: AxiosInstance) {
    return async function ({
        campaignId,
        file,
        fileName,
        onProgress,
        signal,
    }: PostImageRequest): Promise<PostImageResponse> {
        const form = new FormData();
        form.append(
            "file",
            file,
            fileName ?? (file instanceof File ? file.name : "image")
        );
        const response = await axios.post<PostImageResponse>(
            `/api/campaigns/${encodeURIComponent(campaignId)}/images`,
            form,
            {
                signal,
                onUploadProgress: (event) => {
                    if (onProgress && event.total)
                        onProgress(event.loaded / event.total);
                },
            }
        );
        return response.data;
    };
}

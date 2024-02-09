const { $axios } = useNuxtApp();
import type { AxiosResponse } from "axios";
import * as yup from "yup";

const getUserCampaignDto = yup.object({
	campaignName: yup.string(),
	campaignId: yup.string()
})
const getUserResponseSchema = yup.object({
	dmCampaigns: yup.array(getUserCampaignDto),
	memberCampaigns: yup.array(getUserCampaignDto)
});
export type GetUserResponse = yup.InferType<typeof getUserResponseSchema>
export const user = {
    async getUser() : Promise<GetUserResponse> {
        return $axios.get("/api/user")
			.then(response => getUserResponseSchema.validate(response.data));   
    },
};

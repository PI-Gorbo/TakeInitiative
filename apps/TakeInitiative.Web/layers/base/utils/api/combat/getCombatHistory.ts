// import type { AxiosInstance } from "axios";
// import type { GetCombatRequest } from "./getCombatRequest";
// import * as yup from "yup";

// // Create Campaign
// export type GetCombatHistoryRequest = {
//     combatId: string;
// };

// export const getCombatHistoryResponseValidator = yup.object({
//     events: yup.array(
//         yup.object({
//             eventName: yup.string().required(),
//             userId: yup.string().required(),
//         }),
//     ),
//     playerList: yup.array(yup.string()),
// });
// export type GetCombatHistoryResponse = yup.InferType<
//     typeof getCombatHistoryResponseValidator
// >;

// export function getCombatHistoryRequest(axios: AxiosInstance) {
//     return async function (
//         request: GetCombatRequest,
//     ): Promise<GetCombatHistoryResponse> {
//         return await axios
//             .get(`/api/combat/${request.combatId}/history`)
//             .then((resp) =>
//                 validateWithSchema(
//                     resp.data,
//                     getCombatHistoryResponseValidator,
//                 ),
//             );
//     };
// }

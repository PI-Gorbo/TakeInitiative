import type { AxiosInstance } from "axios";

export function putReprojectCombatsRequest(axios: AxiosInstance) {
    return () => axios.put("/api/admin/reproject/combat");
}

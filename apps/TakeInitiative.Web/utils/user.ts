import { isAxiosError } from "axios";

/** A 401 from `GET /api/user` is an answer, not a failure: nobody is signed in. */
export function userFromGetUserError(error: unknown): null {
    if (isAxiosError(error) && error.response?.status === 401) {
        return null;
    }
    throw error;
}

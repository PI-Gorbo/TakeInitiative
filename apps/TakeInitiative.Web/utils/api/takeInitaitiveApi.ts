import { user } from "./user";

export const useTakeInitApi = () => {
	const {$axios} = useNuxtApp()
	return {
		user: user($axios),
	}
};

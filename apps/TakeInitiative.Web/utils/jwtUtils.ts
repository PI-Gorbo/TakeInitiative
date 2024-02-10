export const localStorageJwtKey = "TakeInitiative_Token";
export default {
    localStorageJwtKey,
	getJwt() {
		return window.localStorage.getItem(localStorageJwtKey);
	},
	setJwt(jwt: string) : boolean {
		if (!this.isValidJwt(jwt)) {
			return false;
		}
		window.localStorage.setItem(localStorageJwtKey, jwt)
		return true;
	},
    isValidJwt(jwt: string): boolean {
        if (jwt == null) {
            return false;
        }
		const parseJwtResult = this.parseJwt(jwt)
		console.log("Result of parsing the jwt.",parseJwtResult)
        return parseJwtResult == true;
    },
    parseJwt(jwt: string): boolean | null {
        debugger;
        try {
            var base64Url = jwt.split(".")[1];
            var base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/");
            var jsonPayload = decodeURIComponent(
                window
                    .atob(base64)
                    .split("")
                    .map(function (c) {
                        return (
                            "%" +
                            ("00" + c.charCodeAt(0).toString(16)).slice(-2)
                        );
                    })
                    .join(""),
            );
            return JSON.parse(jsonPayload);
        } catch {
            return null;
        }
    },
};

export const localStorageJwtKey = "TakeInitiative_Token";
export type TakeInitJWT = {
	UserId: string,
	exp: number, // Expiration time
	iat: number, // Issued At Time
	nbf: number // Not Before Time
}
export default {
    localStorageJwtKey,
	getJwtAsString() {
		return window.localStorage.getItem(localStorageJwtKey);
	},
	getJwt() : TakeInitJWT | false {
		const jwtAsString = this.getJwtAsString()
		if (jwtAsString == null) {
			return false
		}
		return this.validateJwt(jwtAsString)
	},
	setJwt(jwt: string) : boolean {
		if (!this.validateJwt(jwt)) {
			return false;
		}
		window.localStorage.setItem(localStorageJwtKey, jwt)
		return true;
	},
    validateJwt(jwt: string): TakeInitJWT | false {
        if (jwt == null) {
            return false;
        }
		const parseJwtResult = this.parseJwt(jwt)
		console.log("Result of parsing the jwt.",parseJwtResult)
		if (parseJwtResult == null) {
			return false
		}

		const time = new Date().getTime() / 1000; // Get into seconds
		if (parseJwtResult.exp <= time) {
			return false
		}

		if (parseJwtResult.iat > time) {
			return false;
		}

		if (parseJwtResult.nbf > time) {
			return false;
		}

        return parseJwtResult;
    },
    parseJwt(jwt: string): TakeInitJWT | null {
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
            return JSON.parse(jsonPayload) as TakeInitJWT;
        } catch {
            return null;
        }
    },
};

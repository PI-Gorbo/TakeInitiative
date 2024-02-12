const localStorageJwtKey = "TakeInitiative_Token";
export type TakeInitJWT = {
	UserId: string,
	exp: number, // Expiration time
	iat: number, // Issued At Time
	nbf: number // Not Before Time
}

export const useAuthPersistence = () => {
	const authToken = useCookie(localStorageJwtKey)
	function parseToken(jwt: string): TakeInitJWT | null {
		try {
			var base64Url = jwt.split(".")[1];
			var base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/");
			var jsonPayload = decodeURIComponent(
					atob(base64)
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
		} catch (error) {
			return null;
		}
	}

	function validateToken(jwt: string): TakeInitJWT | false {
		if (jwt == null) {
			return false;
		}
		const parseJwtResult = parseToken(jwt)
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
	}


	return {
		getTokenAsString() {
			return authToken.value
		},
		getToken() : TakeInitJWT | false {
			const jwtAsString = this.getTokenAsString()
			if (jwtAsString == null) {
				return false
			}
			return validateToken(jwtAsString)
		},
		setToken(jwt: string) : boolean {
			debugger;
			if (!validateToken(jwt)) {
				return false;
			}
			authToken.value = jwt
			return true;
		},
		clearToken() : void {
			authToken.value = null
		}	
	};
}
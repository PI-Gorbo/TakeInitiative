export const LocalStorageJwtKey = "TakeInitiative_Token";
export default {
    LocalStorageJwtKey: "TakeInitiative_Token",
    isValidJwt(jwt: string): boolean {
        if (jwt == null) {
            return false;
        }

        return this.parseJwt(jwt) == true;
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

ifeq ($(OS),Windows_NT)
SHELL := pwsh.exe
.SHELLFLAGS := -NoProfile -Command
endif

api:
	dotnet run
api.watch: 
	dotnet watch run

generate.client:

	nswag openapi2csclient /input:swagger.json /classname:TakeApiClient /namespace:TakeInitiative.Api.Client /output:../TakeInitiative.Api.Client/TakeApiClient.cs
	cd ../TakeInitiative.Web/ && npx swagger-typescript-api -p ../TakeInitiative.Api/swagger.json -o ./layers/base/utils/api -n api.ts --axios

ifeq ($(OS),Windows_NT)
SHELL := powershell.exe
.SHELLFLAGS := -NoProfile -Command
endif

api:
	dotnet run
api.watch: 
	dotnet watch run

generate.client:
	cp swagger.json ../TakeInitiative.Web/swagger.json
	nswag openapi2csclient /input:swagger.json /classname:TakeApiClient /namespace:TakeInitiative.Api.Client /output:../TakeInitiative.Api.Client/TakeApiClient.cs

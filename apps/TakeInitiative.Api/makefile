api:
	dotnet run
api.watch: 
	dotnet watch run

generate.client:
	nswag openapi2csclient /input:swagger.json /classname:TakeApiClient /namespace:TakeInitiative.Api.Client /output:../TakeInitiative.Api.Client/TakeApiClient.cs

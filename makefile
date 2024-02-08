SHELL := pwsh -NoProfile

## Database
db:
	docker run -p 5432:5432 --name takedb -e POSTGRES_PASSWORD=postgres -d postgres 

updb: db
dropdb:
	docker stop takedb
	docker rm takedb
refreshdb: dropdb updb

## Api
api:
	dotnet run --project ./apps/TakeInitiative.Api
watchApi: 
	dotnet watch run --project ./apps/TakeInitiative.Api
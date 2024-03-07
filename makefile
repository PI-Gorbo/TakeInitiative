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
docker.api:
	docker build -t takeinitiative-api:latest ./apps/TakeInitiative.Api/
docker.drop.api:
	docker stop takeinitiative-api-1
	docker rm takeinitiative-api-1

## Web
docker.web:
	docker build -t takeinitiative-web:latest ./apps/TakeInitiative.Web/
docker.drop.web:
	docker stop takeinitiative-web-1
	docker rm takeinitiative-web-1

## Docker
docker.build: docker.api docker.web
docker.compose: docker.build
	docker compose up -d


	


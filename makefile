## Database
db:
	docker run -p 5432:5432 --name takedb-local -e POSTGRES_PASSWORD=postgres -d postgres 

updb: db
dropdb:
	docker stop takedb-local
	docker rm takedb-local
refreshdb: dropdb updb

## Api
docker.api:
	docker build -t takeinitiative-api:latest ./apps/TakeInitiative.Api/
docker.drop.api:
	docker stop takeapi
	docker rm takeapi

## Web
docker.web:
	docker build -t takeinitiative-web:latest ./apps/TakeInitiative.Web/
docker.drop.web:
	docker stop takeweb
	docker rm takeweb

## Database
docker.databsae:
	docker run -p 5432:5432 --name takedb -e POSTGRES_PASSWORD=postgres -d postgres 
docker.drop.database:
	docker stop takedb
	docker rm takedb

## Docker
docker.build: docker.api docker.web
docker.compose: docker.build
	docker compose up -d
docker.drop: docker.drop.web docker.drop.api dropdb

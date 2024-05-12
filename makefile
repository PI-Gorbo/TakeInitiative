## Api
docker.build.api:
	docker build -t takeinitiative-api:latest ./apps/TakeInitiative.Api/
docker.drop.api:
	docker stop takeapi
	docker rm takeapi
run.api:
	docker start takedb
	cd ./apps/TakeInitiative.Api && dotnet run
run.api.watch:
	cd apps/TakeInitiative.Api && dotnet watch run

## Web
docker.build.web:
	docker build -t takeinitiative-web:latest ./apps/TakeInitiative.Web/
docker.drop.web:
	docker stop takeweb
	docker rm takeweb
run.web:
	cd ./apps/TakeInitiative.Web && npm run dev

## Database
docker.isolated.database:
	docker run -p 5432:5432 --name takedb -e POSTGRES_PASSWORD=postgres -d postgres 
docker.drop.database:
	docker stop takedb
	docker rm takedb

# Compose
docker.build: 
	make docker.build.api
	make docker.build.web

docker.compose:
	docker compose up -d

docker.dev.compose:
	make docker.build
	docker compose -f compose.dev.yml up -d

docker.publish: 
	make docker.build 
	make docker.compose

docker.refresh.web: 
	make docker.build.web 
	make docker.drop.web 
	make docker.compose

docker.refresh.api: 
	make docker.build.api 
	make docker.drop.api 
	make docker.compose

docker.refresh.database:
	make docker.drop.database
	make docker.compose

# TakeInitiative

s
A webapp designed to help dungeon masters to create combats and track initiative for those combats.

## Technologies

-   C# ASP.NET WebAPI for the backend
-   Nuxt Frontend (BFF pattern)

## Requirements for local development

1. Have `Docker` Installed
2. Have `make` installed
3. If you are developing the API:
    - The `dotnet 8 SDK` is required
    - Python3 is required and the d20 package
4. If you are developing the Frontend, then `node` / `npm` is required (>= v18)

## QuickStart

If you want to just get all resources up and running on your computer, without having to install any extra SDKs, run `make docker.dev.compose`

This will:

-   Build a local image of the API and Frontend, with all requirements.
-   Run 3 docker containers, with the Database, API and Frontend.

## Working on individual projects

### API

1. Run the in an individual container using `make docker.isolated.database`
2. Go to the folder `./apps/TakeInitiative.Api/`
3. Set the path for the python3 dll. `appsettings.json` has an example path, but that can be overriden in `appsettings.Development.json`

    An example is as follows:

    `appsettings.Development.json`

    ```
    {
        "Logging": {
            "LogLevel": {
                "Default": "Information",
                "Microsoft.AspNetCore": "Warning"
            }
        },
        "PythonDLL": "my own custom value"
    }
    ```

4. Install the d20 package from python
5. Run `make api` (Runs `dotnet run`)

### Web

1. Go to the folder `./apps/TakeInitiative.Web`
2. Run `npm i` to install all relevant packages.
3. Make a copy of the file `TEMPLATE.env`, and rename it `.env`. This file serves the same utility as the `appsettings.development.json` file on the API.
4. Run `npm run dev` to launch the web.

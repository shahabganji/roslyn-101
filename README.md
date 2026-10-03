# Customer Management System

A .NET 10 demo application for customer registration and management. It combines an ASP.NET Core API and Blazor WebAssembly UI with an event-sourced domain backed by Azure Cosmos DB. The solution also contains Roslyn analyzers, code fixes, and a source generator.

## Solution layout

- `src/App/` contains the API, Blazor UI, domain model, API contracts, and Cosmos DB event store.
- `src/Analyzers/` contains the Roslyn analyzer and code-fix provider.
- `src/SourceGenerator/` contains the event-store source generator.
- `test/` contains analyzer and source-generator tests.
- `packaging/` contains the analyzer package project and packaging script.

## Requirements

- .NET SDK 10.0.401 or a compatible later SDK (see `global.json`).
- An Azure Cosmos DB account with a database named `Swetugg-Demo` and a container named `Customers`.

## Build and test

From the repository root:

```sh
dotnet build CusomerManagementSystem.slnx
dotnet test CusomerManagementSystem.slnx
```

## Run the application

Configure the API's Cosmos DB connection string using .NET user secrets:

```sh
dotnet user-secrets set "ConnectionStrings:CosmosDb" "<cosmos-db-connection-string>" \
  --project src/App/CustomerManagementSystem.Api/CustomerManagementSystem.Api.csproj
```

Then start the API, which also serves the Blazor WebAssembly application:

```sh
dotnet run --project src/App/CustomerManagementSystem.Api/CustomerManagementSystem.Api.csproj
```

In Development, the API's OpenAPI document is available at `/openapi/v1.json`.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).

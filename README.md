# AutoParts

Independent ASP.NET Core Web API for vehicle, part and compatibility catalog management.

For a fuller handoff to another person or AI, read
[`docs/PROJECT_CONTEXT.md`](docs/PROJECT_CONTEXT.md).

## Requirements

- .NET SDK 10
- EF Core CLI (`dotnet tool install --global dotnet-ef --version 10.*` if needed)

## Run

```powershell
dotnet restore
dotnet ef database update --project src/AutoParts.Api
dotnet run --project src/AutoParts.Api --urls http://localhost:5080
```

- Health: `http://localhost:5080/health`
- Swagger: `http://localhost:5080/swagger`
- Catalog JSON: `http://localhost:5080/api/parts/catalog`
- Catalog TSV: `http://localhost:5080/api/parts/catalog.tsv`
- External search: `GET http://localhost:5080/api/external-catalog/search?q=oil`
- Portuguese preview: `GET http://localhost:5080/api/external-catalog/preview/{oem}`
- Import from BMV.parts: `POST http://localhost:5080/api/external-catalog/import/{oem}`

## Curate imported catalog

Process imported BMV.parts parts by vehicle, ordered alphabetically by vehicle/model,
marking screws/nuts/washers/hoses as ignored and translating the remaining part
names:

```powershell
dotnet run --project src/AutoParts.Api -- --curate-imported-parts-by-vehicle --dry-run
dotnet run --project src/AutoParts.Api -- --curate-imported-parts-by-vehicle --translation-batch-size=50
```

After review, physically remove ignored parts and related compatibilities:

```powershell
dotnet run --project src/AutoParts.Api -- --delete-ignored-catalog-items
dotnet run --project src/AutoParts.Api -- --delete-ignored-catalog-items --confirm-delete
```

SQLite is used locally via `appsettings.json`. The database provider is isolated in the application startup and can be replaced without changing controllers or services.

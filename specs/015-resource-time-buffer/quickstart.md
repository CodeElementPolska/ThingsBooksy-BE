# Quickstart — 015-resource-time-buffer

## Build & test loop

```bash
dotnet build backend/ThingsBooksy.slnx
dotnet test backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.IntegrationTests --filter "AC=AC-3|AC=AC-4|AC=AC-5|AC=AC-6|AC=AC-7|AC=AC-8|AC=AC-9"
dotnet format backend/ThingsBooksy.slnx
```

Integration tests need the Docker PostgreSQL (`docker compose up` from PowerShell); `ThingsBooksyWebAppFactory` applies migrations automatically.

## Migration (developer only)

```bash
dotnet ef migrations add SoftDeleteResourceTypeUniqueIndex \
  --project backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations \
  --startup-project backend/src/Bootstrapper/ThingsBooksy.Bootstrapper
```

Expected `Up()`: `DropIndex("IX_resource_types_GroupId_Name")` + `CreateIndex(... unique: true, filter: "\"DeletedAt\" IS NULL")`. Nothing else. If the model changes again inside the story: `migrations remove` then `migrations add` (constitution VI).

## Manual check (Swagger at localhost:8080/swagger)

1. `POST /resources/types` → 201; log shows `ResourceSchemaCreatedEvent` dispatched.
2. `POST /resources/instances` → 201; `ResourceInstanceCreatedEvent`.
3. `DELETE /resources/types/{id}` → 204; row still in `resources.resource_types` with `deleted_at` set; instances have `deleted_at`; one `ResourceSchemaDeletedEvent`.
4. `GET /resources/types/{id}` → 404; `POST /resources/types` with the same name → 201.

## Fleet gates

```bash
node tools/fleet/red-first-prover.js --story 015-resource-time-buffer   # after tests, before behaviour
node tools/fleet/gate.js --story 015-resource-time-buffer               # build, format, tests, swagger re-export, contract-diff, ac-matrix
```

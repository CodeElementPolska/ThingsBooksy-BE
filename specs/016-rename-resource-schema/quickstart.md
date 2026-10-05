# Quickstart: verifying story 016

## After Step 0 (internal rename) - nothing visible may have changed

1. `dotnet build backend/ThingsBooksy.slnx` - green.
2. `dotnet test backend/ThingsBooksy.slnx` - green, same number of tests as before Step 0.
3. `dotnet test backend/src/Shared/ThingsBooksy.Shared.IntegrationTests --filter Category=Tooling`, then `git diff --stat generated/swagger.base.json` - no difference.
4. No file under `backend/src/Modules/Resources/ThingsBooksy.Modules.Resources.Migrations` changed.
5. `git diff --stat frontend` - empty.

## After the migration (developer)

1. The new migration file contains only `RenameTable`, `RenameColumn`, `RenameIndex`, `DropPrimaryKey` / `AddPrimaryKey`, `DropForeignKey` / `AddForeignKey` - no `DropTable`, `CreateTable`, `DropColumn`, `AddColumn`.
2. `node tools/fleet/migration-check.js --story 016-rename-resource-schema` -> expected `REVIEW`; answer the G2b question that names the migration sha.
3. Optional, local only (developer): start the application on the existing development database; it starts, and the schemas and resources created earlier are still listed.

## After the behaviour phase

1. `node tools/fleet/gate.js --story 016-rename-resource-schema` -> GREEN (build, format, tests, swagger re-export, contract-diff CLEAR against `contract-next.json`, AC matrix).
2. Old addresses: `GET /resources/types?groupId=…` -> 404; new: `GET /resources/schemas?groupId=…` -> 200.
3. Search rules (FR-009, FR-011), each must print nothing:
   - backend: case-insensitive `resource[ _]?type` in `backend/src`, excluding `*.Migrations/Migrations/2026051*`, `20260928111659_*` and their designer files;
   - frontend: case-insensitive `resourceType|resources/types` in `frontend/src`; and `resource type` anywhere except `features/auth/auth-page/auth-page.component.html`.

## After the frontend change - owner walk-through (AC-8)

On `http://localhost:4200`: open a group -> Schemas; create a schema; open and edit it; add a resource of that schema. Everything works as before; the resources table header reads `Schema`, an empty schema list reads `Create the first schema to describe your resources.`.

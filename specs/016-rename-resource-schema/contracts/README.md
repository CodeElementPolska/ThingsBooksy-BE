# Contract: story 016

The API contract is never hand-written (constitution, Governance). Sources of truth:

- base: `generated/swagger.base.json`
- delta of this story: `runs/016-rename-resource-schema/contract-delta.overlay.json` (OpenAPI Overlay 1.0, 13 actions, generated from the base)
- result: `runs/016-rename-resource-schema/contract-next.json` (`node tools/fleet/contract-compose.js --story 016-rename-resource-schema`)

## Endpoint diff (from `contract-next.json`)

| Change | Before | After |
|---|---|---|
| removed + added | `POST /resources/types` - `Create resource type` - body `CreateResourceTypeRequest` | `POST /resources/schemas` - `Create resource schema` - body `CreateResourceSchemaRequest` |
| removed + added | `GET /resources/types?groupId` - `Get resource types` | `GET /resources/schemas?groupId` - `Get resource schemas` |
| removed + added | `GET /resources/types/{id}` - `Get resource type` | `GET /resources/schemas/{id}` - `Get resource schema` |
| removed + added | `PUT /resources/types/{id}` - `Update resource type` - body `UpdateResourceTypeRequest` | `PUT /resources/schemas/{id}` - `Update resource schema` - body `UpdateResourceSchemaRequest` |
| removed + added | `DELETE /resources/types/{id}` - `Delete resource type` | `DELETE /resources/schemas/{id}` - `Delete resource schema` |
| parameter renamed | `GET /resources/instances?resourceTypeId&groupId&includeDeleted&afterId&take` | `GET /resources/instances?resourceSchemaId&groupId&includeDeleted&afterId&take` |
| field renamed | `CreateResourceInstanceRequest.resourceTypeId` | `CreateResourceInstanceRequest.resourceSchemaId` |
| field renamed | `ResourceInstanceRowDto.resourceTypeId` | `ResourceInstanceRowDto.resourceSchemaId` |

Unchanged: `POST /resources/instances` (address, operation name), `GET / PUT / DELETE /resources/instances/{id}`, every other module.

## Not visible in the description (covered by acceptance tests)

- `GET /resources/instances/{id}` body carries `resourceSchemaId` (no response schema in the swagger today).
- Error bodies: codes `resources_forbidden`, `resources_domain`, `RESOURCE_SCHEMA_NAME_TAKEN`; messages of the spec's Message list.
- `201` + `Location: /resources/schemas/{id}` on create (the swagger shows a bare `200`).

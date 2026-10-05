# Data Model: name map for the rename

No entity, field, relationship, validation rule or state transition is added or removed. This file is the single map old name -> new name. Sources: F-1, F-3, F-4, F-6; ASM-3, ASM-11.

## Database (schema `resources`) - changed by the migration (DEC-1)

| Kind | Today | After |
|---|---|---|
| table | `resource_types` | `resource_schemas` |
| column in `resource_instances` | `ResourceTypeId` | `ResourceSchemaId` |
| column in `resource_property_definitions` | `ResourceTypeId` | `ResourceSchemaId` |
| primary key | `PK_resource_types` | `PK_resource_schemas` |
| foreign key | `FK_resource_property_definitions_resource_types_ResourceTypeId` | `FK_resource_property_definitions_resource_schemas_ResourceSchemaId` |
| index | `IX_resource_property_definitions_ResourceTypeId` | `IX_resource_property_definitions_ResourceSchemaId` |
| unique index (filter `"DeletedAt" IS NULL` kept) | `IX_resource_types_GroupId_Name` | `IX_resource_schemas_GroupId_Name` |

Unchanged: `resource_instances`, `resource_property_definitions`, `resource_property_values`, `group_read_models`, `group_member_read_models` (table names), every other column, cascade rule, query filter, soft-delete columns. No sequences exist.

## Backend code - Step 0 (internal, invisible)

| Today | After |
|---|---|
| entity `ResourceType` (`Domain/ResourceType.cs`) | `ResourceSchema` |
| `ResourceInstance.ResourceTypeId`, `ResourcePropertyDefinition.ResourceTypeId` | `ResourceSchemaId` (pinned to the old column name until the skeleton phase) |
| `DbSet<ResourceType> ResourceTypes` | `DbSet<ResourceSchema> ResourceSchemas` |
| `ResourceTypeConfiguration` | `ResourceSchemaConfiguration` (table name pinned until the skeleton phase) |
| feature slices `CreateResourceType`, `UpdateResourceType`, `DeleteResourceType`, `GetResourceType`, `GetResourceTypes` (commands, queries, results, handlers, data providers, namespaces, folders) | `CreateResourceSchema`, `UpdateResourceSchema`, `DeleteResourceSchema`, `GetResourceSchema`, `GetResourceSchemas` |
| `TypeId` on `UpdateResourceTypeCommand`, `DeleteResourceTypeCommand`, `GetResourceTypeQuery` | `SchemaId` |
| `CreateResourceInstanceCommand.ResourceTypeId`, `GetResourceInstancesQuery.ResourceTypeId` | `ResourceSchemaId` |
| data-provider methods `GetResourceTypeAsync`, `GetResourceTypeWithDefinitionsAsync` | `GetResourceSchemaAsync`, `GetResourceSchemaWithDefinitionsAsync` |
| `ResourceTypeNameAlreadyExistsException` | `ResourceSchemaNameAlreadyExistsException` (code literal untouched in Step 0) |
| DI registrations in `Core/Extensions.cs`, `GroupDeletedHandler` usage, comments | follow the names above |
| test folder `ResourceTypes/`, test classes and methods, `ResourcesResourceTypeFactory`, `ResourcesTestClient` method names, `ManagementGroupsTestClient.ResourcesAllResourceTypesDeletedAsync` | `ResourceSchemas/`, `…ResourceSchema…` |

Kept in Step 0 (client-visible, changed later): request record names and properties in `.Api/Requests`, `ResourceInstanceRowDto.ResourceTypeId`, `GetResourceInstanceQueryResult.ResourceTypeId`, endpoint parameter `resourceTypeId`, route strings, operation names, messages, the 409 code, test-client URLs and JSON names, raw SQL table name.

## API contract - behaviour phase (visible)

| Today | After |
|---|---|
| `POST`, `GET /resources/types` | `POST`, `GET /resources/schemas` |
| `GET`, `PUT`, `DELETE /resources/types/{id}` | `GET`, `PUT`, `DELETE /resources/schemas/{id}` |
| `Location: /resources/types/{id}` | `Location: /resources/schemas/{id}` |
| operation names `Create / Get / Update / Delete resource type`, `Get resource types` | `… resource schema`, `Get resource schemas` |
| `CreateResourceTypeRequest`, `UpdateResourceTypeRequest` | `CreateResourceSchemaRequest`, `UpdateResourceSchemaRequest` |
| `resourceTypeId` in `CreateResourceInstanceRequest`, `ResourceInstanceRowDto`, `GetResourceInstanceQueryResult`, query of `GET /resources/instances` | `resourceSchemaId` |
| messages 1-6 of the spec's Message list | new wording |
| 409 code `RESOURCE_TYPE_NAME_TAKEN` | `RESOURCE_SCHEMA_NAME_TAKEN` |

Not renamed anywhere: `PropertyDataType`, `DataType`, `dataType`; `ResourceInstance` and "instance" naming; events `ResourceSchemaCreatedEvent`, `ResourceSchemaDeletedEvent`, `ResourceInstanceCreatedEvent`, `ResourceInstanceDeletedEvent`.

## Frontend

| Today | After |
|---|---|
| `ResourceTypeSummaryDto`, `ResourceTypeDetailDto` | `ResourceSchemaSummaryDto`, `ResourceSchemaDetailDto` |
| `getResourceTypes`, `getResourceType`, `createResourceType`, `updateResourceType`, `deleteResourceType` | `getResourceSchemas`, `getResourceSchema`, `createResourceSchema`, `updateResourceSchema`, `deleteResourceSchema` |
| URLs `/resources/types`, `/resources/types/{id}` | `/resources/schemas`, `/resources/schemas/{id}` |
| `resourceTypeId` (payload, row usage in store, modal, panel, spec) | `resourceSchemaId` |
| generated `api/Resources.ts`, `api/data-contracts.ts` | in line with the new swagger |
| texts: `Type`, `Create the first schema to define a resource type.`, `Add resource of type <name>` | `Schema`, `Create the first schema to describe your resources.`, `Add resource to schema <name>` |

Unchanged: routes (`groups/:groupId/schemas`, `schemas/new`, `schemas/:schemaId`), the store's `SchemaSummary` model, the sign-in slogan.

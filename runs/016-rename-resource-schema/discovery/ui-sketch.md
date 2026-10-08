# UI sketch - 016-rename-resource-schema

No new screen, element, state or action. The sketch lists the existing screens the story touches, what the owner sees change (three texts, DEC-4) and which address each action calls after the story. Sources: F-3, F-6; spec.md AC-8.

## Screen: group detail - Schemas panel (`groups/:groupId/schemas`)

| Element | States | Action -> endpoint (after) | Visible change |
|---|---|---|---|
| Schema list | loading / empty / list | on open -> `GET /resources/schemas?groupId={groupId}` | none |
| Empty-state sentence | empty | - | text: `Create the first schema to describe your resources.` (was `Create the first schema to define a resource type.`) |
| "Add schema" button | owner only, as today | navigate to `schemas/new` | none |
| "Open schema <name>" | per row | navigate to `schemas/:schemaId` | none |
| "Add resource" button of a schema | per row | opens the create-resource modal with the schema preselected | screen-reader label: `Add resource to schema <name>` (was `Add resource of type <name>`) |
| "Delete schema" | per row, owner only, as today | `DELETE /resources/schemas/{id}` | none |

## Screen: group detail - Resources panel

| Element | States | Action -> endpoint (after) | Visible change |
|---|---|---|---|
| Resources table | loading / empty (`No resources yet. Add the first resource to a schema.`) / rows | on open -> `GET /resources/instances?groupId={groupId}&afterId&take` | column header: `Schema` (was `Type`); the cell still shows the schema name, now matched by `resourceSchemaId` |
| Row "—" in the schema column | schema of the row not in the list | - | none |

## Modal: create resource

| Element | States | Action -> endpoint (after) | Visible change |
|---|---|---|---|
| Schema select (`Schema`, `— Pick a schema —`) | none selected (`Select a schema to fill in resource fields.`) / selected | - | none |
| Property fields of the selected schema | per data kind (Text / Number / Boolean) | - | none |
| Submit | saving / error toast (message from the API) | `POST /resources/instances` with `resourceSchemaId` | none |

## Screen: schema designer (`schemas/new`, `schemas/:schemaId`)

| Element | States | Action -> endpoint (after) | Visible change |
|---|---|---|---|
| Form (`Schema`, name, description, property definitions), live preview | `Loading schema…` / editing / client-side validation (`Schema name is required.`) | on open of an existing schema -> `GET /resources/schemas/{id}` | none |
| "Save schema" | saving / error toast | new: `POST /resources/schemas`; existing: `PUT /resources/schemas/{id}` | none; a taken name still shows the toast "A schema with this name already exists in the group." (the frontend does not read the error code) |

## Not changed

- Sign-in page slogan `One platform for every resource type.` (DEC-4: ordinary English, not the name of the thing).
- Routes, navigation, permissions shown in the UI, every other text.

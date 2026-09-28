---
title: "Resources: lifecycle events for schemas and resources (schema soft-delete)"
epic: "Availability epic (GitHub epic number not confirmed by owner; issue #10 refers to its decisions no. 2 and no. 3)"
why: "As a group owner I want the Resources module to inform the rest of the system about creation and deletion of schemas and resources, so that the Availability module can maintain its own read models and per-schema rules without cross-module queries. (Rescoped in discovery round 1, DEC-3: the schema buffer BufferMinutes is owned by Availability as a per-schema rule and leaves this story.)"
source_issue: "#10 - [BE] Resources: BufferMinutes w ResourceType (encja, komendy, requesty, GET, migracja) - title to be updated by the business layer after DEC-3"
acceptance_criteria:
  - id: AC-3
    given: "a resource schema is created"
    when: "SaveChangesAsync has completed"
    then: "ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId) is published via IMessageBroker"
  - id: AC-4
    given: "a resource instance is created"
    when: "SaveChangesAsync has completed"
    then: "ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId) is published via IMessageBroker"
  - id: AC-5
    given: "a resource schema is deleted through the delete command"
    when: "SaveChangesAsync has completed"
    then: "exactly one ResourceSchemaDeletedEvent(Guid SchemaId, Guid GroupId) is published; no per-instance events are published for the instances soft-deleted as a side effect (epic decision no. 2: the consumer hard-deletes its rules for that SchemaId)"
  - id: AC-6
    given: "a resource instance is deleted through the delete command"
    when: "SaveChangesAsync has completed"
    then: "ResourceInstanceDeletedEvent(Guid ResourceId, Guid SchemaId) is published"
  - id: AC-7
    given: "the new event contracts"
    when: "they are added to the solution"
    then: "they live in Shared.Abstractions under Events/Resources/ and are the only coupling between Resources and Availability"
  - id: AC-8
    given: "a resource schema with instances exists"
    when: "the group owner deletes it through DELETE /resources/types/{id}"
    then: "the response is 204; the schema row remains in the database with DeletedAt set (visible only with IgnoreQueryFilters); GET /resources/types/{id} returns 404 and the list no longer contains it; its instances are soft-deleted as today"
    role: owner
  - id: AC-9
    given: "a resource schema named X was soft-deleted in group G"
    when: "the group owner creates a new schema named X in group G"
    then: "the response is 201 Created (the unique name constraint applies only to non-deleted schemas)"
    role: owner
out_of_scope:
  - "BufferMinutes on ResourceType - owned by Availability as a per-schema rule (DEC-3, discovery round 1); removed AC-1 and AC-2"
  - "Per-schema events when a whole group is deleted (GroupDeletedHandler bulk-deletes types via ExecuteDeleteAsync); Availability cleans up on GroupDeleted instead"
  - "Availability module read models and handlers consuming the new events"
  - "Frontend changes ([BE] issue)"
rejected_alternatives:
  - option: "Availability queries Resources via IModuleClient for schema data"
    reason: "Story goal is explicitly to let Availability keep its own read models without cross-module queries"
  - option: "BufferMinutes stored on ResourceType in Resources and replicated to Availability through Created/Updated events"
    reason: "DEC-3: the buffer is a scheduling rule with a single consumer; two sources of truth plus a permanent sync mechanism for a value Resources never uses"
  - option: "One ResourceInstanceDeletedEvent per instance when a schema is deleted"
    reason: "DEC-4 (pending owner confirmation): consumer cascades by SchemaId, as the epic rule already assumes; keeps the bulk soft-delete path untouched"
blast_radius:
  modules: ["Resources", "Shared.Abstractions"]
  touches_contract: false
  touches_authz: false
  touches_schema: true
  touches_ui: false
depends_on: []
capability_map_version: "ef9341f9bd76df83af914e1bda2ba6c4d20f4cad1698fe971aae1bccb854048c"
open_from_epic:
  - "Epic open decision no. 3 - CLOSED in this story by DEC-3: Availability owns the schema buffer"
discovery_changes:
  - "round 1 / DEC-3: removed AC-1 (BufferMinutes validation) and AC-2 (BufferMinutes in GET); AC-3 payload reduced to (SchemaId, GroupId); AC-5 payload fixed to (SchemaId, GroupId) with the no-per-instance-events clause; AC-6 payload fixed to (ResourceId, SchemaId)"
  - "round 1 / DEC-5: added AC-8 (schema soft delete) and AC-9 (name reusable after soft delete; partial unique index); touches_schema = true (one migration: index only)"
---

# Story - issue #10 (verbatim, owner-provided, Polish) - ORIGINAL TEXT BEFORE RESCOPE

Jako wlasciciel grupy chce, aby schemat zasobu mial domyslny bufor (BufferMinutes), a modul Resources informowal reszte systemu o tworzeniu i usuwaniu schematow oraz zasobow, aby modul Availability mogl utrzymywac wlasne read-modele bez zapytan miedzy modulami.

## Kryteria akceptacji (original)
- ResourceType ma pole BufferMinutes (int, domyslnie 0, walidacja >= 0 z rozsadnym gornym limitem); pole jest w create/update request, w komendach i w wynikach GET. **[REMOVED by DEC-3]**
- Po utworzeniu schematu publikowane jest ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId, int BufferMinutes). **[payload changed by DEC-3: no BufferMinutes]**
- Po utworzeniu zasobu publikowane jest ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId).
- Usuniecie schematu i usuniecie zasobu publikuje odpowiednie zdarzenia *Deleted (wymagane przez regule "usuniecie schematu usuwa twardo jego reguly"; patrz decyzja nr 2 w epiku).
- Zdarzenia sa publikowane po SaveChangesAsync (konwencja z ManagementGroups).
- Kontrakty zdarzen leza w Shared.Abstractions/Events/Resources/.

## Kontekst z analizy (claims from the issue - verified in discovery)
- Resources dzis nic nie publikuje; CreateResourceTypeCommandHandler i CreateResourceInstanceCommandHandler przyjmuja tylko (IDataProvider, IClock) - trzeba wstrzyknac IMessageBroker. **[confirmed F-1, F-3, F-6]**
- DeleteResourceTypeCommandHandler soft-deletuje instancje i hard-deletuje typ. **[confirmed F-4]**
- GroupDeletedHandler kasuje typy hurtowo przez ExecuteDeleteAsync - tam zdarzen per schemat nie bedzie; sprzatanie po usunieciu grupy realizuje Availability na podstawie GroupDeleted. **[confirmed F-4]**
- Otwarta decyzja nr 3 z epika: zrodlo prawdy dla bufora schematu (Resources vs Availability). **[closed by DEC-3: Availability]**

## Owner rule stated in discovery round 1
- "Everything that is deleted must be a soft delete" - see DEC-5.

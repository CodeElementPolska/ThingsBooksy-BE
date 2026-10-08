# Odłożony temat: SonarQube i statyczna analiza jako część substratu floty agentów

**Status:** odłożone (2026-09-23) — do przypomnienia na końcu redesignu floty agentów (branch `ai/014-agents-redesign`).
**Forma docelowa:** epik + user stories w GitHub Project.

## Kontekst

Podczas projektowania nowej floty agentów ustalono, że bramki jakości muszą opierać się na
warstwie deterministycznej („substrat" — skrypty bez LLM), a nie na ocenie LLM przez LLM.
Pojawiło się pytanie, czy SonarQube pokrywa ten substrat.

## Wniosek z analizy

SonarQube Community Build jest darmowy (self-hosted), analizuje C# i TypeScript, ale:

- w Community analizuje tylko gałąź główną (analiza gałęzi feature i dekoracja PR to edycja płatna),
- nie ma reguł zależności między projektami .NET ani łatwych własnych reguł C#,
- pokrywa wyłącznie wymiar „bugi / smells / duplikacje / security hotspots" — czyli jeden
  z ~7 elementów substratu.

Nie zastępuje: mapy możliwości, kontraktu-jako-różnicy, dowodu czerwonych testów,
macierzy kryterium→test, bramki Definition of Done, ACL ścieżek dla agentów.

## Proponowany epik: „Statyczna analiza i reguły projektowe egzekwowane przez narzędzia"

| Story | Narzędzie | Koszt | Co zastępuje w flocie |
|---|---|---|---|
| Zakaz `Guid.NewGuid()` jako błąd kompilacji | `Microsoft.CodeAnalysis.BannedApiAnalyzers` + `BannedSymbols.txt` | ~1 h | ręczne sprawdzanie przez reviewera |
| Testy architektury: brak referencji między modułami, schemat EF ≠ `public`, `InternalsVisibleTo` | NetArchTest / ArchUnitNET (xUnit) | ~0.5 dnia | część `architecture-guard` |
| Reguły stylu jako błędy builda | `.editorconfig` + `dotnet format --verify-no-changes`, ESLint w CI | ~2 h | wymiar „styl" w code review |
| SonarQube Community w Docker Compose + Quality Gate na `main` | SonarQube + sonar-scanner dla .NET i JS | ~1 dzień | wymiar „utrzymanie / security hotspots" w code review (po merge) |
| Tagowanie konwencji `enforced-by: analyzer | archtest | lint | llm-review` | edycja `.claude/conventions/*.md` | ~2 h | reviewer LLM dostaje tylko reguły `llm-review` |
| Kolejność „`SaveChangesAsync` przed `PublishAsync`" w handlerach komend (konstytucja IV) — kandydat z C5 story 015 (finding `review-spec-conformance-1-2`, decyzja DEC-7: właściciel świadomie nie chce testów kolejności; po wdrożeniu Inbox/Outbox reguła może stać się zbędna) | analizator Roslyn (przepływ w metodzie) albo test architektury na podstawie kolejności wywołań w `HandleAsync` | ~0.5 dnia | słaba asercja w testach akceptacyjnych AC-3…AC-6 |
| Każde zdarzenie `IEvent` w `Shared.Abstractions.Events.*` ma ≥ 1 `IEventHandler<T>` w jakimś module ALBO jest na allow-liście zdarzeń czekających na przyszły moduł (z id story) — kandydat `architecture-guard-1-1` (015); allow-lista startowa: `ResourceSchemaCreatedEvent`, `ResourceSchemaDeletedEvent`, `ResourceInstanceCreatedEvent`, `ResourceInstanceDeletedEvent` (konsument = przyszły moduł Availability, DEC-8) | test architektury (refleksja po assembly modułów) | ~2 h | ręczne sprawdzanie sierot przez guarda |
| Klauzule AC-7 story 015 „jedyne sprzężenie Resources↔Availability" i „pliki w `Events/Resources/`" — DEC-9: właściciel przyjął, że to testy architektury, nie testy story (brak referencji między modułami; spójność folder↔namespace w `Shared.Abstractions`) | NetArchTest + prosty test folder/namespace | w ramach wiersza „Testy architektury" | `trace-auditor-1-1/1-2` |
| Wykrywanie metod-aliasów dodanych w diffie (ciało = jedno wywołanie metody-rodzeństwa w tej samej klasie) — `rule_candidate` z `review-maintainability-1-1` (015) | skrypt w `tools/fleet` po diffie albo analizator | ~2 h | uwaga OPINION reviewera konwencji |
| Testy integracyjne poza `Clients/` nie zawierają surowych ścieżek `/<moduł>/…` przekazywanych do `HttpClient` (GetAsync/PostAsJsonAsync/PutAsJsonAsync/DeleteAsync); allow-lista: asercje na dokumencie OpenAPI — `rule_candidate` z `review-maintainability-1-1` (016; zmiana trasy w tej story wymusiła edycję 6 testów 401) | skrypt w `tools/fleet` po diffie albo analizator | ~2 h | konwencja `integration-test-infrastructure.md` sprawdzana przez reviewera |
| Wyszukiwanie starej nazwy po rename (FR-009/FR-011 story 016) jako skrypt z jawną allow-listą plików, które muszą ją wymieniać (migracja rename, testy negatywne), zamiast wymuszania konkatenowanych literałów w testach — `rule_candidate` z `review-maintainability-1-4` (016) | skrypt `tools/fleet` (grep + allow-lista) | ~1 h | ręczne reguły T036 dyrygenta; 9 miejsc z `"resource" + "Type"` w testach |
| Klient TS `frontend/src/app/api/` zawsze równy wynikowi `swagger-typescript-api --modular` nad `generated/swagger.base.json` — w CI regeneracja i porównanie; dryf ląduje we własnym commicie — `rule_candidate` z `review-maintainability-1-8` (016; pliki były nieaktualne 2 tygodnie przed story, DEC-8) | skrypt npm `api:generate` + krok gate/CI porównujący | ~2 h | ręczna regeneracja przez dyrygenta w T032 |
| Projekt `<Moduł>.IntegrationTests` nie zawiera literałów SQL nazywających schemat innego modułu (np. `FROM resources.` w `ManagementGroups.IntegrationTests`) — `rule_candidate` z `architecture-guard-1-1` (016); dziś test kaskady GroupDeleted czyta `resources.resource_schemas` surowym SQL i każda zmiana fizyczna w Resources psuje testy ManagementGroups w runtime | test architektury (skan źródeł testów) | ~2 h | uwaga OPINION guarda; decyzja właściciela, czy asercje kaskady przenieść do `Resources.IntegrationTests` lub projektu e2e |
| Komunikaty widoczne dla użytkownika w `Resources.Core` (wyjątki, mapper błędów, `ResourcesDomainException("…")`) nie zawierają „resource type” — `rule_candidate` z `trace-auditor-1-1` (016); DEC-9: klauzula AC-7 dowodzona NOTE T036 dyrygenta, nie testem | skan literałów komunikatów (skrypt) albo konwencja „komunikaty jako stałe” + test | ~2 h | NOTE T036 w journalu story |
| Testy Access (016) — `MemberAccessBaselineTests`/`OutsiderAccessBaselineTests`/`ForeignSchemaFilterTests` seedują identyczny kształt danych osobno; `review-maintainability-1-5` proponuje wspólny helper w `Clients/` i scalenie zduplikowanych testów 409/„missing id” (jeden test na parę AC × zachowanie). Spec serwisu FE `resources-api.service.spec.ts` lokalizuje klasę i metody refleksją (`review-maintainability-1-7`) — po rename zbędne; wstrzyknąć `ResourcesApiService` i wywołać typowo | refaktor testów przy następnej story Resources | ~2 h | — (OPINION, nie reguła) |

## Decyzje do podjęcia przy realizacji

- Czy Quality Gate Sonara ma blokować merge (wymaga CI), czy być tylko raportem.
- Czy warto rozważyć płatną edycję dla analizy gałęzi feature (raczej nie przy solo-dev).

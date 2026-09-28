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
| Wykrywanie metod-aliasów dodanych w diffie (ciało = jedno wywołanie metody-rodzeństwa w tej samej klasie) — `rule_candidate` z `review-maintainability-1-1` (015) | skrypt w `tools/fleet` po diffie albo analizator | ~2 h | uwaga OPINION reviewera konwencji |

## Decyzje do podjęcia przy realizacji

- Czy Quality Gate Sonara ma blokować merge (wymaga CI), czy być tylko raportem.
- Czy warto rozważyć płatną edycję dla analizy gałęzi feature (raczej nie przy solo-dev).

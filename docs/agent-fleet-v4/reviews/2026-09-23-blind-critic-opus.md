# Ślepa recenzja techniczna `workflow.md` DRAFT v1 (Opus, 2026-09-23)

Recenzent dostał WYŁĄCZNIE `workflow.md` + minimalny kontekst (stack, solo dev, Windows, SpecKit, mechanizmy Claude Code).
Nie widział `decisions.md`, audytu 013 ani wcześniejszych recenzji. Właściciel: „nie biorę tego zdania za bardzo na serio, chcę kontekst osoby bez naszych ustaleń".
Oznaczenia recenzenta: [V] zweryfikowane w dokumentacji Claude Code, [?] niezweryfikowane.

## 1. Werdykt
Nie wdrożyłby v1: dokument opisuje okablowanie, a pomija substrat (10 rodzin skryptów bez kontraktów we/wy; analizatory i arch-testy odłożone → w dniu 1 „o poprawności rozstrzyga skrypt" jest fikcją). Największe ryzyko: **C2 — tester ślepy na `src/**`, bez powłoki, ma wyprodukować kompilujące się czerwone testy .NET** (brak limitu pętli, dokument sam przyznaje, że nie zna rozwiązania). Drugie: koszt utrzymania 3 sesje × 6 faz × ~24 agentów dla jednego developera.

## 2. Symulacja story „anulowanie rezerwacji do 24h; admin zawsze; powiadomienie" — luki
- §1.1: `capability-map` — nikt nie definiuje, kto/kiedy regeneruje `generated/*`.
- §1.1: `capability-analyst` przepisuje deterministyczny JSON na JSON — zero wartości.
- §1.1 vs §2/§3: `proposal.md` nie istnieje w artefaktach; sprzeczność.
- Krytycy: „inny model/effort" bez podania jakiego.
- G1: `backlog-writer` bez klucza idempotencji (awaria MCP po Epic, przed Story → duplikat).
- **`<story>` nigdzie niezdefiniowane** (numer issue? slug? branch?) — blokuje §3, §4, S7.
- §2: `dev-analyst` ma skille SpecKit, ale bez powłoki — skille odpalają PowerShell i tworzą branche.
- §1.2: „pętla bez limitu rund" sprzeczna z §0.6.
- §0.5: D-1/D-2 są w `decisions.md` — dokument nie jest samowystarczalny.
- **Brak kontraktu BE↔BE** (zdarzenia `IEvent`, `IModuleClient`, `Shared.Abstractions`) — `contract-delta` pokrywa tylko HTTP; `Shared.Abstractions` nie ma właściciela w ACL.
- `/speckit-clarify` jako lint zawsze wygeneruje pytania → pętla bez końca.
- G2: paczka 1500–3000 linii naraz → właściciel przeczyta 10%.
- [V] **`UserPromptSubmit` NIE łapie odpowiedzi na `AskUserQuestion`** → `owner-answers.jsonl` bez źródła; potrzebny hook `Elicitation`/`ElicitationResult`.
- [V] **wznawianie workflow tylko w tej samej sesji** — crash CLI w środku fazy = utrata przebiegu; potrzebny własny dziennik faz.
- C1: `arch-tests` puste (odłożony epik) → zawsze zielone; „limit rozmiaru story" bez progu; brak drogi powrotnej C → B.
- C2: tester bez kompilatora; brak szwu czasu (`TimeProvider`) → `DateTime.UtcNow` niedeterministyczne; subagent nie zapyta.
- C2/C4: **`dotnet format` w gate i Husky przy commicie łamią hash `tests/acceptance`**.
- C3: `Shared.Abstractions` bez właściciela → deadlock lub dwie definicje zdarzenia.
- C3: klient TS generowany z czego? `swagger.base` (bez nowych endpointów) czy `contract-delta` (diff, nie pełna spec) → potrzebny `contract-next.json = base ⊕ delta`; realnie FE czeka na BE.
- [V] allowlista Bash to dopasowanie tekstowe; `dotnet test`/`dotnet format` z powłoki writera omijają ACL na zapis do `tests/acceptance`.
- Migracja: brak polityki „druga vs regeneracja", rollbacku, kto podnosi Postgresa.
- C4: brak atrybucji porażki do writera przy 3 równoległych; **brak limitu rund C4**.
- Swagger „z builda" bez uruchomienia aplikacji wymaga toolingu (Swashbuckle CLI / `Microsoft.Extensions.ApiDescription.Server`) — S2/S3 na tym stoją.
- C5: po zmianie zachowania nowy test nie będzie czerwony na bazie; brak protokołu re-baseline hasha.
- **Brak ścieżki na błędny test** (nie kod) → twardy deadlock.
- C5: arbiter-LLM, gdy właściciel siedzi przy klawiaturze.
- C6: `trace-auditor` bez władzy (co po „AC-2 słabo asertowane"?); ESCALATE z guarda ląduje w paczce ze zgodą na commit.
- G3: gdzie żyje kod przez C1–C6 (worktree nieużyty); brak odrzucenia części wyniku; brak procedury wycofania story.

## 3. Tryby awarii nieadresowane
Awaria w połowie fazy (brak cross-session resume); provenance vs restart (kaskada unieważnień po każdym `format`); przepełniony kontekst writera; schemat po 5 retry z tym samym promptem; konflikt hasha z formatem; błędny test; migracja/rollback (`deny` po argumentach Bash zawodne); dwie story pod rząd (`swagger.base` bez niezmergowanej story-1 → fałszywy diff); zmiana konwencji bez wersjonowania; brak preflightu Postgres/Docker; **Windows case-insensitive → deny po ścieżce tekstowej obchodzi się wielkością liter, 8.3, junction** (potrzebna kanonizacja); MAX_PATH; wzorce jednosegmentowe `Edit(src/**)` vs `**/src/**`; **koszt 1–3 mln tokenów/story bez bezpiecznika**.

## 4. Co wyciąć (wg recenzenta)
`capability-analyst` (mapa jest deterministyczna); jeden krytyk zamiast dwóch; scalić `impact-analyst` z `code-researcher`; scalić `plan-guard` z `architecture-guard`; wyciąć `review-arbiter` (spór → właściciel); 2 reviewerów zamiast 3 (bez `maintainability`); `trace-auditor` wyciąć lub przesunąć przed G2; scalić `rule-harvester`+`docs-delta` w `closer`; punktację założeń uprościć do `hard_list | reszta`; scalić sesje A i B; wyciąć `tasks.md` z toru; tory zostawić, ale zdefiniować progi.
Zostać musi: ACL tester↔writer, red-first, `ac-matrix`, `contract-diff`, `gate`, pusty ZAPYTAJ przed bramką, `decided_by: owner` tylko ze skryptu.

## 5. Braki blokujące implementację
Definicja `<story>`; strategia branchy/worktree; schematy JSON (tylko `findings` naszkicowany); kontrakt faza↔faza i kody wyjścia; zachowanie dyrygenta przy DISPUTE / BLOCKED_ON_DECISION / ESCALATE / błąd schematu / crash / timeout; definicja BLOCKER/MAJOR/MINOR i „blockery maleją"; zawartość paczki G2; mechanika zapisu odpowiedzi (`ElicitationResult`); umiejscowienie hooka ACL (frontmatter vs `settings.json` z rozgałęzieniem po `agent_type`) — rozstrzygnąć prototypem; kontrakt BE↔BE; jak powstaje `swagger.base.json`; D-4 fabryki; re-baseline hasha; ścieżka naprawy błędnego testu; limity rund C2/C4, cap na `clarify`, budżet tokenów z przerwaniem; preflight środowiska; model/effort per agent.

## 6. Kontrpropozycja recenzenta (7 agentów)
`/story` (persona analyst + researcher ×N + premortem) → G1 → skrypt: issue, worktree, `runs/<issue>/`, `contract-next.json = base ⊕ delta`.
`/build`: 1) tester (ślepy na src, ale `dotnet build` tylko tests/) → red-first → **G2 = lista testów + założenia + pytania** (nie spec+plan+tasks); 2) writer ×moduł + klient TS z `contract-next` + migracja (destrukcyjna → STOP) + gate, cap 3; 3) jeden reviewer spec+authz, cap 2, spór → właściciel; zmiana zachowania → powrót do 1 z jawnym re-baseline; 4) skrypt regresja/DoD/metryki + closer (ADR, capability-map, rule-candidates) → G3.
Różnice: 2 sesje, tester z kompilatorem, bramka na testach, pełny OpenAPI zamiast diffu, bez `tasks.md`, bez arbitra, worktree per story, dziennik faz w plikach, hook ACL w `settings.json` po `agent_type`, twarde capy i budżet.

## 7. Pytania recenzenta do właściciela
1. Czym jest `<story>` i gdzie żyje kod między G1 a G3?
2. Jak powstaje `swagger.base.json` bez uruchomionego backendu i kto go regeneruje?
3. Kto definiuje kontrakt zdarzeń i `IModuleClient` między modułami?
4. Co robi dyrygent przy DISPUTE / BLOCKED_ON_DECISION / błędzie schematu / crashu; limity rund C2 i C4?
5. Budżet tokenów/czasu na story i próg rezygnacji z fazy?

---
**Stanowisko orkiestratora (Fable):** lista luk w większości trafna; redukcja do 7 agentów sprzeczna z preferencją właściciela (specjalizacja zostaje). Przyjęte od razu: tester z kompilatorem (D-4c), `DISPUTE_TEST`, hash po formacie, limit rund C4, prototyp hooka ACL w obu wariantach, kanonizacja ścieżek. Do rozstrzygnięcia w kolejnych krokach: `<story>` id + worktree, kontrakt BE↔BE, `contract-next.json`, `ElicitationResult`, dziennik faz zamiast resume, budżet tokenów, zawartość paczki G2.

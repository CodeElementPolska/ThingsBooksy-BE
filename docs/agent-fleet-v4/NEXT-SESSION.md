# NEXT-SESSION — przekazanie pracy floty z sesji 2026-09-30 → 2026-10-02

Ten plik jest punktem wejścia dla NOWEJ sesji. Czytać w kolejności: ten plik → `handoff-2026-10-02/critiques.md` → `handoff-2026-10-02/strategic-review-opus.md` i `strategic-review-fable.md` → dopiero potem `HANDOFF.md` (uwaga: ma ≥8 nieaktualnych zdań, lista niżej) i `tools/fleet/README.md` (dziennik defektów).

## Jak uruchomić
PowerShell, katalog repo, gałąź `016-rename-resource-schema`, zwykła sesja bez persony:
```
claude
```
Pierwsza wiadomość: „Przeczytaj `docs/agent-fleet-v4/NEXT-SESSION.md` i wykonaj sekcję »Pierwsze kroki« — każdy krok dopiero po mojej zgodzie na jego plan.”
Dlaczego zwykła sesja: żadna persona nie ma narzędzi do budowy agentów i skryptów floty; rejestr agentów ładuje nowe pliki `.claude/agents/*.md` dopiero przy następnej wiadomości (HANDOFF, nawyk 1).

## Stan repo (2026-10-02)
- `main` = PR #59 (story 015 zmergowana). Gałąź 016 = `main` + sesja A 016 (G1 PASSED 2026-09-29, issue #57, epik #56) + `247acc0 fix(fleet)` (feature.json usunięty, prompt-builder fail-closed, test szablony→ACL).
- **NIESKOMITOWANE w drzewie (czekają na „tak” właściciela):**
  - `feat(fleet): migration-check (S9b)` — `tools/fleet/migration-check.js` + `migration-check.test.js` (63 testy) oraz hunki w `gate.js`, `status.js`, `decide.js`, `package.json`, `owner-answer-hook.js`, `README.md`, `docs/agent-fleet-v4/{workflow.md,runbook-delivery.md,decisions.md,schemas/journal-event.schema.json}`. Plan: `handoff-2026-10-02/plan-migration-check-v2.md`. Przeszedł 2 rundy krytyków; zweryfikowany end-to-end na syntetycznej story.
  - `docs/agent-fleet-v4/handoff-2026-10-02/` + ten plik — pakiet przekazania.
  - `.claude/settings.json` — **niezwiązana zmiana (`enabledPlugins`), NIE commitować z flotą.**
- Story 016: `state.json` phase B RUNNING, ale discovery stoi od 2026-09-30 13:55 (tylko `discovery/questions/1-4.md`, brak faktów, brak `specs/016-*`). Trzeba ustalić, czy sesja B żyje; jeśli nie — wznowić (`claude --agent dev-analyst` na gałęzi 016).

## Decyzje właściciela z tej sesji (obowiązują)
- Pętla pracy (zapisana w pamięci sesji właściciela): plan z argumentacją → ≥2 krytyków → streszczenie → implementacja → nowi krytycy → streszczenie; commity tylko po „tak”. **Obaj recenzenci strategiczni zalecają skalowanie tej pętli według ryzyka** (hook ACL, bramki: tak; dokumentacja, prompty, zmiany < ~100 linii: nie) — decyzja właściciela, do potwierdzenia na starcie.
- A) `fe-writer` — budować (pierwszy nowy agent). B) kolejność: fail-closed ACL → fe-writer + kroki FE w gate → spec-critic → `/deliver`; `*.Tests.Unit` = zwykła story przez sesję A. C) `migration-check` zamiast agenta `migration-reviewer` (zrobione). C1) spec-critic zamiast plan-guard (brief w `critiques.md` §2). A1) REVIEW z migration-check też otwiera G2b (D-1). B) sprostowanie D-4a (migrację generuje właściciel między C3a a C2). E) osobne commity.
- **Recenzje strategiczne (Opus + Fable, zgodne) proponują korektę kolejności i zakresu** — patrz „Pierwsze kroki”; właściciel ma to potwierdzić lub odrzucić na starcie nowej sesji.

## Pierwsze kroki (propozycja z obu recenzji; każdy krok = plan → zgoda → wykonanie)
1. **Porządek (bez pętli krytyków):** za zgodą zacommitować migration-check (bez `.claude/settings.json`) i pakiet `handoff-2026-10-02/` + ten plik; przepisać `HANDOFF.md` do ~15 linii samego stanu (historia → dziennik w README). Lista nieaktualnych zdań HANDOFF: `:1` data 09-28; `:6` gałęzie `ai/014` i cherry-pick (wszystko zmergowane); `:8` „~25 wpisów” (jest 39) i „odłożone” docs-delta/rule-harvester (skreślone); `:9` „sesja A nieprzetestowana” (G1 016 przeszło 09-29); `:10` start z `015-…` (016 wyrosła z `main`), „Persona przydzieli 016-” (następna to 017); `:12` lista agentów, które właściciel skreślił; `:13` „Nie istnieje jeszcze: sesja A, C5, C6” (istnieją); `:34-38` materiał do projektowania C5/C6 (zrobione); nawyk 7 „`git status -- backend`” (od FE także `-- frontend`).
2. **Dowieźć 016 do G2.** Zero pracy nad flotą, dopóki nie ma `specs/016-rename-resource-schema/`. Przed C2 rozstrzygnąć z właścicielem: (i) kto zmienia ~10 plików frontendu dla 016 (regeneracja klienta `swagger-typescript-api` z nowego swaggera po BE + rename w `features/groups|schemas` + 1 `.spec.ts`): dyrygent ręcznie z jednorazowym REBASELINE (Opus) albo „fe-writer v0” = plik agenta + 1 szablon + wpis ACL + kroki `fe-build`/`fe-test` w gate (Fable); (ii) defekt gate: `test-hash` obejmuje wszystkie 39 istniejących `.spec.ts`, więc każda edycja mocka = gate RED bez REBASELINE — rozstrzygnąć przed C2, nie „na etapie planu fe-writera”; (iii) fakty do ustalenia w 10 minut: komenda generatora klienta (skrypt `gen-api-client` nie istnieje), `npm run lint` nie istnieje (poprawić CLAUDE.md), `red-first-prover --fe` nigdy nie uruchomiony (testy FE idą przez `ng test`; sprawdzić filtr `[AC-n]` na żywo).
3. **G2b na 016:** migration-check da REVIEW/DESTRUCTIVE (rename). Właściciel już zdecydował w AC-9, że dane deweloperskie można wymazać — G2b zamyka się JEDNYM AskUserQuestion nazywającym migrację (sha8) i `decide.js --gate G2b` (zob. runbook, wiersz „migracja”). Nie rozbudowywać migration-check (Testcontainers, analiza `Sql`).
4. **Po G3 016, przed sesją B 017:** fail-closed ACL (zmiana D-13; `base_read_allow`, `unrestricted` dla person, hook fail-closed dla agentów floty bez wpisu, parzystość z `prompt-builder`) — w worktree, gdy ŻADNA sesja story nie działa (hook czytany przy każdym wywołaniu narzędzia przez wszystkie sesje; zapis w trakcie edycji = nieczytelny `fleet-acl.json` = odmowa dla wszystkich). Jeden krytyk (kod bezpieczeństwa). Szczegóły wymagań: `handoff-2026-10-02/plan-D16-scope-v2.md` §7 Część 1 + poprawki z `critiques.md` §4.
5. **Spec-critic: dopiero po pomiarze.** Najpierw `closer.js` liczy decyzje po fazie i źródle (`decisions.jsonl` ma `phase`, `asked_by`, `finding_id`) na 016 (i 017); D-16 bez progów liczbowych („oceniamy po 017”). Budowa fe-writera „pełnego” i spec-critica — na pierwszej story z prawdziwym nowym UI / po danych z 016.
6. **D-16 w `decisions.md`:** krótko (skreślenia + kolejność + „kryteria = wynik closer.js”), bez 7-plikowej propagacji; tabela agentów w `workflow.md`: usunąć wiersze plan-guard/migration-reviewer/docs-delta/rule-harvester, `contract-author` → „wchłonięty przez dev-analyst, do potwierdzenia na 016”. Anchory i pominięte miejsca: `critiques.md` §4.

## Zasady dla nowej sesji
- Nie dotykać `runs/016-*/` poza odczytem; nie uruchamiać `dotnet ef database update`; `git add` tylko wskazanych ścieżek; nie commitować `.claude/settings.json`.
- Nie zmieniać hooka ACL ani `fleet-acl.json`, gdy działa inna sesja Claude na tym drzewie.
- Pliki agentów i szablony po angielsku (jak reszta floty); dokumentacja po polsku.
- Wyniki agentów: `runs/<story>/impl/<agent>.<faza>[-<instancja>]/result.json`; prompty przez `prompt-builder` (ścieżka, nie treść; nawyk 2); odpowiedzi właściciela tylko przez AskUserQuestion (hook projektowy).

## Pakiet
`handoff-2026-10-02/`: `critiques.md` (wszystkie krytyki, skondensowane), `strategic-review-opus.md`, `strategic-review-fable.md`, `strategic-context.md` (kontekst dla recenzentów), `plan-D16-scope-v2.md` (plan dokumentacji + prompt fe-writer, wymaga v3), `plan-migration-check-v2.md`, `review-2026-09-30-acl-feature-json.md`.

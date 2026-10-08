# HANDOFF — flota v4, stan na 2026-10-08

Sam stan, bez historii. Historia i defekty: dziennik w `tools/fleet/README.md`. Projekt: `workflow.md`; decyzje właściciela: `decisions.md`; dostawa: `runbook-delivery.md`. Następne kroki: `NEXT-SESSION.md`, sekcja „Pierwsze kroki” (2–6).

## Gdzie jesteśmy
- **Gałęzie:** `main` = PR #59 (story 015 zmergowana). `016-rename-resource-schema` = `main` + sesja A/B 016 + commity floty + 7 commitów dostawy (`c3350bc` krok 0 … `37174b1` review/zamknięcie + commit zamykający); gałąź gotowa do PR do `main` (sprawdź upstream przed pushem).
- **Story 015:** zamknięta 2026-09-28, DoD 10/10, G3 PASSED — `runs/015-resource-time-buffer/{close-report.md,metrics.json}`.
- **Story 016:** ZAMKNIĘTA — G3 PASSED 2026-10-08, DoD 10/10, gate GREEN (204 BE + 243 FE testów, 84 otagowane AC, 11/11 AC), 0 UNSPECIFIED, 11 decyzji właściciela (DEC-6…DEC-11 w sesji C), 2 REBASELINE, 15 przebiegów agentów, 1,72 M tokenów — `runs/016-rename-resource-schema/{close-report.md,metrics.json}`; komentarz z checklistą na issue #57. Przebieg sesji C i defekty: dziennik w `tools/fleet/README.md` (wiersze 2026-10-07/08).
- **Etykiety AC:** testy zamkniętej story 015 mają `[Trait("AC", "015/AC-n")]`, żeby filtr `AC=AC-n` widział tylko bieżącą story. Etykiety 016 (`AC-1`, `AC-2`, `AC-6`…`AC-14`, 84 testy BE + specy FE `[AC-8]`) trzeba przemianować na `016/AC-n` TERAZ, przed C2 story 017 (reguła trwała; po zmianie `red-first.json` 016 jest historyczny, nie przeliczać).
- **Przeszło na prawdziwej story:** sesja A (016), sesja B (015, 016), sesja C fazy C1–C6 (015, 016 — w 016 także `fe-writer` v0, `migration-check` + G2b, dwa przebiegi `test-designer-fix` w C6 z REBASELINE, `rehash-acceptance.mjs`). Agenci i skrypty ze statusami: tabele w `tools/fleet/README.md`.
- **Zbudowane, niesprawdzone na prawdziwej story:** `review-arbiter` (w 015 i 016 nie było sporu).
- **Nie istnieje:** `write_deny` w hooku ACL (`fe-writer` może technicznie zapisać `*.spec.ts`; pilnuje prompt + `test-hash`), `spec-critic`, skill `/deliver` (dyrygent = sesja główna ręcznie), projekty `*.Tests.Unit`, skrypt generatora klienta TS (`swagger-typescript-api` jest w devDeps, skryptu brak), `npm run lint` we frontendzie.
- **Otwarta luka:** agent z plikiem w `.claude/agents/`, który nie jest personą i nie ma wpisu w `fleet-acl.json`, działa w hooku bez ograniczeń (pilnuje go tylko `prompt-builder`). Fail-closed = zmiana D-13, zaplanowana po G3 016, przed sesją B 017.
- **Skreślone przez właściciela:** `plan-guard` (w jego miejsce spec-critic), `migration-reviewer` (w jego miejsce `migration-check`), `docs-delta`, `rule-harvester`. D-16 (zakres floty) nie jest jeszcze zapisane w `decisions.md`.
- **Następna story:** `017-<slug>` — sesja A może ruszyć (z `main` po merge 016 albo z gałęzi 016). Przed sesją B 017: fail-closed ACL (D-13) i etykiety `016/AC-n`; kandydaci na reguły z 016 w `docs/backlog/deferred-static-analysis-sonarqube.md`.

## Nawyki operacyjne (nauczone kosztem czasu)
1. **Rejestr agentów** ładuje nowe/zmienione pliki `.claude/agents/*.md` dopiero na początku kolejnej wiadomości użytkownika. Po utworzeniu agenta poproś użytkownika o dowolną wiadomość, zanim wywołasz Agent.
2. **Prompt dla subagenta** = ścieżka do `runs/<story>/prompts/<template>.md` z instrukcją „przeczytaj w całości i wykonaj; zwróć tylko JSON `result`". Nie wklejaj treści, nie streszczaj — to niszczy izolację i provenance.
3. **Zapis plików przez subagenta** działa w trybie uprawnień `auto` sesji dyrygenta; w trybie headless (`claude -p`) Write jest blokowany, bo nie ma komu udzielić zgody.
4. **`claude --agent <persona>`** testuje się w PowerShell/cmd, nie z wnętrza działającej sesji Claude (użytkownik raz to pomylił — persona nie została wstrzyknięta). Persona nie zaczyna sama bez `initialPrompt`.
5. **`dotnet`**: nie uruchamiaj równolegle dwóch buildów/testów na tym samym drzewie (blokady `obj/`). Testy integracyjne wymagają Docker Desktop (Testcontainers) — sprawdzaj `docker ps` z PowerShell, nie `wsl docker`. `pwsh` nie ma w Git Bash — `powershell.exe -File`.
6. **Kolejność w dostawie:** `status.js` → `baseline.js` (przed C3a!) → C3a → migracja (użytkownik) → `migration-check.js` (REVIEW/DESTRUCTIVE → AskUserQuestion nazywające migrację + `decide.js --gate G2b`) → C2 → `red-first-prover` → C3b → `gate` → `coverage-gaps` → C4b. `gate`/`dod`/`closer` nigdy w discovery.
7. **Wynik agenta** zapisuj do `runs/<story>/impl/<agent>.<faza>/result.json`, waliduj `validate.js --schema result`, porównaj `files_changed` z `git status -- backend frontend`.
8. **Decyzje właściciela**: tylko `decide.js` (z `--answer-ref latest` po AskUserQuestion). Bramki: `decide.js --gate G2|G2b|G3 --status PASSED|REJECTED`; G2b wymaga odpowiedzi nowszej niż `migration-check.json`, nazywającej migrację (sha8) i zgodnej ze statusem — gate i status porównują `migration_sha`.
9. **Commity tylko po wyraźnym „tak"**; użytkownik chce widzieć, co wchodzi. Wiadomości commitów po angielsku, rozmowa po polsku.
10. **Heredoc w Bash tool** z backtickami/apostrofami potrafi się wywrócić — pisz skrypty plikiem (Write) i uruchamiaj `node plik.js`.

## Otwarte sprawy projektowe (nie decyzje właściciela)
- Propozycje do potwierdzenia — kontrakt BE↔BE, paczka G2, budżet tokenów per story: `workflow.md` §8a.
- `tasks.md` z dev-analysta zawierał gotowy kod (015) — reguła dodana do promptu, do sprawdzenia na 016.
- `SendMessage` niedostępne w `--agent`; dopytanie = nowa instancja (przyjęte).

# HANDOFF — flota v4, stan na 2026-10-05

Sam stan, bez historii. Historia i defekty: dziennik w `tools/fleet/README.md`. Projekt: `workflow.md`; decyzje właściciela: `decisions.md`; dostawa: `runbook-delivery.md`. Następne kroki: `NEXT-SESSION.md`, sekcja „Pierwsze kroki” (2–6).

## Gdzie jesteśmy
- **Gałęzie:** `main` = PR #59 (story 015 zmergowana). `016-rename-resource-schema` = `main` + sesja A 016 + commity floty; gałąź jest tylko lokalna (bez upstreamu).
- **Story 015:** zamknięta 2026-09-28, DoD 10/10, G3 PASSED — `runs/015-resource-time-buffer/{close-report.md,metrics.json}`.
- **Story 016:** G1 PASSED 2026-09-29 (issue #57, epik #56), G2 PASSED 2026-10-05; `status.js` pokazuje fazę C1. Jest `specs/016-rename-resource-schema/` (39 zadań w 8 fazach). Dostawa nie ruszyła: przed nią `fe-writer` v0 (`next-session-fe-writer.md`), potem sesja C zaczyna od kroku 0 (zmiana nazw w C#, robi dyrygent) i dopiero po nim `baseline.js`.
- **Etykiety AC:** testy zamkniętej story 015 mają `[Trait("AC", "015/AC-n")]`, żeby filtr `AC=AC-n` widział tylko bieżącą story. Etykiety 016 trzeba przemianować tak samo przed C2 story 017 (reguła trwała — po G3 016).
- **Przeszło na prawdziwej story:** sesja A (016), sesja B (015), sesja C fazy C1–C6 (015). Agenci i skrypty ze statusami: tabele w `tools/fleet/README.md`.
- **Zbudowane, niesprawdzone na prawdziwej story:** `review-arbiter` (nie było sporu); `migration-check.js` (S9b: 63 testy + syntetyczna story, pierwszy prawdziwy przebieg będzie na 016).
- **Nie istnieje:** `fe-writer` (do zbudowania w nowej sesji wg `next-session-fe-writer.md`, przed sesją C 016), `spec-critic`, skill `/deliver` (dyrygent = sesja główna ręcznie), projekty `*.Tests.Unit`, skrypt generatora klienta TS (`swagger-typescript-api` jest w devDeps, skryptu brak), `npm run lint` we frontendzie.
- **Otwarta luka:** agent z plikiem w `.claude/agents/`, który nie jest personą i nie ma wpisu w `fleet-acl.json`, działa w hooku bez ograniczeń (pilnuje go tylko `prompt-builder`). Fail-closed = zmiana D-13, zaplanowana po G3 016, przed sesją B 017.
- **Skreślone przez właściciela:** `plan-guard` (w jego miejsce spec-critic), `migration-reviewer` (w jego miejsce `migration-check`), `docs-delta`, `rule-harvester`. D-16 (zakres floty) nie jest jeszcze zapisane w `decisions.md`.
- **Następna story:** `017-<slug>`, dopiero po G3 016.

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

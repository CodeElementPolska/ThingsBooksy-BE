# HANDOFF — flota v4, stan na 2026-09-28

Dla nowej sesji przejmującej pracę. Czytać razem z `workflow.md`, `decisions.md`, `runbook-delivery.md`, `tools/fleet/README.md`.

## Gdzie jesteśmy
- Gałęzie: `ai/014-agents-redesign` = flota (head `c71782a`); `015-resource-time-buffer` = `ai/014` + discovery + C3a + **C2/C3b/C4b (sesja C, 2026-09-28)** przeplatane commitami floty `fix(fleet)` (`7c395ab`, `c59d049`, `a1f13cf`). Decyzja właściciela: cherry-pick commitów floty na `ai/014` i rebase 015 **dopiero po zaprojektowaniu C5/C6** — wszystko przenosimy na koniec.
- Story 015: G2 PASSED, C3a/C2/C3b/C4b DONE, gate GREEN (138 testów), `status.js` = C4 PASSED. Otwarte na G3: `UNSPECIFIED` ASM-S1/S2 (kasowanie już skasowanego → 400, brak zdarzenia — dodać AC czy zostawić), ASM-S5 (brak projektu `Resources.Tests.Unit`, T027 niewykonany). **Następny krok: zaprojektować C5 (reviewerzy ×3, `dedup-findings`, `review-arbiter`) na materiale 015 i uruchomić; potem C6 (`architecture-guard`, `trace-auditor`, `dod.js`, `closer.js`, G3).**
- Zbudowane i sprawdzone na prawdziwej story: substrat (`tools/fleet/*`), sesja B (`dev-analyst`, `code-researcher`, `impact-analyst`), agenci dostawy (`be-writer` C3a+C3b, `test-designer` C2, `test-designer-sighted` C4b). Dziennik defektów z sesji C w `tools/fleet/README.md` (11 wpisów z 2026-09-28: CRLF, AC z front matter, cytowanie `node`, kolektor coverlet, ścieżki Cobertura, zakres hasha red-first i inne).
- **Nie istnieje jeszcze:** sesja A (`scrum`, `capability-analyst`, `scope-critic`, `premortem-critic`, `backlog-writer`), C5 (reviewerzy ×3, `review-arbiter`, `dedup-findings`), C6 (`architecture-guard`, `trace-auditor`, `docs-delta`, `rule-harvester`), `plan-guard`, `migration-reviewer`, `fe-writer`, skill `/deliver` (dyrygent = sesja główna ręcznie), skrypt workflow, projekty `*.Tests.Unit`, odłożony epik analizatorów (`docs/backlog/`).

## Nawyki operacyjne (nauczone kosztem czasu)
1. **Rejestr agentów** ładuje nowe/zmienione pliki `.claude/agents/*.md` dopiero na początku kolejnej wiadomości użytkownika. Po utworzeniu agenta poproś użytkownika o dowolną wiadomość, zanim wywołasz Agent.
2. **Prompt dla subagenta** = ścieżka do `runs/<story>/prompts/<template>.md` z instrukcją „przeczytaj w całości i wykonaj; zwróć tylko JSON `result`". Nie wklejaj treści, nie streszczaj — to niszczy izolację i provenance.
3. **Zapis plików przez subagenta** działa w trybie uprawnień `auto` sesji dyrygenta; w trybie headless (`claude -p`) Write jest blokowany, bo nie ma komu udzielić zgody.
4. **`claude --agent <persona>`** testuje się w PowerShell/cmd, nie z wnętrza działającej sesji Claude (użytkownik raz to pomylił — persona nie została wstrzyknięta). Persona nie zaczyna sama bez `initialPrompt`.
5. **`dotnet`**: nie uruchamiaj równolegle dwóch buildów/testów na tym samym drzewie (blokady `obj/`). Testy integracyjne wymagają Docker Desktop (Testcontainers) — sprawdzaj `docker ps` z PowerShell, nie `wsl docker`. `pwsh` nie ma w Git Bash — `powershell.exe -File`.
6. **Kolejność w dostawie:** `status.js` → `baseline.js` (przed C3a!) → C3a → migracja (użytkownik) → C2 → `red-first-prover` → C3b → `gate` → `coverage-gaps` → C4b. `gate`/`dod`/`closer` nigdy w discovery.
7. **Wynik agenta** zapisuj do `runs/<story>/impl/<agent>.<faza>/result.json`, waliduj `validate.js --schema result`, porównaj `files_changed` z `git status -- backend`.
8. **Decyzje właściciela**: tylko `decide.js` (z `--answer-ref latest` po AskUserQuestion). Bramki: `decide.js --gate G2 --status PASSED`.
9. **Commity tylko po wyraźnym „tak"**; użytkownik chce widzieć, co wchodzi. Wiadomości commitów po angielsku, rozmowa po polsku.
10. **Heredoc w Bash tool** z backtickami/apostrofami potrafi się wywrócić — pisz skrypty plikiem (Write) i uruchamiaj `node plik.js`.

## Otwarte sprawy projektowe (nie decyzje właściciela — propozycje w `workflow.md` §8a)
- kontrakt BE↔BE (zdarzenia w `Shared.Abstractions`): krok szeregowy w C3a jako jedyny właściciel zapisu; test architektury na sieroty;
- paczka G2: jeden ekran (decyzje → założenia → diff endpointów → szkic UI);
- budżet tokenów per story (`state.budget_tokens`) — po 2–3 stories z metrykami;
- `tasks.md` z dev-analysta zawiera gotowy kod (015) — reguła dodana do promptu, do sprawdzenia na następnej story;
- `SendMessage` niedostępne w `--agent`; dopytanie = nowa instancja (przyjęte).

## Materiał do projektowania C5/C6 (reviewerzy, arbiter, guard)
- `reviews/2026-09-21-fable.md`, `reviews/2026-09-21-opus.md` — pierwsze recenzje projektu (sekcje o pętli review, arbitrze, guardach, metrykach);
- `reviews/2026-09-23-blind-critic-opus.md` — ślepa recenzja z symulacją story;
- `runs/015-resource-time-buffer/` — prawdziwy materiał: spec z AC, wynik C3a, wkrótce testy i zachowanie.
- Reguły już ustalone: D-9 (wagi), `findings.schema.json` (`rule_ref` obowiązkowy), arbiter tylko dla sporów, od rundy 2 tylko diff, „blockery muszą maleć", zaakceptowana uogólnialna uwaga → `rule_candidate`.

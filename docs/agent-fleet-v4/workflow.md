# Flota agentów v4 — projekt workflow

**Status:** DRAFT v1 (2026-09-23). Zakres tej iteracji: workflow i komunikacja między agentami.
Prompty agentów, skille i skrypty powstają w kolejnych iteracjach (kolejność: D-3).
Decyzje właściciela: `decisions.md` (D-1…D-8). Odłożone: `../backlog/deferred-static-analysis-sonarqube.md`.

---

## 0. Zasady projektowe

1. **Źródło prawdy poza LLM.** O poprawności rozstrzyga skrypt lub test, nie drugi model. LLM ocenia tylko to, czego nie da się wyrazić maszynowo.
2. **Rama = mechanizm, nie prompt.** Izolację i limity egzekwują: allowlista narzędzi, hook `PreToolUse` per agent (ACL ścieżek), `omitClaudeMd`, brak powłoki u agentów ślepych, schemat wyniku, `maxTurns`, prompty składane przez skrypt.
3. **Bramka właściciela = granica fazy.** Subagent i skrypt workflow nie potrafią zapytać człowieka. Docelowo 3 bramki na story (G1, G2, G3) + wyjątkowa G2b przy migracji destrukcyjnej.
4. **Jeden autor na artefakt; artefakt ma schemat i provenance** (autor, run id, hashe wejść). Nikt nie „przepisuje" cudzego dokumentu.
5. **Trzy koszyki decyzji (D-2) ze sztywną listą (D-1).** Wszystko, czego agent nie zapytał, jest w logu z punktacją.
6. **Hooki egzekwują, nigdy nie sterują przepływem.** Przepływ jest w skrypcie workflow z dziennikiem i wznawianiem.
7. **Jedna story naraz (D-8).** Równoległość tylko wewnątrz story.

---

## 1. Trzy sesje

```
SESJA A  /shape     (biznes)     → GitHub: Epic + Story              [G1]
SESJA B  /discover  (discovery)  → runs/<story>/discovery/* + specs/  [G2]
SESJA C  /deliver   (dostawa)    → kod, testy, docs, GitHub, commit   [G2b?, G3]
```

Każda sesja startuje z czystego kontekstu i czyta wyłącznie pliki poprzedniej. Sesje A i B to persony sesji głównej (`claude --agent scrum`, `claude --agent dev-analyst`) — jedyne agenty, które rozmawiają z właścicielem. Sesja C to cienki dyrygent (skill) uruchamiający fazy jako skrypty workflow.

### 1.1 Sesja A — `/shape` (biznes)

```
scrum [sesja główna] ⇄ właściciel
  ├─ capability-analyst [sub, RO]    ← capability-map.json + swagger z builda + trasy FE
  ├─ scope-critic       [sub, blind] ← proposal.md + capability-report + odrzucone opcje
  └─ premortem-critic   [sub, blind] ← proposal.md + capability-report + blast radius
raporty krytyków → właściciel NIEFILTROWANE (scrum nie streszcza)
→ story.md → backlog-writer [skrypt] → GitHub Epic/Story
```

**Wejście:** pomysł właściciela. **Wyjście:** issue Story z sekcjami: cel i „dlaczego", kryteria akceptacji z ID (`AC-1…`), poza zakresem, odrzucone alternatywy z powodami, `blast_radius` (moduły), `depends_on`, wersja capability-map. Bez tasków (D-6).
**G1:** właściciel akceptuje story. Skrypt `backlog-writer` waliduje wymagane sekcje przed zapisem do GitHuba (dry-run pokazany właścicielowi).
**Stop krytyków:** jedna runda + jedna replika; `NO_OBJECTIONS` dozwolone; każdy zarzut ma wagę z rubryki i alternatywę.

### 1.2 Sesja B — `/discover` (discovery techniczne)

```
dev-analyst [sesja główna] ⇄ właściciel   (pętla bez limitu rund; stop = koszyk ZAPYTAJ pusty)
  ├─ code-researcher ×N [sub, RO, ∥]  ← JEDNO pytanie → fakt + plik:linia + pewność
  └─ impact-analyst     [sub, RO]     ← story + kod → moduły, pliki, migracja?, BLOCKED_BY
po KAŻDEJ rundzie: zrzut do runs/<story>/discovery/round-N.md
→ decisions.md · assumptions.md · facts.md · ui-sketch.md · spec.md (D-5, D-7)
→ /speckit-plan → /speckit-tasks → /speckit-clarify + /speckit-analyze jako LINT
→ contract-author [sub] → contract-delta.yaml (różnica vs swagger z builda)
| verdict: RETURN / SPLIT / TOO_BIG / BLOCKED_BY → issue dostaje etykietę + powód, sesja kończy się
```

**Format pytania do właściciela (D-2):** kontekst prostym językiem → co mówi kod (dowód) → opcje → rekomendacja z argumentem → najmocniejszy argument przeciw → konsekwencje.
**G2 (jedna paczka):** spec.md + plan + tasks + ui-sketch + contract-delta + lista założeń (koszyk ZAŁÓŻ, milczenie = default) + decyzje z koszyka ZAPYTAJ (wymagają odpowiedzi). Skrypt zapisuje odpowiedzi właściciela do `owner-answers.jsonl`; wpis `decided_by: owner` w `decisions.md` musi wskazywać id odpowiedzi.
**Lint:** jeśli `/speckit-clarify` ma pytania, discovery nie było skończone — wracamy do pętli, nie do właściciela.

**Implementacja sesji B (2026-09-24):** persona `.claude/agents/dev-analyst.md` (uruchamiana `claude --agent dev-analyst` na gałęzi `NNN-slug`; hook `PostToolUse(AskUserQuestion)` → `tools/fleet/owner-answer-hook.js` → `journal.jsonl`), workerzy `.claude/agents/code-researcher.md` i `impact-analyst.md` z szablonami w `tools/fleet/templates/`, prompty składane przez `prompt-builder.js`, decyzje zamykane wyłącznie przez `tools/fleet/decide.js`. Status: `tools/fleet/README.md`.

### 1.3 Sesja C — `/deliver` (dostawa, jedna story)

Dyrygent = skill w sesji głównej, który: wylicza stan skryptem `status`, uruchamia kolejną fazę jako skrypt workflow, obsługuje skrzynkę decyzji na granicach faz. **Nie pisze promptów agentów** — robi to skrypt z szablonów i listy dozwolonych ścieżek.

```
C1 BRAMA PLANU      arch-tests [skrypt] → plan-guard [sub, RO] → contract-validate [skrypt]
                    → limit rozmiaru story [skrypt]
C3a SZKIELET        scaffold [skrypt, gdy nowy moduł]
                    be-writer ×moduł [sub]: encje (właściwości + Create), konfiguracja EF, DbSet, migracja
                    BEZ handlerów, endpointów i metod domenowych (sprawdzane skryptem: brak nowych plików
                    w katalogach handlerów/endpointów)
                    → migracja generowana [skrypt] → migration-reviewer [sub] (destrukcyjna → G2b)
                    → core-surface [skrypt]: dump powierzchni typów → generated/core-surface.json
C2  TESTY (przebieg 1, ślepy)
                    test-designer BE + FE [sub, ACL deny src/**, `dotnet build` tylko projekty testowe]
                    wejście: AC + contract-delta + core-surface.json + konwencje testowe
                    pisze fabryki per encja (projekt testów) + testy: Arrange EF → Act HTTP → Assert EF
                    → red-first prover [skrypt]: testy kompilują się i są CZERWONE (404, nie brak typów)
                    → ac-matrix [skrypt]: każde AC ma ≥1 test → hash tests/acceptance
C3b ZACHOWANIE      be-writer ×moduł  ∥  fe-writer ×komponent   [sub, tests/acceptance RO]
                    handlery, endpointy, metody domenowe, testy jednostkowe
                    (klient TS generowany [skrypt])
                    → wynik: DONE | BLOCKED_ON_DECISION | DISPUTE | DISPUTE_TEST (test błędny → tester)
                    → krok integracyjny [szeregowy]: pliki współdzielone (.slnx, Bootstrapper, WebAppFactory)
C4  BRAMKI          gate [skrypt]: build · format · analizatory · arch-tests · unit + acceptance
                    · contract-diff (swagger z builda vs contract-delta) · ac-matrix · hash testów akceptacyjnych
                    (hash liczony PO dotnet format, żeby formatowanie go nie łamało)
                    porażka → writer (dostaje TYLKO output gate'a) → C4 ponownie (limit 3 rund)
C4b TESTY (przebieg 2, z kodem)
                    coverage [skrypt]: nietrafione gałęzie NOWEGO kodu (dotnet test --collect coverage)
                    test-designer [sub, ACL src/** zdjęty, tests/acceptance z przebiegu 1 RO]
                    pisze test per nietrafiona gałąź; każdy test: tag AC-id ALBO UNSPECIFIED: plik:linia
                    UNSPECIFIED → decyzja właściciela w G3 (dodać AC / usunąć regułę)
                    → gate ponownie
C5 REVIEW           review-spec-conformance ∥ review-security-authz ∥ review-maintainability [sub, RO]
                    → dedup [skrypt] → writer naprawia | DISPUTE → review-arbiter [sub]
                    zmiana zachowania → spec-delta → C2 (tester aktualizuje) → C3
                    stop: 0 blockerów | 3 rundy | blockery nie maleją → paczka eskalacyjna (G3)
C6 ZAMKNIĘCIE       architecture-guard [sub, tylko reguły niewyrażalne maszynowo]
                    → trace-auditor [sub] (jakość asercji per AC) → pełna regresja [skrypt]
                    → docs-delta [sub] (capability-map narracyjna, ADR) → rule-harvester [sub]
                    → closer [skrypt]: DoD, checklista na issue story, state.json, metryki
G3                  eskalacje + raport + zgoda na commit (właściciel)
```

**G2b:** gdy `migration-reviewer` oznaczy migrację jako destrukcyjną — faza C3 kończy się, dyrygent pyta właściciela.

---

## 2. Tabela agentów

Legenda trybu: **główna** = persona sesji głównej (`--agent`), **sub** = subagent, **skrypt** = bez LLM.
„Nie widzi" egzekwowane hookiem ACL + `omitClaudeMd` + brakiem narzędzi, nie promptem.

| Agent | Tryb | Wejście | Wyjście (schemat) | Nie widzi | Narzędzia | Stop |
|---|---|---|---|---|---|---|
| **scrum** | główna | rozmowa, raporty subów | `story.md` | kod źródłowy | Read(docs, capability-map), Agent, AskUserQuestion | G1 |
| **capability-analyst** | sub RO | pytanie + capability-map + swagger + trasy | `capability-report.json` (fakty + dowody) | rozmowa, `specs/*`, kod | Read, Grep (ACL: docs + generated) | schemat OK |
| **scope-critic** | sub blind, inny model/effort | proposal + capability-report + odrzucone opcje | `critique.json` (rubryka wartość/zakres) \| NO_OBJECTIONS | rozmowa, kod, CLAUDE.md | brak (tekst) | `maxTurns: 3` |
| **premortem-critic** | sub blind | proposal + capability-report + blast radius | `critique.json` (rubryka awarii: dane, migracje, authz, ops) | jw. | brak | `maxTurns: 3` |
| **backlog-writer** | skrypt | `story.md` | issue GitHub (Epic/Story) | — | MCP GitHub | walidacja sekcji + dry-run |
| **dev-analyst** | główna | issue story, fakty researcherów | decisions, assumptions, facts, ui-sketch, `spec.md` \| RETURN/SPLIT/TOO_BIG/BLOCKED_BY | — (rozmówca) | Read, Agent, AskUserQuestion, Write(`runs/<story>/discovery/`, `specs/<story>/`), skille SpecKit (plan, tasks, clarify, analyze) | koszyk ZAPYTAJ pusty |
| **code-researcher** ×N | sub RO ∥ | jedno pytanie faktograficzne | fakt + plik:linia + pewność \| „nie wiem" | hipotezy, uzasadnienie story | Read, Grep, Glob | schemat OK |
| **impact-analyst** | sub RO | story + kod | moduły, pliki, migracja?, BLOCKED_BY | rozmowa | Read, Grep, Glob | schemat OK |
| **contract-author** | sub | spec + swagger z builda | `contract-delta.yaml` (tylko zmiany, cytaty dla istniejącego) | plan implementacji | Read, Write(contract) | walidator OpenAPI + rejestr faktów |
| **plan-guard** | sub RO | plan.md, tasks.md, konstytucja | `violations.json` z `rule_ref` | kod, notatki | Read, Grep | 0 naruszeń \| ESCALATE |
| **test-designer** BE/FE — przebieg 1 | sub blind | AC z ID, contract-delta, `core-surface.json`, konwencje testowe, infrastruktura testów | fabryki per encja + testy akceptacyjne (Arrange EF → Act HTTP → Assert EF; FE: DOM + mocki) z tagami AC | `src/**`, plan, tasks, writerzy | Read(tests, specs, generated), Write(tests/acceptance), Bash allowlist: `dotnet build <projekt testów>` | red-first prover: czerwone; ac-matrix pełna |
| **test-designer** — przebieg 2 | sub (ten sam typ, inny ACL) | raport pokrycia (nietrafione gałęzie nowego kodu), kod, AC | testy dla gałęzi, każdy z tagiem `AC-id` lub `UNSPECIFIED: plik:linia` | testy przebiegu 1 (zapis), notatki writera | Read(src, tests), Write(tests/acceptance/pass2), Bash: `dotnet build/test` | każda nietrafiona gałąź ma test lub uzasadnienie |
| **be-writer** ×moduł | sub | spec, plan, tasks, contract-delta, konwencje BE, testy akceptacyjne (RO) | kod + testy jednostkowe + `result.json` (DONE/BLOCKED_ON_DECISION/DISPUTE, `assumptions[]`, `tasks_completed[]`) | inne moduły (zapis), `tests/acceptance` (zapis), decisions (zapis) | Read, Edit, Write(ACL: swój moduł), Bash allowlist (`dotnet build/test/format`) | gate zielony |
| **fe-writer** ×komponent | sub | jw. + ui-sketch + generowany klient TS | jw. | kod BE, `tests/acceptance` (zapis) | Read, Edit, Write(ACL: swój komponent), `ng build`, `npm test` | gate zielony |
| **migration-reviewer** | sub RO | plik migracji (wygenerowany skryptem) | ocena ryzyka: `destructive: bool` + uzasadnienie | — | Read | destrukcyjna → G2b |
| **review-spec-conformance** | sub RO ∥ | diff + spec + contract-delta | `findings.json` (rule_ref = AC-id, dowód, waga; typ UNSPECIFIED_BEHAVIOR obowiązkowy) | `assumptions` writera, inne przeglądy | Read, Grep | schemat OK |
| **review-security-authz** | sub RO ∥ | diff + macierz rola×endpoint [skrypt] | `findings.json` (rule_ref = CWE / konstytucja) | jw. | Read, Grep | schemat OK |
| **review-maintainability** | sub RO ∥ | diff + konwencje oznaczone `enforced-by: llm-review` | `findings.json` + `rule_candidate[]` | jw.; reguły egzekwowane narzędziami | Read, Grep | schemat OK |
| **review-arbiter** | sub RO | JEDNO sporne znalezisko + tekst sporu + fragment kodu + przywołana reguła | UPHOLD \| OVERTURN \| ESCALATE + uzasadnienie | historia, reszta przeglądu | Read | jeden werdykt |
| **architecture-guard** | sub RO | solution po zielonych arch-tests, konstytucja | `violations.json` (tylko semantyczne) | notatki writerów | Read, Grep | 0 \| ESCALATE |
| **trace-auditor** | sub RO | ac-matrix + spec + testy | lista słabych asercji per AC (w tym dokładne komunikaty, ścieżki admina) | kod produkcyjny | Read | każde AC ocenione |
| **docs-delta** | sub | spec-delta, decisions | capability-map narracyjna, ADR | kod | Read, Write(docs) | DoD |
| **rule-harvester** | sub | zaakceptowane `rule_candidate` | issue „dodaj analizator / arch-test / lint" | — | Read, (GitHub przez skrypt) | schemat OK |

Reguły od rundy 2 przeglądu: reviewer widzi tylko diff poprawek + swoje poprzednie uwagi; uwagi spoza diffu → dług techniczny (issue), chyba że konstytucja/bezpieczeństwo.

---

## 3. Artefakty i ich schematy

```
runs/<story>/
  state.json              ← wyliczany skryptem `status`, nigdy pisany przez agenta
  events.jsonl            ← hooki SubagentStart/Stop (agent_type, run id, czas, tokeny)
  owner-answers.jsonl     ← surowe odpowiedzi właściciela (hook UserPromptSubmit / skrypt bramki)
  discovery/
    round-N.md            ← zrzut każdej rundy rozmowy
    decisions.md          ← {id, question, options, chosen, decided_by, answer_ref, rationale}
    assumptions.md        ← {id, assumption, bucket, score{reversible, visible, hard_list}, default, veto?}
    facts.md              ← {claim, evidence: file:line | captured-response, by: researcher-run}
    ui-sketch.md          ← ekrany → elementy → stany → akcje → endpointy
  contract-delta.yaml     ← OpenAPI diff vs generated/swagger.base.json
  tests/red-first.json    ← lista testów + komunikaty upadku PRZED implementacją
  ac-matrix.json          ← AC-id → [testy] (generowany)
  impl/<agent>/result.json
  review/round-N/<reviewer>.findings.json · disputes.json · arbiter.json
  escalations.md          ← paczka dla G3
  metrics.json
specs/<story>/            ← spec.md, plan.md, tasks.md — po zamknięciu NIEMUTOWALNE
generated/                ← capability-map.json, swagger.base.json, api-client/ (nigdy ręcznie)
```

**Schematy:** `docs/agent-fleet-v4/schemas/` (JSON Schema 2020-12) — `provenance`, `story`, `critique`, `decision`, `assumption`, `fact`, `result`, `findings`, `arbiter-verdict`, `fleet-acl`, `state`, `journal-event`; tabela producent → konsument → walidator oraz definicje BLOCKER/MAJOR/MINOR/OPINION i statusów writera w `schemas/README.md`. `contract-delta` = OpenAPI Overlay 1.0 nad `generated/swagger.base.json`; `contract-next.json = base ⊕ overlay` karmi generator klienta TS i `contract-diff`.

Każdy artefakt agenta ma nagłówek provenance: `{author_agent, run_id, story, inputs: [{path, sha256}]}`. Skrypt odrzuca artefakt, którego wejścia zmieniły hash od czasu wygenerowania (nieaktualność). Bez `rule_ref` znalezisko ma `severity: OPINION` (nie blokuje, trafia do zbiorczego przeglądu dla właściciela).

---

## 4. Mechanizmy egzekwowania

| Reguła | Mechanizm |
|---|---|
| tester nie czyta `src/**`; writer nie pisze do `tests/acceptance/**` ani cudzych modułów | `fleet-acl.json` (read-allow / read-deny / write-allow per `agent_type`) + jeden hook `PreToolUse` w frontmatterze agenta; normalizacja backslashy i wielkości liter (Windows) |
| agent ślepy nie omija ACL powłoką | brak Bash/PowerShell w `tools`; testy uruchamia skrypt-runner |
| agent nie widzi orkiestracji ani starej floty | `omitClaudeMd: true` + tylko potrzebne konwencje przez `skills:`; orkiestracja usunięta z `CLAUDE.md` |
| orchestrator nie „dopisuje" kontekstu do promptów | prompty składane przez skrypt z szablonu + listy ścieżek; zakaz wklejania treści plików (hooki nie działają na wklejony tekst) |
| wynik agenta ma stały kształt | `schema` w `agent()` (walidacja + retry) |
| pętle mają koniec | `maxTurns` per agent + licznik rund w skrypcie + reguła „blockery muszą maleć" |
| agent read-only nie potrzebuje Write | raport = tekst zwracany; zapisuje skrypt |
| decyzja właściciela nie może być sfabrykowana | `decided_by: owner` wpisuje tylko skrypt, z `answer_ref` do `owner-answers.jsonl`; subagenci bez zapisu do `decisions.md` |
| założenie z listy D-1 nie przejdzie bez decyzji | DoD grep po `assumptions.md`: `hard_list: true` bez `decided_by: owner` → FAIL |
| testy akceptacyjne nie są „dopasowane" po fakcie | hash katalogu `tests/acceptance` po C2 (liczony po `dotnet format`), sprawdzany w C4; red-first proof; przebieg 2 pisze do osobnego katalogu |
| test błędny (nie kod) nie blokuje na zawsze | writer zwraca `DISPUTE_TEST` z dowodem (AC + zachowanie testu) → tester poprawia → jawny re-baseline hasha zapisany w dzienniku |
| brak nieodwracalnych akcji | globalny deny: `git push`, `git commit` (poza closerem za zgodą), `git reset`, `dotnet ef database update` poza lokalnym; brak `bypassPermissions`; zapis do GitHuba tylko skryptem z dry-runem |
| brak przecieku między story | `memory:` wyłączone u agentów ślepych i krytyków |
| dane zewnętrzne to nie instrukcje | reguła w promptach agentów czytających GitHub/WWW + treść przekazywana jako dane w schemacie |

---

## 5. Substrat — lista skryptów (kolejność budowy, D-3)

**Implementacja i status:** `tools/fleet/README.md` (D-12). Stan 2026-09-24: S1 (hook ACL, zarejestrowany w `.claude/settings.json` projektu, D-13), S2 (`capability-map` + test `SwaggerExport`, D-15), S3 (`contract-compose` na OpenAPI Overlay + `contract-diff`), S5 (`ac-matrix` z `[Trait("AC")]`, D-14) — zweryfikowane. Pozostałe ⏳.

| # | Skrypt | Co robi | Zamyka wadę audytu |
|---|---|---|---|
| S1 | `fleet-acl` (hook) | egzekwuje `fleet-acl.json` per agent | 1 |
| S2 | `capability-map` | swagger z builda + trasy Angulara + moduły → `generated/capability-map.json` | 5 (analityk wymyślający stan) |
| S3 | `contract-validate` / `contract-diff` | walidacja OpenAPI delta; diff swagger z builda vs delta | 5 |
| S4 | `red-first-prover` | uruchamia testy akceptacyjne po szkielecie (C3a), przed zachowaniem; muszą się kompilować i upaść | 1 |
| S4a | `core-surface` | dump powierzchni typów modułu po C3a (typy, sygnatury `Create`, właściwości, `DbSet`) → `generated/core-surface.json` | 1 (tester nie czyta kodu) |
| S4b | `coverage-gaps` | nietrafione gałęzie NOWEGO kodu po zielonym gate → wejście dla przebiegu 2 testera | 6 |
| S4c | `skeleton-check` | w C3a brak nowych plików w katalogach handlerów/endpointów | — |
| S5 | `ac-matrix` | AC-id ↔ testy z metadanych testów | 6 |
| S6 | `gate` (C4) | build, format, analizatory, arch-tests, testy, contract-diff, ac-matrix, hash testów | 1, 3, 5 |
| S7 | `dod` + `closer` + `status` | bramka DoD, checklista na issue, `state.json`, metryki | 3 |
| S8 | `prompt-builder` | składa prompty agentów z szablonów i list ścieżek; provenance | 1, 2 |
| S9 | `dedup-findings`, `authz-matrix`, `gen-api-client`, `gen-migration`, `scaffold-module` | mechaniczne kroki C3/C5 | — |
| S10 | `fleet-smoke` | zabawkowa story: każdy agent produkuje swój artefakt swoimi narzędziami | 4 |

Reguły kodu (analizatory, NetArchTest, SonarQube) — odłożony epik; `gate` (S6) ma na nie miejsce, wchodzą, gdy powstaną.

---

## 6. Metryki (per story, punkt odniesienia = audyt 013)

1. Liczba przerwań właściciela (cel: ≤ 3 bramki + G2b).
2. Decyzje zakopane w założeniach, wykryte po fakcie (cel: 0 dla listy D-1).
3. Założenia „trywialne" obalone przez właściciela (kalibracja rubryki D-2).
4. Rozjazdy dokument–kod na końcu (cel: 0 — contract-diff, ac-matrix).
5. Pokrycie AC testami (cel: 100% w macierzy; trace-auditor ocenia jakość).
6. Defekty znalezione ręcznie w 7 dni po zamknięciu.
7. Rundy napraw, tokeny, czas ścienny.
8. Odsetek zaakceptowanych uwag review zamienionych w check deterministyczny.

Źródło: `events.jsonl` + `metrics.json` pisane przez hooki i closer, nie przez agentów.

---

## 7. Weryfikacja założeń platformowych (prototypy 2026-09-24)

| Założenie | Wynik | Konsekwencja dla projektu |
|---|---|---|
| Hook `PreToolUse` z frontmatteru subagenta blokuje Read/Grep/Glob po ścieżce na Windows | **POTWIERDZONE** (10/10 w harnessie, 18/18 wariantów ścieżek standalone) | Model **allowlist** (lista dozwolonych katalogów), fail-closed, kanonizacja przez `fs.realpathSync.native`. Lista zakazów po tekście NIE wystarcza: Grep bez `path` i Glob `**` przeszukują całe repo. Hook dostaje `agent_type` i `agent_id`, więc wariant „jeden globalny hook w `settings.json` z rozgałęzieniem po `agent_type`" też jest wykonalny. Pola: Read `file_path`, Grep `pattern,path,output_mode`, Glob `pattern[,path]`. |
| `omitClaudeMd: true` odcina treść `CLAUDE.md` | **POTWIERDZONE** (kontrola bez flagi cytowała reguły; z flagą — brak) | Ale harness wstrzykuje każdemu subagentowi sekcję `gitStatus` z nazwami zmienionych/przemianowanych plików → **nazwy plików niezacommitowanych zmian są widoczne dla agentów ślepych** (treść nie). Akceptowalne w C2 (szkielet i tak jawny), do uwzględnienia przy krytykach: uruchamiać na czystym drzewie lub w worktree. |
| `tools: []` = brak narzędzi | **FAŁSZ** — pusta lista = wszystkie narzędzia | Agent „bez narzędzi" definiować jawną minimalną listą (`tools: Read`) + ACL. |
| `resumeFromRunId` po zmianie wejścia | **POTWIERDZONE z zastrzeżeniem**: replay niezmienionego prefiksu, pierwszy zmieniony `agent()` i wszystko za nim liczy się od nowa; **tylko w tej samej sesji** | Fazy C1–C6 muszą mieć dziennik w plikach (`runs/<story>/journal.jsonl`) i wznawiać się z artefaktów; runtime workflow nie jest źródłem stanu. |
| `claude --agent <persona>`: prompt systemowy, allowlista narzędzi, hooki z frontmatteru | **POTWIERDZONE** w trybie `-p` i interaktywnym: prompt persony = prompt systemowy; `tools:` egzekwowane (interaktywnie dokładnie 8 narzędzi z frontmatteru); hooki z frontmatteru odpalają się z `agent_type`; `CLAUDE.md` dociera (persona = sesja główna). Sesja czeka na pierwszy prompt — persona nie zaczyna sama. (Pierwszy test właściciela był nieważny: komendę wpisano wewnątrz działającej sesji Claude.) | Persony `scrum`/`dev-analyst` dostają `initialPrompt:` we frontmatterze, żeby rozmowa zaczynała się od ich pytania. Uruchamiać z PowerShell/cmd, nie z wnętrza Claude. |
| `AskUserQuestion` w personie `--agent` | **POTWIERDZONE** (pytanie z opcjami działa pod allowlistą) | — |
| `SendMessage` (kontynuacja subagenta) w trybie `--agent` | **NIEDOSTĘPNE** (potwierdzone na story 015): dopytanie = nowa instancja z nowym plikiem pytania | projekt zakłada jednorazowych workerów; fact id = numer pytania |
| `.specify/feature.json` nadpisuje gałąź dla skryptów SpecKita | **PUŁAPKA potwierdzona** (na 015 `/speckit-plan` nadpisał `specs/010/plan.md`; przywrócono z gita) | `status.js` odmawia pracy przy niezgodności; checklista startu persony |
| Skrypty SpecKita (`pwsh`) w Git Bash | **PUŁAPKA**: `pwsh` nie ma w PATH Git Bash (exit 127); działa `powershell.exe -File` | skrypty substratu i skille wołać przez `powershell.exe` lub `node`, nie `pwsh` |
| Skille SpecKita wewnątrz persony | **POTWIERDZONE** (skill ładuje się, `check-prerequisites.ps1` działa, exit 0). **Pułapka:** dla gałęzi spoza konwencji `NNN-slug` skrypt po cichu wskazał `specs/010-*` (fallback) — skill edytowałby cudzą spec | `<story>` id = numer SpecKita `NNN`, gałąź `NNN-slug`, katalog `specs/NNN-slug/`, `runs/NNN-slug/`; skrypt `status` odmawia pracy, gdy gałąź nie pasuje do wzorca |
| `AskUserQuestion` usuwane subagentom | niezweryfikowane wprost (brak w dokumentacji; wniosek pośredni z braku dialogu) | projekt i tak zakłada brak |
| Zapis odpowiedzi właściciela do `owner-answers.jsonl` | **POTWIERDZONE**: hook `PostToolUse` z matcherem `AskUserQuestion` dostaje `tool_response.answers` (pytanie → wybrana opcja), `annotations`, `agent_type`, `prompt_id`, `tool_use_id`, `duration_ms`. (`ElicitationResult` dotyczy tylko MCP; `UserPromptSubmit` łapie wpisane prompty.) | Provenance decyzji: `PostToolUse(AskUserQuestion)` → `owner-answers.jsonl`; `answer_ref` = `tool_use_id`. Odpowiedzi wpisane tekstem łapie `UserPromptSubmit`. |
| `dotnet test --collect coverage` daje raport gałęzi dla S4b | **DO SPRAWDZENIA** | — |
- ~~Fabryki testowe dla nowych encji przy testerze ślepym~~ → rozstrzygnięte D-4a/D-4b (szkielet → dump powierzchni → tester).
- Czy `dotnet test --collect:"XPlat Code Coverage"` daje raport gałęzi wystarczający do wyznaczenia nietrafionych gałęzi nowego kodu (S4b).

---

## 8a. Uzupełnienia po ślepej recenzji (propozycje, do potwierdzenia)

- **Kontrakt BE↔BE (zdarzenia, `IModuleClient`).** `contract-delta` pokrywa tylko HTTP. Dla komunikacji między modułami kontraktem jest sam kod w `Shared.Abstractions` (rekordy `IEvent`, interfejsy klientów). Propozycja: `contract-author` w sesji B produkuje też `contracts/shared-abstractions.delta.md` (lista nowych/zmienionych rekordów zdarzeń i metod klientów z sygnaturami C#); w C3a **krok szeregowy** przed writerami modułów tworzy te pliki w `Shared.Abstractions` (jedyny właściciel zapisu do tego katalogu = ten krok, nie writerzy modułów); test architektury sprawdza, że każde zdarzenie ma ≥1 publisher i ≥1 handler (brak sierot).
- **Paczka G2 (co widzi właściciel).** Jeden ekran, w tej kolejności: (1) decyzje z koszyka ZAPYTAJ — każda w formacie D-2, do odpowiedzi; (2) lista założeń ZAŁÓŻ — jedno zdanie + konsekwencja, milczenie = zgoda; (3) **diff endpointów** z `contract-next` vs baza (nie surowy overlay); (4) szkic UI; (5) link do `spec.md`/`plan.md` jako załącznik, nie treść. `tasks.md` nie jest pokazywany.
- **Budżet story.** `state.budget_tokens` jako twardy limit: dyrygent nie startuje kolejnej fazy po przekroczeniu, otwiera paczkę eskalacyjną (kontynuuj / przyjmij z długiem / porzuć). Wartość do ustalenia z właścicielem po pierwszych 2–3 stories z metrykami.
- **Gdzie żyje kod między G1 a G3.** Przy D-8 (jedna story naraz): gałąź `NNN-slug` w drzewie roboczym; worktree niepotrzebny. Porzucenie story = `git branch -D` + usunięcie `runs/NNN-slug/` + zamknięcie issue z etykietą; migracja nigdy nie została zastosowana poza lokalną bazą.

## 8. Tory pracy

| Tor | Kiedy (decyduje skrypt po `blast_radius`) | Co pomija |
|---|---|---|
| hotfix | jeden plik, bez kontraktu/authz/schematu | wszystko — edycja wprost w sesji |
| średni | zmiana bez UI i bez kontraktu (refaktor BE, wewnętrzna logika) | sesję A, krytyków, contract-author, fe-writer; jeden reviewer |
| pełny | dotyka kontraktu, authz, schematu DB lub UI | nic |

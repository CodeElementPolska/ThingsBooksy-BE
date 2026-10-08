# Nowa sesja: fe-writer v0 (pod story 016)

Opis dla NOWEJ sesji, która buduje agenta `fe-writer` w wersji minimalnej. Decyzja właściciela z 2026-10-05: kod produkcyjny frontendu w story 016 (T029–T031) pisze `fe-writer`, nie dyrygent ręcznie.

## Jak uruchomić
PowerShell, katalog repo, gałąź `016-rename-resource-schema`, zwykła sesja bez persony (`claude`). Pierwsza wiadomość: „Przeczytaj `docs/agent-fleet-v4/HANDOFF.md` i `docs/agent-fleet-v4/next-session-fe-writer.md`, przedstaw plan, wykonaj po mojej zgodzie.”

**Kiedy:** przed startem sesji C story 016 i przed `baseline.js`. Powód: sesja zmienia `fleet-acl.json`, który hook czyta przy każdym wywołaniu narzędzia we wszystkich sesjach — w tym czasie nie może działać żadna inna sesja Claude na tym drzewie. Sprawdź to na starcie (lista procesów `claude.exe`) i zapytaj właściciela, jeśli jakaś żyje.

## Zasady pracy
- Najpierw plan do zgody właściciela; commity tylko po wyraźnym „tak”; `git add` tylko wskazanych ścieżek; `.claude/settings.json` nie wchodzi do commita floty.
- Pętla plan → krytycy → implementacja → krytycy obowiązuje dla zmian w `gate.js` i dla wpisu ACL (jeden krytyk wystarczy); plik agenta, szablony i dokumentacja idą bez niej.
- Nie dotykać `runs/016-*/` ani `specs/016-*/` poza odczytem. Nie uruchamiać `dotnet ef database update`.
- Pliki agenta i szablony po angielsku, dokumentacja po polsku.
- Po utworzeniu pliku agenta poprosić właściciela o dowolną wiadomość, zanim padnie pierwsze wywołanie `Agent` (HANDOFF, nawyk 1).

## Co obowiązuje (decyzje właściciela, nie otwierać ponownie)
- `fe-writer` powstaje teraz, PRZED fail-closed w hooku ACL (odwrócenie kolejności z 2026-10-01). Fail-closed zostaje na po G3 016. Skutek: `fe-writer` musi mieć wpis w `fleet-acl.json` od pierwszej chwili — `prompt-builder` i `npm run acl:test` i tak tego wymagają.
- Testy frontendu pisze `test-designer` w fazie testów ślepych (T021), nie `fe-writer`. `fe-writer` nigdy nie edytuje `*.spec.ts`.
- Dowód „czerwonych” testów frontendu dla 016 robi dyrygent: po T021 uruchamia `npm test` i zapisuje w journalu `NOTE`, że testy `[AC-8]` padają. `red-first-prover --fe` nie jest naprawiany w tej sesji.
- Hash testów (`gate`, krok `test-hash`) zostaje bez zmian. Obejmuje wszystkie pliki `.spec.ts`, więc każda edycja speca przez `fe-writer` po fazie testów ślepych wywraca gate — to jest strażnik zakazu z poprzedniego punktu.

## Zakres v0
1. **`.claude/agents/fe-writer.md`** na wzór `.claude/agents/be-writer.md` (ten sam kształt front matter: `model`, `effort`, `tools`, `omitClaudeMd`, `maxTurns`). Reguły: tylko kod produkcyjny frontendu; nigdy backend, nigdy `*.spec.ts`, nigdy `frontend/src/app/api/` (generowane); konwencje `.claude/conventions/angular-*.md` są wiążące; test sprzeczny ze specyfikacją → `DISPUTE_TEST`; decyzja należąca do właściciela → `BLOCKED_ON_DECISION`; wynik = JSON `result`.
2. **`tools/fleet/templates/fe-writer-behaviour.md`** na wzór `be-writer-behaviour.md` (`agent: fe-writer`, faza C3b). Wejścia: spec, plan, tasks, `data-model.md`, `runs/{story}/discovery/ui-sketch.md`, `contract-next.json`, decyzje, założenia, konwencje Angular; testy FE do odczytu.
3. **Wpis `fe-writer` w `tools/fleet/fleet-acl.json`.**
   - odczyt: `frontend`, `specs`, `runs`, `generated`, `.claude/conventions` (plus `runs/*/prompts`, jeśli `runs` go nie pokrywa — sprawdzić testem „agent czyta własny prompt”);
   - zapis: `frontend/src/app/features`, `frontend/src/app/shared`, `frontend/src/app/core`, `runs/*/impl`;
   - shell: `npm --prefix frontend run build`, `npm --prefix frontend test`, `node tools/fleet/validate.js`, `git diff`, `git status`. Hook dzieli komendy po `|` i `&&` i dopasowuje prefiksy, więc `cd frontend && …` nie przejdzie.
4. **`gate.js`: kroki `fe-build` i `fe-test`** po kroku `tests`. Uruchamiane, gdy story ma `touches_ui: true` ALBO od baseline są zmiany pod `frontend/`; w przeciwnym razie SKIPPED. Sam warunek „zmiany pod `frontend/`” byłby fail-open dla story, która powinna zmienić UI, a nie zmieniła.
5. **Testy ACL** w `acl-hook.test.js`: `fe-writer` czyta `frontend` i własny prompt, pisze w `features/`, dostaje odmowę zapisu do `backend/`, `specs/` i `frontend/src/app/api/`, odmowę `dotnet`.
6. **Dokumentacja:** `runbook-delivery.md` (C3b: `be-writer` ∥ `fe-writer`; dowód czerwonych testów FE przez dyrygenta; kroki FE w gate), wiersz agenta i wiersz dziennika w `tools/fleet/README.md`, linia stanu w `HANDOFF.md`.

## Poza zakresem v0
Fail-closed w hooku ACL i `write_deny`; naprawa `red-first-prover --fe`; shell FE dla `test-designer`; skrypt generatora klienta; lint FE; dynamiczny zakres zapisu per feature; krok „świeżość klienta TS” w gate; `spec-critic`; `/deliver`.

## Fakty ustalone (nie odkrywać ponownie)
- Frontend: Angular 21, standalone, signals; testy przez `ng test` (`@angular/build:unit-test`), nie przez goły `vitest`. `ng test` ma `--filter <regex>` po nazwach testów i `--watch`, które poza terminalem interaktywnym domyślnie jest wyłączone.
- `frontend/package.json` ma skrypty `ng`, `start`, `build`, `watch`, `test`. Nie ma `lint` (CLAUDE.md twierdzi inaczej) ani skryptu generatora.
- Generator: `swagger-typescript-api` 13.13.0; składnia to `swagger-typescript-api generate …`. Komenda z `.claude/conventions/angular-folder-structure.md:125` nie ma słowa `generate` — do sprawdzenia przed użyciem; nie była uruchamiana.
- W 016 serwis `features/groups/services/resources-api.service.ts` używa `HttpClient` z literałami adresów i importuje tylko typy z `api/data-contracts.ts`; klasy `api/Resources.ts` nikt nie importuje (research.md R6).
- `test-designer` ma już w ACL odczyt i zapis `frontend/src/app/**/*.spec.ts`; nie ma komendy shell dla frontendu. Reviewerzy C5 i `architecture-guard` już czytają `frontend/src`.
- `prompt-builder` odmawia: agentowi bez wpisu w ACL, gdy `agent:` szablonu różni się od `--agent`, gdy wejście lub konwencja jest poza `read_allow`, oraz przy nadpisaniu promptu bez `--instance`/`--force`.
- Etykiety AC zamkniętej story 015 mają postać `015/AC-n` (sposób A, 2026-10-05), więc filtr `AC=AC-n` widzi tylko testy bieżącej story.

## Zadania 016, które dostanie fe-writer
`specs/016-rename-resource-schema/tasks.md`, faza 7: T029, T030, T031, a na koniec T033 (build i testy zielone, w tym `[AC-8]`). Mapa nazw: tabela „Frontend” w `data-model.md`. Trzy teksty: spec.md AC-8 (DEC-4). T034 to przejście właściciela po ekranach.

## Do decyzji właściciela na etapie planu
1. **T032 — dwa pliki generowane (`api/Resources.ts`, `api/data-contracts.ts`).** Rekomendacja: dyrygent uruchamia generator na swaggerze po zmianie backendu; `fe-writer` nie ma zapisu do `api/`. Alternatywa z tasks.md (ASM-15): poprawka ręczna samych nazw, jeśli generator zmienia układ plików.
2. **Szablon `fe-writer-fix.md` (naprawy po review C5).** Rekomendacja: zrobić od razu jako kopię `be-writer-fix.md` ze ścieżkami frontendu; bez niego uwagi reviewerów do kodu FE poprawia dyrygent ręcznie.
3. **Zakres zapisu.** Rekomendacja: całe `features/`, `shared/`, `core/` (jak wyżej). Węższy zakres per feature wymaga zmiennej w ACL, której hook dziś nie zna.

## Weryfikacja przed commitem
- `npm run acl:test` i `npm run migration:test` zielone (w `tools/fleet`).
- `prompt-builder` buduje prompt `fe-writer` na syntetycznej story (nie na 016).
- `gate.js` na syntetycznej story: kroki FE przechodzą na bieżącym drzewie, są SKIPPED bez `touches_ui` i bez zmian FE, a celowo zepsuty test FE daje RED.
- Przebieg próbny `fe-writer` na zabawkowym zadaniu poza 016; wynik zwalidowany `validate.js --schema result`, `files_changed` porównane z `git status -- backend frontend`; po próbie drzewo przywrócone.

## Zakończenie
Commit `feat(fleet): fe-writer v0` po „tak” właściciela. Następny krok: sesja C story 016 według `runbook-delivery.md`.

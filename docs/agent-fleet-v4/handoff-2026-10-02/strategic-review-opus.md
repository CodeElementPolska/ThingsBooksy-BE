# Przegląd strategiczny — agent Opus (2026-10-02, niezależny, read-only)

## Czy idziemy w dobrym kierunku?
**Werdykt: kierunek techniczny dobry, proporcje złe.** Flota buduje się głównie sama, a story stoi w miejscu.
- Poprawki z 247acc0 idą do przyczyny (usunięcie `.specify/feature.json`; odmowa w `prompt-builder` zamyka klasę błędów ACL). Oba defekty w 016 to nawroty punktowych poprawek z 015 — lekcja dobra, wykonanie poprawne.
- Koszt przeglądu dominuje nad dostawą: ok. 15–16 przebiegów krytyków w 3 dni → 247acc0 (+63/−25), niezacommitowany migration-check (328+191 nowych linii, +89/−20 w 10 plikach), 0 linii kodu produktu. Mniej więcej jeden krytyk na 40 linii kodu floty.
- Story 016 nie ruszyła od 2026-09-30 13:55 (`runs/016-*/discovery/` ma tylko `questions/`).
- Substrat rośnie (28 skryptów, 2518 linii, 2 pliki testów), ale rośnie też dług w prozie: `HANDOFF.md` ma ok. 8 nieaktualnych stwierdzeń (:1 data, :6 gałęzie ai/014, :8 „~25 wpisów” vs 39, :9 „sesja A nieprzetestowana”, :10 start z 015, :12 lista skreślonych agentów, :13 „nie istnieje: sesja A, C5, C6”, :37 „wkrótce testy” dla 015).
- Dziennik defektów zbiega się tylko częściowo: klasa CRLF wróciła w 015 czterokrotnie, zanim zamknął ją `hash.js`; 016 ma 4 wiersze (2 nawroty) przed jakąkolwiek fazą dostawy. Za mało danych na ocenę zbieżności.

## Trzy decyzje
**Decyzja 1: ani (a), ani (b). Rozdzielić ACL od fe-writera.** Fail-closed ACL to mała zmiana (`acl-hook.js:32-33`, `base_read_allow`, test statyczny „każdy plik w `.claude/agents` ma wpis albo jest na liście `unrestricted`”) — robić w osobnym worktree i wmergować, gdy sesja B jest bezczynna. Ryzyko „żywej infrastruktury” jest realne, ale inne: hook przy nieczytelnym `fleet-acl.json` odmawia wszystkim agentom floty — groźny jest zapis w trakcie edycji, nie zmiana semantyki. **fe-writer nie jest potrzebny dla 016**: zmiana na froncie to regeneracja klienta (`api/Resources.ts`, `data-contracts.ts`) i mechaniczna zmiana nazw w ok. 8 plikach `features/groups|schemas`, w tym 1 `.spec.ts` — wystarczy zadanie dyrygenta (za zgodą właściciela) i jednorazowy REBASELINE hasha testów. fe-writera budować na pierwszej story z prawdziwym nowym UI; wtedy pytania z decyzji 3 będą miały dane. Do sprawdzenia: czy sesja B 016 jeszcze żyje (artefakty stoją od 2 dni).

**Decyzja 2: tak, wyrzucić próg 110k.** Nie budować nowych liczników: `decisions.jsonl` już ma `phase` i `asked_by` — wystarczy, że `closer.js` je zagreguje. Poprawić przesłankę: DEC-6 nie był wykrywalny na etapie spec (3 z 4 były); wszystkie cztery były tanimi pytaniami, nie przeróbkami; „~30% tokenów” niepotwierdzone. Spec-critic przy n=1 to hipoteza, nie decyzja do D-16.

**Decyzja 3: tak, odłożyć — ale do pierwszej story z nowym UI.** Dla 016 zostaje jedno: kto edytuje te ok. 10 plików frontendu.

## Największe ryzyko, którego nikt nie nazwał
1. **Najtańsza story niesie najcięższy proces.** 016 to rename bez danych i bez zewnętrznych klientów; AC-6, AC-7, AC-13 da się sprawdzić grepem. Mimo to: 14 AC, 4 bramki, nośnik budowy fe-writera, G2b z sha i spec-critica. G2b dla 016 na pewno da DESTRUCTIVE, a właściciel już zdecydował o wymazaniu danych w AC-9 — ceremonia każe mu zatwierdzać to samo drugi raz.
2. **„LLM ocenia LLM” na kodzie floty prowadzi do przeuczenia.** Krytycy wymyślają przypadki, każdy staje się kodem (UTF-16, expression-bodied, sealed — EF ich nie generuje). Wszystko dla 8 migracji, z których przy A1 i tak 3 idą do G2b. Prostsza reguła „każda nowa migracja → G2b z diffem” kosztuje minutę uwagi właściciela, nie 519 linii.
3. **Przekazanie do nowej sesji gubi kontekst.** Nowa sesja zacznie od HANDOFF z ~8 nieaktualnymi zdaniami, odziedziczy regułę „≥2 krytyków przed i po każdej edycji”, a w drzewie leży niezwiązana zmiana `.claude/settings.json` (`enabledPlugins`).

## Co zrobić w nowej sesji jako pierwsze
1. Porządek: sprawdzić, czy sesja B 016 żyje; za zgodą zacommitować migration-check bez `settings.json`; przepisać `HANDOFF.md` do ~15 linii samego stanu, historię do dziennika.
2. Fail-closed ACL w worktree: hook, `base_read_allow`, test statyczny, smoke S10. Najwyżej jeden krytyk (kod bezpieczeństwa).
3. Dowieźć 016 do G3 bez nowych agentów: FE robi dyrygent, G2b zamyka się raz. Potem porównać `metrics.json` z 015 i dopiero decydować o fe-writerze i spec-criticu.

## Czego NIE robić
- Nie budować fe-writera, spec-critica ani `/deliver`, zanim 016 nie przejdzie G3.
- Nie stosować pełnej pętli z krytykami do każdej edycji; skalować według ryzyka: hook ACL i bramki tak, dokumentacja i prompty nie.
- Nie rozbudowywać migration-check (Testcontainers, analiza `Sql`).
- Nie wpisywać do D-16 prozy z kryteriami „utrzymać/wyciąć”, których nikt nie liczy — kryterium ma być wynikiem `closer.js`.

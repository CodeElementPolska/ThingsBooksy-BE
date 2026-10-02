# Przegląd strategiczny — agent Fable (2026-10-02, niezależny, read-only)

## Czy idziemy w dobrym kierunku?
**Werdykt: kierunek dobry, tempo i proporcje złe.** Poprawki są root-cause (usunięcie `feature.json`, fail-closed w `prompt-builder`, test szablon→ACL zamykają klasy). Ale dziennik (39 wierszy) pokazuje wzorzec: klasa CRLF/hash wracała 4 razy, zanim powstał `hash.js`; oba nawroty z 016 to skutki „poprawek prozą/częściowym ACL”. Zbieżność nieudowodniona — sesja B 016 praktycznie nie ruszyła. Koszt przeglądu dominuje: 3 dni, 0 linii produktu, ~700 linii floty, ~15 krytyków. Na 015 C5 zjadło ~805k z 1378k tokenów (56%) dla 1631 linii backendu. Dług procesowy rośnie szybciej niż substrat: HANDOFF ≥8 nieaktualnych zdań, `workflow.md:133-147` listuje 5 agentów, których nie będzie, `:187` hook „w frontmatterze”, `runbook-delivery.md:3,40` „brak reviewerów”. Zapisanie jednej decyzji D-16 wymaga planu 64 linii w 7 plikach — dokumentacja jest ręcznie replikowanym stanem.

## Trzy decyzje
**Decyzja 1 — rozdzielić, nie łączyć.** Fail-closed zmienia hook czytany przy każdym wywołaniu narzędzia we wszystkich sesjach — robić, gdy ŻADNA sesja story nie działa (po zamknięciu 016, przed B 017). fe-writer dla 016 potrzebny w wersji minimalnej: FE 016 to regeneracja klienta + 1 serwis (15 odwołań do `resources/types` w 2 plikach: `api/Resources.ts`, `features/groups/services/resources-api.service.ts`). Najpierw dokończyć B do G2, potem jedna krótka sesja „fe-writer v0” (plik agenta, 1 szablon, wpis ACL, kroki `fe-build`/`fe-test` w gate). Argument „fe-writer musi istnieć przed C2, bo tasks.md go przypisuje” jest słaby: tasks.md może przypisać zadania agentowi, który powstanie później.

**Decyzja 2 — tak, i dalej.** 110k niemierzalne (sesja B nie loguje AGENT_END) i niezatwierdzone. Kryterium „≥½ decyzji, które inaczej zapadłyby w C5/C6” jest równie niemierzalne (kontrfaktyczne). Mierzalny jest tylko udział decyzji po G2 (`decisions.jsonl` ma `phase`; 015: 4/9) — pięć linii w `closer.js`. Przy n=2 to osąd, nie pomiar; zapisać „oceniamy po 017”, bez progów.

**Decyzja 3 — pół na pół.** Trzy z sześciu pytań to fakty do ustalenia dziś w 10 minut: komenda generatora (`swagger-typescript-api` w devDeps, skryptu brak), lint nie istnieje (poprawić jedną linię CLAUDE.md), klient dla 016 ze swaggera po BE (sekwencyjnie). Jedno nie jest pytaniem fe-writera, lecz defektem gate'a: test-hash obejmuje wszystkie 39 `.spec.ts`, więc edycja mocka wywraca gate bez REBASELINE — rozstrzygnąć przed C2.

## Największe ryzyko
1. **Flota wyparła produkt.** Ostatni artefakt B 016: 2026-09-30 13:55; `specs/016-*` nie istnieje; zero faktów w discovery. Story o 12 AC utknęła, a plan dokłada przed nią trzy budowy.
2. **G2b z wyjątku staje się rutyną.** Golden set: 3 z 8 migracji = REVIEW, a od A1 REVIEW otwiera G2b — bramka na większości story, z sha8 w etykiecie i regexem na odpowiedź. Przewidywalny skutek: właściciel klika „Akceptuję” odruchowo, a 4 sprzężone skrypty pilnują teatru. Bramek przybywa: G1, G2, G2b, G3, DEC w C5, UNSPECIFIED w G3, repliki krytyków w A, pytania spec-critica, meta-pętla plan→krytycy.
3. **Handoff = utrata kontekstu.** Prawdziwy stan leżał w Temp; spec-critic to kolejna warstwa „LLM ocenia LLM” wbrew D-3, dokładana zanim zmierzono, czy C5 (56% tokenów) da się odchudzić.

## Co zrobić w nowej sesji jako pierwsze
1. Zacommitować migration-check (za zgodą) i jedną krótką aktualizację HANDOFF — tylko fakty o stanie, bez D-16 w 7 plikach; esencja planów i ustaleń do repo (zrobione: `handoff-2026-10-02/`).
2. Doprowadzić 016 do G2 (wznowić lub powtórzyć sesję B). Zero pracy nad flotą, dopóki nie ma `specs/016-rename-resource-schema/`.
3. fe-writer v0 + kroki FE w gate + decyzja o test-hash dla FE — minimum pod C2 016; fail-closed ACL i spec-critic dopiero po G3 016.

## Czego NIE robić
- Nie uruchamiać fail-closed ACL w drzewie, w którym działa inna sesja Claude (hook jest wspólny).
- Nie wpisywać do D-16 progów liczbowych (110k, „≥½”).
- Nie stosować pętli „plan → 2 krytyków → implementacja → 2 krytyków” do zmian poniżej ~100 linii ani do dokumentacji; to reguła dla hooka i gate'a.
- Nie budować spec-critica, zanim `closer.js` nie policzy decyzji po fazie na 016.

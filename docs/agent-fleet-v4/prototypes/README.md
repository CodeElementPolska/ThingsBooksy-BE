# Prototypy zweryfikowane 2026-09-24

Kopie plików użytych w prototypach z sekcji 7 `workflow.md`. Ścieżki wewnątrz są absolutne i wskazują na scratchpad sesji — przy budowie substratu przenieść do `.claude/hooks/` i sparametryzować.

| Plik | Co udowodnił |
|---|---|
| `acl-hook.js` | hook `PreToolUse` w modelu allowlist, fail-closed; kanonizacja ścieżek przez `fs.realpathSync.native`; Grep/Glob bez `path` = całe repo = odmowa. 18/18 wariantów standalone (`acl-hook.cases.js`), 10/10 w harnessie (subagent i `--agent`). |
| `log-stdin-hook.js` | zrzut surowego stdin hooka; użyty do potwierdzenia, że `PostToolUse(AskUserQuestion)` niesie `tool_response.answers`. |
| `agent-blind.md.example` | frontmatter agenta ślepego: `omitClaudeMd`, jawna lista narzędzi (bez powłoki, bez Agent/Skill), hook z frontmatteru, `maxTurns`. |
| `agent-persona.md.example` | frontmatter persony dla `claude --agent`: allowlista egzekwowana, `AskUserQuestion` dostępne, hook `PostToolUse`. Brakuje `initialPrompt:` — dodać w wersji docelowej. |

Wnioski zbiorcze: sekcja 7 w `workflow.md`.

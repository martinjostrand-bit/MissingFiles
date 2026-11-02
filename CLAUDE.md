# MissingFiles

Windows desktop app and CLI that find source files missing from a destination folder, and copy them.

`MissingFiles_specification_v2.md` is the source of truth for requirements, work packages (§2) and decisions (§7). Refer to it by ID (REQ-, NFR-, FT-, TC-, DEV-, CM-) and do not repeat it here. If code and specification disagree, raise it with the user instead of choosing silently.

## Commands

```powershell
dotnet build                          # warnings are errors
dotnet test
dotnet format --verify-no-changes
```

CI (`.github/workflows/ci.yml`) also builds with `-c Release` and requires at least 80 % line coverage on `MissingFiles.Core`. The performance test (FT-10) runs only when `MISSINGFILES_PERF=1` is set.

## Conventions

- `MissingFiles.Core` holds all logic and has no UI dependencies. `MissingFiles.Cli` and `MissingFiles.App` (WPF, MVVM) stay thin.
- Never add code that deletes, moves or overwrites user files (NFR-05). The source is opened read-only (NFR-06).
- No network access or telemetry (NFR-12). UI, CLI output, logs and documentation are in English (NFR-07).
- Use `Path.Join`, not `Path.Combine` (CodeQL `cs/path-combine`).
- Files use CRLF on checkout (`.gitattributes`). Never commit `bin/` or `obj/`.
- Match the style of the surrounding code, and name requirement IDs in tests where the existing tests do.

## Roles

| To do this | Use |
|---|---|
| Implement a work package | `/implement-wp <WP>` |
| Verify the current change | `/verify [<WP>]` |
| Check new requirements | The `spec-reviewer` agent |
| Check CI for the current commit | `/ci-status` |
| Publish a release | `/release` |

Whoever writes a change does not verify it (DEV-04). Do not fix findings inside a verification run, and do not summarise your reasoning to the `verifier`.

## Definition of done (DEV-05)

1. `dotnet build` has no errors or warnings, and `dotnet test` passes.
2. The `verifier` verdict is PASS.
3. The commit message has a short, descriptive subject and includes the trailer `Verified: PASS`.
4. Commit and push to `main` using the GitHub account `martinjostrand-bit` (§3.1). CI is the final gate, so check it with `/ci-status`.

Never write `Verified: PASS` without a PASS verdict for that exact change. If the change was edited after the verdict, verify it again.

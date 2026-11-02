---
name: implement-wp
description: Implement one MissingFiles work package with the implement, verify, fix loop (DEV-06), then commit and push.
argument-hint: <WP>
arguments: [wp]
disable-model-invocation: true
effort: high
---

Implement work package $wp from §2 of `MissingFiles_specification_v2.md`. Handle only this work package.

## Before starting

Check that the working tree is clean and that the dependencies of $wp in §2 are done. If not, stop and tell the user.

## Loop

Allow at most 3 verification rounds.

1. **Implement.** Start the `developer` agent with the work package ID, and ask it to include the `README.md` status line if the work package will be done. In later rounds, send it the verifier's findings exactly as written.
2. **Verify.** Run `/verify $wp`. It always starts a new `verifier`. Do not pass on anything from the developer.
3. **Decide.**
   - PASS: go to "Finish".
   - FAIL or INVALID, with rounds left: go back to step 1.
   - FAIL or INVALID after round 3: stop, leave the changes uncommitted, and report the remaining findings to the user.

Do not edit the code yourself between rounds, because that change would skip the developer step. If the developer reports a finding as wrong or the specification as unclear, stop and ask the user.

## Finish

Commit exactly what was verified. Any further edit needs a new verification round.

1. Commit using the rules in `CLAUDE.md`, with the trailer `Verified: PASS`, and push.
2. Run `/ci-status`.
3. Report the verdict, the commit, and the CI result.

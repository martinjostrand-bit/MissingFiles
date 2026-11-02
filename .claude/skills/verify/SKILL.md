---
name: verify
description: Run an independent verifier on the current uncommitted MissingFiles change (DEV-04) and report its verdict.
argument-hint: "[WP]"
arguments: [wp]
effort: high
---

Run an independent verification of the current uncommitted change. Work package: "$wp" (may be empty).

Do these steps exactly. You run the verification; you do not perform it, fix anything, or add your own opinion of the change.

1. Record the repository state. Keep the printed value:

   ```bash
   { git rev-parse HEAD; git status --porcelain; git diff HEAD; git ls-files --others --exclude-standard -z | xargs -0 -r sha256sum; } 2>/dev/null | sha256sum
   ```

2. Check whether the change touches the copy engine:

   ```bash
   git status --porcelain -- src/MissingFiles.Core/Copying
   ```

3. Start the `verifier` agent in the foreground.
   - The prompt is only `Verify work package <WP>.`, or `Verify the current change.` if there is no work package. Add nothing else: no reasoning, summary, test results or hints.
   - If step 2 printed anything, set `effort` to `xhigh`. Otherwise leave it out.

4. Run the command from step 1 again and compare the values. If they differ, the result is **INVALID**: tell the user that the verifier changed the repository, and show `git status --short`.

5. Otherwise, report the verifier's verdict exactly as written, including every finding. If the verdict is not in the expected format, treat it as FAIL.

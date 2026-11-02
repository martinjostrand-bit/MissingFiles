---
name: ci-status
description: Report the result of the latest CI and CodeQL runs for the current MissingFiles commit.
effort: low
allowed-tools: Bash(git rev-parse*), Bash(gh run list*), Bash(gh run view*)
---

1. List the runs for the current commit:

   ```bash
   gh run list --commit "$(git rev-parse HEAD)" --json databaseId,workflowName,status,conclusion,url
   ```

2. Report each workflow as passed, failed or still running. If there are no runs yet, say so.
3. For a failed run, show the failing step from `gh run view <id> --log-failed`, limited to the relevant lines.

Report the state once. Do not wait for runs to finish unless the user asks.

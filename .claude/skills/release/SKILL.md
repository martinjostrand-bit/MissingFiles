---
name: release
description: Publish a MissingFiles release to GitHub Releases from a passing CI build (CM-06).
disable-model-invocation: true
effort: medium
---

Publish a release according to CM-06 and NFR-10. A release is public, so get the user's confirmation at step 4 before tagging or publishing.

1. Check the prerequisites, and stop if any fails:
   - The current branch is `main`, the working tree is clean, and `HEAD` matches `origin/main`.
   - The CI workflow in `.github/workflows/ci.yml` sets `BB` from `GITHUB_RUN_NUMBER` (WP10), and `LICENSE` exists (WP11).
   - The UX checklist in §6.5 has been done for this release. Ask the user.
   - Secret scanning and push protection are enabled (DEV-07). Using the account in §3.1, run both commands. Stop unless each prints exactly `enabled`; any other output, including empty output or `null`, stops the release:

     ```bash
     gh api repos/martinjostrand-bit/MissingFiles --jq .security_and_analysis.secret_scanning.status
     gh api repos/martinjostrand-bit/MissingFiles --jq .security_and_analysis.secret_scanning_push_protection.status
     ```

2. Find the CI run for `HEAD`, and stop unless it succeeded:

   ```bash
   gh run list --workflow CI --commit "$(git rev-parse HEAD)" --json databaseId,number,conclusion
   ```

3. Work out the revision: `XX.YY.ZZ` from `Directory.Build.props` and `BB` from the run's `number`. Compare it with the latest tag from `git tag --list 'v*'`. Check that the increase matches CM-01 and CM-03, and that the tag doesn't exist yet.
4. Show the user the revision, the commit and the release contents, and ask for confirmation.
5. Download the artifact to the scratchpad, into a new empty directory:

   ```bash
   gh run download <run-id> --name MissingFiles-win-x64 --dir <dir>
   ```

6. Create `MissingFiles-XX.YY.ZZ.BB-win-x64.zip` with `MissingFiles.exe`, `MissingFiles.Cli.exe`, `FileTypes.default.json` and `LICENSE`. Check that the file version of both executables is `XX.YY.ZZ.BB`.
7. Tag and publish:

   ```bash
   git tag vXX.YY.ZZ.BB && git push origin vXX.YY.ZZ.BB
   gh release create vXX.YY.ZZ.BB <zip> --title "MissingFiles XX.YY.ZZ.BB" --generate-notes
   ```

8. Report the release URL.

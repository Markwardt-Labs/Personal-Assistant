# Project

Documentation for this repository's own tooling and workflow.

## Scripts

`Scripts/` holds file-based C# apps (`dotnet run Scripts/<Name>.cs`, from the root) and tasks for automated repository actions. Only what a developer runs by hand is in the folder itself; what those are built from is in `Scripts/Support/`.

- `Scripts/Run.task` - runs the dev server and deploys the Android client to an emulator. A `.task` file lists scripts, one per line, and the scripts of a task run together.
- `Scripts/Publish.cs` - cuts a release (see Publishing below). A manual, human-only action.
- `Scripts/Support/` - the scripts the tasks above are built from, which are not run directly, and `Data/`, the git-ignored state they keep:
  - `Server.cs` runs the server with its app folder under `Scripts/Support/Data/` (through the `--data-folder` argument) and a config rewritten on each start (port 52680, access key `dev`; reachable from the emulator at `10.0.2.2`); the settings and everything else the server keeps stay there between runs.
  - `Client.cs` deploys the Android client (`Client/Mobile/`) to an emulator, starting the first AVD if no device is attached, launches it and streams its log until interrupted. An emulator the script started is stopped when the script ends; an already attached device is left running.

## Publishing

Run `Scripts/Publish.cs` locally to cut a release:

1. It prompts for the version to publish (e.g. `1.2.3`) - `Server/Server.csproj` carries no `<Version>` of its
   own, so this is what actually gets built and published.
2. It creates the GitHub Release (and its underlying tag) for that version locally via `gh release
   create` - done locally because a repo ruleset blocks the default `GITHUB_TOKEN` from creating tags.
3. It dispatches `build.yml`'s `workflow_dispatch` trigger with the version as input. The workflow
   verifies the dispatcher has Admin permission on the repo, refuses to run from anything but `main`,
   publishes the server (Windows and Linux x64) as a self-contained single-file
   executable and the Android client as an APK signed with the release keystore (repository secrets `ANDROID_KEYSTORE_BASE64`, the PKCS12 keystore base64-encoded with alias `personalassistant`, and `ANDROID_KEYSTORE_PASSWORD`, used for both store and key; losing the keystore means installed copies must be uninstalled before a differently signed update), and uploads them alongside `THIRD-PARTY-NOTICES.txt` as release assets. The asset names (`PersonalAssistant-Server-linux`, `PersonalAssistant-Server-win.exe`, `PersonalAssistant-android.apk`) and the plain version tag are what the self-update of installed copies relies on, so renaming either breaks it.
4. It waits for that run to finish, rolling the release/tag back if the workflow fails, so a failed
   publish never leaves one behind. If it can't confirm the run happened at all, it leaves the release
   in place instead, rather than risk deleting one that's still running.

## Workflows

- `.github/workflows/build.yml` - builds (with the Android workload), verifies formatting (`dotnet format --verify-no-changes`,
  never applies fixes), and runs tests on every push/PR to `main`; its `publish` job (see Publishing
  above) only runs on `workflow_dispatch`, gated on the dispatcher having Admin permission on the repo
  and the run being on `main`.
- `.github/workflows/codeql.yml` - CodeQL security analysis on push/PR to `main` and a weekly schedule.

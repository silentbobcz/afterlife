# AFTERLIFE: notes for AI coding sessions

Mod manager for Cyberpunk 2077. C# .NET 10, WPF + CommunityToolkit.Mvvm, SQLite, xUnit. The map of the code is
`docs/HOW-IT-WORKS.md`; read it first, then search for the symbol you need instead of reading large files whole.

## Work queue

The current batch of work is in `FRONTA.md` (Czech); between batches the file does not exist, the newest state is
`main` and the next free package numbers are in the header of `UKOLY.md`. A session started with "read FRONTA.md and do
package O4" does exactly that package: read the header and the package, work without asking, write the handover
under the package and set its status.

What waits for the owner (things to try, decide or deliver) is in [`UKOLY.md`](UKOLY.md) (Czech). Update it at the end
of a session that added or settled something there.

## Branch, builds, releases

- The newest state is on the branch named at the top of `FRONTA.md` (normally `main`). Start your work branch from it,
  commit and push there. Pushing a work branch does not trigger GitHub Actions; a push to `main` does.
- Do not tag and do not change the version in `Directory.Build.props` unless the owner says so: releasing is the
  owner's call.
- Commit only your own files (`git add <file>`, not `-A`); another session may work in the same branch.

## Several sessions at once

Several sessions often work on this repository on the same day. Without these rules they switch branches under each
other and take the same task numbers.

- Never switch the branch of a shared clone and never leave it off `main`. Do your work in a folder of your own:
  `git worktree add <folder> -b <branch> origin/main`, and remove the worktree when the branch is merged.
- A branch lives hours, not days: finish, get it checked, merge into `main` (only when the owner says so), delete it on
  GitHub and locally. Before merging, bring `main` into the branch; `main` moves many times a day.
- Take numbers for `UKOLY.md` items and `FRONTA.md` packages at merge time, from the `main` of that moment, not when
  you start. Re-read both files right before you write to them; another session may have closed the batch meanwhile.
- When one session runs helpers on packages, each helper gets a working copy of its own, does not commit and does not
  write to `FRONTA.md` or `UKOLY.md`. The session that started them reads the real diff, runs the checks, writes the
  handovers and merges.
- Do not let an editor reformat the tables of `UKOLY.md` or `FRONTA.md`; a reformatted file blocks every merge.

## Checks (work in a Linux cloud session too)

```
apt-get install -y dotnet-sdk-10.0          # once per cloud session, if dotnet is missing
dotnet test tests/Afterlife.Tests
dotnet build src/Afterlife.App -p:EnableWindowsTargeting=true
```

The WPF window cannot run on Linux: what you could not see running goes to the handover as "not verified".

On the owner's PC there is no .NET 10 SDK. Push the work branch and start the checks on GitHub:
`gh workflow run build.yml --ref <branch>` (a pushed work branch alone starts nothing). A run started by hand also
makes a test build of the full edition, the artifact `AFTERLIFE-full-test`. Releases on GitHub carry the full edition
(installer and zip) and the AFTERLIFE Offline zip; Nexus Mods gets only the Offline zip, and Offline has no installer. The same run also carries the installer in `AFTERLIFE-full-test`.

## Rules

- The repository is public: code, comments, commits and UI text in English; no patch numbers, no model names.
- Do not touch the journaled deployment (`Deployment.cs`, `Deploy.cs`) or crash recovery without a reason in the package.
- Colours and fonts only from the tokens in `App.xaml`.
- Logs must stay free of personal details: write through `ActivityLog` or the window's error log (both filter with
  `LogPrivacy.Scrub`), never straight to a file.
- Every code package: one line in `CHANGELOG.md` under the topmost version heading; 1–3 lines in `docs/HOW-IT-WORKS.md` when the
  structure changes.
- Files over ~600 lines (C#) or ~800 (XAML) take no new code: add a file next to them.

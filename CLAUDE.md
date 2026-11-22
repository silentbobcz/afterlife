# AFTERLIFE: notes for AI coding sessions

Mod manager for Cyberpunk 2077. C# .NET 10, WPF + CommunityToolkit.Mvvm, SQLite, xUnit. The map of the code is
`docs/HOW-IT-WORKS.md`; read it first, then search for the symbol you need instead of reading large files whole.

## Work queue

The current batch of work is in [`FRONTA.md`](FRONTA.md) (Czech). A session started with "read FRONTA.md and do
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

## Checks (work in a Linux cloud session too)

```
apt-get install -y dotnet-sdk-10.0          # once per cloud session, if dotnet is missing
dotnet test tests/Afterlife.Tests
dotnet build src/Afterlife.App -p:EnableWindowsTargeting=true
```

The WPF window cannot run on Linux: what you could not see running goes to the handover as "not verified".

## Rules

- The repository is public: code, comments, commits and UI text in English; no patch numbers, no model names.
- Do not touch the journaled deployment (`Deployment.cs`, `Deploy.cs`) or crash recovery without a reason in the package.
- Colours and fonts only from the tokens in `App.xaml`.
- Logs must stay free of personal details: write through `ActivityLog` or the window's error log (both filter with
  `LogPrivacy.Scrub`), never straight to a file.
- Every code package: one line in `CHANGELOG.md` under the topmost version heading; 1–3 lines in `docs/HOW-IT-WORKS.md` when the
  structure changes.
- Files over ~600 lines (C#) or ~800 (XAML) take no new code: add a file next to them.

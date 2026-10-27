# Contributing

Thanks for helping. Bug reports with details are the most useful contribution right now.

## Reporting bugs

Use [Issues → New issue → Bug report](https://github.com/silentbobcz/afterlife/issues/new/choose) and include:

- the AFTERLIFE version (About) and Windows version,
- **Settings → Copy diagnostics**,
- the exported mod list (**Export list**),
- steps to reproduce, what you expected, what happened,
- the latest log from `%LOCALAPPDATA%\AFTERLIFE\logs`, if possible.

A mod that is imported wrongly? Link its Nexus page and file name.

## Ideas

Open a *Feature request*. Describe the problem you want solved, not only the solution.

## Pull requests

1. Open an issue first for anything bigger than a small fix, so we can agree on the approach.
2. Keep changes focused. One topic per pull request.
3. `dotnet build` with no new warnings and `dotnet test` green.
4. Logic changes in Core or Infrastructure need a test. Anything that writes to the game folder must stay crash-safe (see [docs/HOW-IT-WORKS.md](docs/HOW-IT-WORKS.md)) and must never touch files AFTERLIFE did not install.
5. UI texts are English, short and without developer jargon.

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).

## Code of conduct

Be decent. See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

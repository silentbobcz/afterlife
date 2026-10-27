# Building AFTERLIFE

## Requirements

- Windows 10 or 11 (the app is WPF; `Afterlife.Core`, `Afterlife.Infrastructure` and the tests also build and run on Linux)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Any editor: Visual Studio 2022/2026, Rider or VS Code with C# Dev Kit

## Build and test

```
git clone https://github.com/silentbobcz/afterlife.git
cd afterlife
dotnet build Afterlife.sln -c Release
dotnet test tests/Afterlife.Tests -c Release
```

The tests run against a fake game install in a temporary folder. They never touch a real game.

## Run

```
dotnet run --project src/Afterlife.App
```

Use a copy of the game or a test install while developing. A folder named `AFTERLIFE Library` next to the executable is used as the library automatically, which keeps development data apart from your normal library.

## Publish the single-file executable

```
dotnet publish src/Afterlife.App -c Release -r win-x64 --self-contained ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -o publish/app
```

The result is `publish/app/AFTERLIFE.exe` with .NET included. Release zips also contain `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.txt` and the font licenses from `src/Afterlife.App/Assets/Fonts`.

## Version

The version lives in `Directory.Build.props` (`<Version>`). It is shown in About and in *Copy diagnostics*.

## Continuous integration

`.github/workflows/build.yml` builds, tests and publishes on every push and pull request. Pushing a tag `v*` (for example `v0.5.0`) also creates a draft GitHub release with the zip and its SHA-256.

## Project layout

| Folder | |
|---|---|
| `src/Afterlife.Core` | Pure logic without I/O: model, game paths, import analysis, FOMOD, frameworks, planner |
| `src/Afterlife.Infrastructure` | SQLite store, library, deployment and recovery, coordinator, Nexus client, collections |
| `src/Afterlife.App` | WPF app, view models, styles, fonts |
| `tests/Afterlife.Tests` | xUnit tests with a fake game install and crash injection |
| `branding` | Logo and promo image generator (`gen_branding.py`, needs Python, cairosvg and the fonts) and the generated images |
| `docs` | User guide, FAQ, internals |

More about the internals: [HOW-IT-WORKS.md](HOW-IT-WORKS.md).

# AFTERLIFE: fronta pro modely (2. dávka)

Sestaveno 22. 11. 2026 po vydání 1.0.0-beta.1. **Nejnovější stav: větev `main`.** Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). První dávka je v `archiv/FRONTA-1-davka.md` (jen archiv, stav z ní nečti).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej balík O8.“

## Pravidla dávky

- Pracuj ve své pracovní větvi založené z `main` a odesílej do ní. Do `main` slučuje jen uživatelův pokyn; tag a vydání dělá uživatel.
- Sestavení pro uživatele jen na pokyn. Testy a kontrolní překlad si balík pouští sám.
- Co nešlo vidět běžet: v předávce „Neověřeno“ a uživateli položka A v `UKOLY.md`.
- Každý zápis do logu jde přes `ActivityLog` nebo chybový log okna, tedy přes `LogPrivacy.Scrub`; nic nepiš do souboru přímo.
- Předávka pod balíkem, nejvýš deset řádků.

## Stav balíků

| Balík | Model | Co | Čeká na | Stav |
|---|---|---|---|---|
| O8 | Opus | Přestavba nastavení v kódu podle schváleného náhledu | B3 | čeká na B3 |
| O9 | sub-Opus | Samostatný instalátor (Inno Setup), jen na GitHubu | nic | hotovo, zkontrolováno |
| F-kontrola | Fable | Kontrola O8, uzavření dávky (O9 zkontrolováno) | O8 | čeká |

## O8. Přestavba nastavení v kódu

Opus. Čeká na B3 (schválený náhled z O7). Soubory: `Views/Panels.xaml` (šablona nastavení do vlastního souboru ve `Views/`), `ViewModels/Overlays/SettingsVm.cs`. Logika voleb se nemění, jen se přesunou. Dvě nové drobnosti z náhledu: u každého druhu uložených souborů odkaz „Show“, který otevře jeho složku v Průzkumníku; u FIXERu rámeček „In development · not released yet“ s popisem, co FIXER je. Do alpha.11, pokud uživatel neřekne jinak.

Podklad z první dávky (předávka O7):

**O7** (21. 11., Opus)
- Stav: hotovo. Náhled `docs/previews/settings-tabs.html` (jeden soubor, bez kódu aplikace): klikací záložky, přepínač okna 1612 × 910 / 2560 × 1440 a edice FULL / OFFLINE, pod náhledem tabulka „kam se která volba přesunula“ a důvody.
- Záložky: 01 Game & library · 02 Nexus Mods (Account, Link mods, Download links) · 03 Updates · 04 Display · 05 Kept files · 06 Diagnostics · 07 FIXER; „Remove AFTERLIFE from this PC…“ dole pod seznamem záložek, oddělené.
- Rozhodl jsem sám: dialog má pevnou velikost 214 + 660 × 520 px a při větším okně neroste (nejdelší je Updates v Offline, 486 px); Game a Library v jedné záložce; v Offline je záložka Updates vidět se zámkem a zašedlými volbami (dnes je oddíl schovaný); na záložkách krátké údaje (součet Kept files, zelená tečka u funkčního klíče Nexusu); dole pod záložkami verze aplikace.
- Ověřeno: náhled v Chromiu, všechny záložky v obou edicích bez rolování.
- Pro O8: šablona do `Views/SettingsView.xaml`, výběr záložky v `SettingsVm` (zapamatovat poslední), texty beze změny kromě rozdělení dlouhých popisků na řádek + šedou poznámku.
- Doplněno 22. 11. podle zadání po F3: v Updates dva samostatné spínače (při startu, každou hodinu), „only switched-on“ zašedlé, když jsou oba vypnuté, a oddíl REQUESTS TO NEXUS MODS (dotazy denně a oranžové varování, v náhledu počítané stejným vzorcem jako `NexusLimits`). Upozornění u „Check every mod now…“ je jako v aplikaci jen v tooltipu.
- Pro uživatele: B3 v UKOLY.md.

## O9. Samostatný instalátor

Opus. Může začít hned. Rozhodnuto 22. 11.: instalátor se nahrává jen na GitHub k vydání, na Nexus Mods ne (každý nahraný soubor tam prochází kontrolou znovu a spustitelný instalátor by nejspíš šel k ruční). Na Nexusu zůstává jen zip. Dále: zip se složkou zůstává hlavní podobou, k jednomu samorozbalovacímu exe se nevracíme (nepodepsaný exe, který se rozbaluje do dočasné složky, zadržela ruční kontrola Nexusu). Instalátor je druhé stažení pro pohodlí.
1. Skript Inno Setup v `installer/` (uživatel ho už používá u NETWATCH): instalace pro jednoho uživatele bez práv správce, zástupce v nabídce Start, odinstalace. Knihovnu a nastavení uživatele instalátor ani odinstalace nemažou; na to je v aplikaci „Remove AFTERLIFE from this PC…“.
2. Krok ve workflow `build.yml`: instalátor se sestaví ze stejné složky jako zip a přibude k vydání se svým SHA-256. Zip zůstává.
3. `README.md` a `docs/USER-GUIDE.md`: dvě cesty instalace, instalátor s odkazem na GitHub Releases. `CHANGELOG.md`. Do předávky napiš větu pro popis na Nexusu s odkazem na instalátor a upozorni uživatele, ať si před zveřejněním ověří, že pravidla Nexusu takový odkaz dovolují.
4. Neověřeno bude: běh instalátoru ve Windows, hlášení SmartScreen u nepodepsaného souboru (položka A pro uživatele). Podpis kódu je samostatné rozhodnutí uživatele, sem nepatří.

## F-kontrola

Fable. Projít commity O8 a O9; O8 proti náhledu `docs/previews/settings-tabs.html` a předávce; testy a překlad obou edic; chyby opravit a zapsat. Pak dávku uzavřít podle skillu `ukoly-a-fronty`.

## Co do fronty nepatří

- Opravy z vyzkoušení beta.1 (A2 až A9 v `UKOLY.md`): přibudou jako balíky, až uživatel napíše, co nesedí.
- Přestavba celého okna (návrhy W1 až W3) a styl MUTED: odložené alternativy.

## Předávky

**O9** (22. 11., sub-Opus pod výrobním vláknem, kontrola Fable)
- Stav: hotovo. Nové `installer/AFTERLIFE.iss`; krok „Installer“ v `.github/workflows/build.yml`; `README.md`, `docs/USER-GUIDE.md`, `docs/BUILDING.md`, `CHANGELOG.md`.
- Instalátor: jen pro aktuálního uživatele, bez práv správce, do `%LOCALAPPDATA%\Programs\AFTERLIFE`, zástupce v nabídce Start (na plochu na vyžádání), odinstalace. Nemá žádné mazání navíc: knihovny ani nastavení se nedotkne.
- Workflow ho sestaví ze stejné složky jako zip, přidá SHA-256 a přiloží k vydání. Verzi bere z `Directory.Build.props`.
- Kontrola (Fable) našla a opravila dvě věci: 1) aplikace po instalaci navrhovala knihovnu modů ve složce, kterou vlastní instalátor; teď instalovaná kopie (pozná se podle odinstalátoru vedle exe) navrhne výchozí místo `%LOCALAPPDATA%\AFTERLIFE\Library` (`AppSettings.SuggestLibrary`, test). 2) krok s instalátorem při běžném sestavení nezastaví build, když nástroj na stroji chybí; u tagu (vydání) selhat musí.
- Ověřeno: 322 testů, překlad, platný YAML. Neověřeno: skript nikdy neprošel překladačem Inno Setup (na Linuxu neběží) a krok workflow neběžel na GitHubu; instalace, aktualizace přes starší verzi a odinstalace ve Windows (A10).
- Rozhodnutí agenta, ponechána: pevné AppId (nesmí se nikdy změnit), název aplikace „AFTERLIFE“ bez „Offline“, instalátor se sestavuje i při běžném pushi, po instalaci nabídne spuštění.
- Věta do popisu na Nexusu: „Prefer a classic installer? The same release is also on GitHub as AFTERLIFE-Offline-<version>-win-x64-Setup.exe (per-user, no admin rights, with an uninstaller) at https://github.com/silentbobcz/afterlife/releases.“ Před zveřejněním ověřit pravidla Nexusu.

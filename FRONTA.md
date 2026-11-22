# AFTERLIFE: fronta pro modely (2. dávka)

Sestaveno 22. 11. 2026 po vydání 1.0.0-beta.1. **Nejnovější stav: větev `claude/kind-hopper-az2rax`** (`main` + O8, bez instalátoru). Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). První dávka je v `archiv/FRONTA-1-davka.md` (jen archiv, stav z ní nečti).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev claude/kind-hopper-az2rax) a udělej další balík.“

## Pravidla dávky

- Pracuj ve své pracovní větvi založené z `main` a odesílej do ní. Tag a vydání dělá uživatel.
- Sestavení pro uživatele jen na pokyn. Testy a kontrolní překlad si balík pouští sám.
- Co nešlo vidět běžet: v předávce „Neověřeno“ a uživateli položka A v `UKOLY.md`.
- Každý zápis do logu jde přes `ActivityLog` nebo chybový log okna, tedy přes `LogPrivacy.Scrub`; nic nepiš do souboru přímo.
- Předávka pod balíkem, nejvýš deset řádků.

## Stav balíků

| Balík | Model | Co | Čeká na | Stav |
|---|---|---|---|---|
| O8 | Opus | Přestavba nastavení v kódu podle schváleného náhledu | nic | hotovo, zkontrolováno |
| F-kontrola | Fable | Kontrola O8 | nic | hotovo, bez nálezu; dávka čeká na vyzkoušení (A10) |

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

## F-kontrola

Fable. Projít commity O8 proti náhledu `docs/previews/settings-tabs.html` a předávce; testy a překlad obou edic; chyby opravit a zapsat. Pak dávku uzavřít podle skillu `ukoly-a-fronty`.

## Co do fronty nepatří

- Opravy z vyzkoušení beta.1 (A2 až A9 v `UKOLY.md`): přibudou jako balíky, až uživatel napíše, co nesedí.
- Přestavba celého okna (návrhy W1 až W3) a styl MUTED: odložené alternativy.
- Samostatný instalátor: zrušeno 22. 11. Zůstává jen zip se složkou; balík O9 se nedělá a jeho zkušební výroba byla vrácena.

## Předávky

**O8** (22. 11., Opus)
- Stav: hotovo. Nové `Views/SettingsView.xaml` (šablona se záložkami, sloučená v `MainWindow.xaml`; stará šablona z `Panels.xaml` pryč) a `ViewModels/Overlays/SettingsVm.Tabs.cs` (výběr záložky, údaje na záložkách, složky uložených souborů). `LibraryPaths.Removed` místo vlastní cesty v `Quarantine`.
- Logika voleb beze změny, jen přesun. Nové: odkazy Show u uložených souborů (dočasné soubory otevřou `staging`; chybějící složka otevře knihovnu), rámeček o FIXERu, součet uložených souborů, zelená tečka u funkčního klíče, verze dole, v Offline záložka Updates se zámkem a zašedlými volbami.
- Rozhodl jsem sám: záložka se pamatuje jen do zavření aplikace (ne do databáze); Link mods jen s uloženým klíčem jako dřív; v CHANGELOGu nový nadpis „Unreleased“ nad beta.1 (číslo doplní vydání).
- Ověřeno: 322 testů, překlad plné i Offline edice.
- Neověřeno: nic z okna jsem neviděl běžet (A10); hlavně barva vybrané záložky, rolování delších textů ve Windows písmech, zámek u Updates.
- Pro uživatele: A10 v UKOLY.md.


**F-kontrola O8** (22. 11., Fable): prošel jsem commit `90f6cc1` proti náhledu a předávce. Chybu jsem nenašel, nic jsem neopravoval. Ověřil jsem, že každá vazba v `Views/SettingsView.xaml` má protějšek v `SettingsVm`, že záložky předávají hodnotu výčtu (ne text) a že v nastavení zůstaly všechny volby včetně dvou přepínačů kontroly aktualizací a varování o dotazech. Sloučeno s větví bez instalátoru; testy a překlad obou edic viz stav níže. Neověřeno zůstává vše, co je vidět jen v okně (A10).


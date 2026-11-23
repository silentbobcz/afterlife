# AFTERLIFE: fronta pro modely (5. dávka)

Sestaveno 23. 11. 2026 z hlášení uživatele na Nexus Mods (každé stažení přes Mod Manager Download otevřelo nové okno, „30 oken“) a z položky D5 v [UKOLY.md](UKOLY.md).
Další volná čísla: počítadlo je v hlavičce `UKOLY.md` (platí to v `main`).

Nejnovější kód: větev `main`. Každý balík má vlastní větev a pracovní složku (`git worktree add <složka> -b <větev> origin/main`); hlavní klon zůstává na `main`. Do `main` se slučuje jen na výslovný pokyn uživatele.
Kontroly: `dotnet test tests/Afterlife.Tests` a `dotnet build src/Afterlife.App -p:EnableWindowsTargeting=true`; na počítači uživatele není .NET 10 SDK, tam `git push` větve a `gh workflow run build.yml --ref <větev>`.
Skilly: alt, ukoly-a-fronty, kohn, afterlife (zdroje v `D:/_AI/_skills/<skill>/SKILL.md`, když nejsou nahrané).

## Pravidla pro všechny balíky

- Vše v repozitáři anglicky (kód, komentáře, commity, texty v okně); bez jmen modelů a čísel balíků v kódu.
- Co volá Nexus Mods, začíná `LockedOffline(...)`; edice Offline se nesmí nikam připojit.
- Na žurnálované nasazení (`Deployment.cs`, `Deploy.cs`) a obnovu po pádu se nesahá.
- Soubory nad 600 řádků (C#) nebo 800 (XAML) nedostanou nový kód: založ soubor vedle.
- Každý balík s kódem: jeden řádek do `CHANGELOG.md` pod `## Unreleased`, 1–3 řádky do `docs/HOW-IT-WORKS.md`, když se mění stavba.
- Zápis do logu jen přes chybový log v `App.xaml.cs` nebo log aktivity (oba filtrují `LogPrivacy.Scrub`).
- Okno WPF mimo Windows nespustíš: co jsi neviděl běžet, napiš do předávky jako „Neověřeno“.

## Stav balíků

| Balík | Kde běží | Co | Stav |
|---|---|---|---|
| O28 | Opus | Odkaz nxm nesmí otevřít druhé okno | ☐ čeká |

## O28. Odkaz nxm nesmí otevřít druhé okno (`App.xaml.cs`, `Services/`)

Může začít hned. Jen plná edice (Offline odkazy nxm nepřebírá).

Příčina je v kódu známá: druhé spuštění (z odkazu `nxm://`) zjistí přes mutex, že AFTERLIFE běží, a pošle odkaz přes pojmenovanou rouru (`SingleInstance.TrySend`, čeká 1,5 s). Když předání selže, `App.OnStartup` se neukončí a otevře **vlastní okno**. Selhávat může z těchto důvodů (který z nich nastal u pisatele, zatím nevíme, viz D5):
- běžící AFTERLIFE je spuštěné jako správce a prohlížeč spouští odkaz bez práv správce, takže roura odmítne zápis (`UnauthorizedAccessException`);
- smyčka `SingleInstance.Listen` chytá jen `OperationCanceledException` a `IOException`; jiná výjimka ji tiše ukončí a od té chvíle selže každé předání;
- roura má jedinou instanci a `ReadLineAsync` nemá časový limit, takže zaseknutý klient zablokuje všechny další.

Body:
1. Přesuň `SingleInstance` z `Services/NexusServices.cs` do vlastního `Services/SingleInstance.cs` (beze změny chování, samostatný commit).
2. `App.OnStartup`: když mutex patří jinému procesu, **nikdy neotevírej okno**. Předání zkoušej opakovaně asi 10 s. Když se nepodaří, použij záložní cestu z bodu 3. Teprve když selže i ta, ukaž krátké hlášení (třeba „AFTERLIFE is already running, possibly as administrator. Close it or start it without administrator rights.“) a skonči.
3. Záložní cesta, která funguje i proti AFTERLIFE spuštěnému jako správce: odkaz se zapíše jako soubor do složky `inbox` vedle nastavení (`AppSettings.SettingsDirectory`), atomicky (`.tmp` a přejmenování). Běžící okno složku sleduje (`FileSystemWatcher` a navíc projde složku při startu, protože hodinky mohou událost ztratit), každý soubor přečte, smaže a obslouží. Přijímej jen text, který projde `NxmLink.TryParse`, nebo „activate“.
4. `Listen`: chyť každou výjimku, zapiš ji do chybového logu a pokračuj. Povol víc instancí roury (`NamedPipeServerStream.MaxAllowedServerInstances`, nová instance hned po připojení klienta) a čtení dej časový limit (asi 5 s).
5. Víc odkazů rychle za sebou: ověř, že se každý zařadí jako vlastní stažení. Pozor na `MainViewModel.HandleExternalAsync`: dokud není knihovna otevřená, drží jen jeden `_pendingLink` a další ho přepíše; udělej z něj frontu.
6. Testy na čistou logiku (zápis a čtení souboru v inboxu, odmítnutí jiného obsahu, pořadí více odkazů). Řádek do `CHANGELOG.md`, 1–2 řádky do `docs/HOW-IT-WORKS.md`.

Hotovo, když: kontroly projdou a v předávce je položka A pro uživatele: AFTERLIFE spuštěné normálně i jako správce, v prohlížeči třikrát rychle za sebou Mod Manager Download, výsledek je jedno okno a tři stažení. Neověřitelné mimo Windows: vše v okně, roura a správce.

## Co do fronty nepatří

- Odpověď pisateli na Nexusu: uživatel ji vkládá sám (návrh dostal ve vlákně), jeho odpověď je D5.

## Jak pustit

Do nového vlákna s modelem Opus: „Přečti `D:/_AI/afterlife/repo/FRONTA.md` a udělej balík O28.“

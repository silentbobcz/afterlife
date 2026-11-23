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
| O28 | Opus | Odkaz nxm nesmí otevřít druhé okno | ☑ hotovo, sloučeno do `main` |

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

### Předávka O28 (23. 11. 2026)

**Stav:** hotovo, sloučeno do `main` 23. 11. (2 commity: přesun `SingleInstance` beze změny, pak oprava). Kontroly na GitHubu prošly: <https://github.com/silentbobcz/afterlife/actions/runs/45282336687> (464 testů, build plné i Offline edice, zkušební zip `AFTERLIFE-full-test`).

Co se změnilo:
- `Services/SingleInstance.cs` (nový soubor): druhé spuštění **nikdy neotevře okno**. `Deliver` zkouší rouru asi 4 s (při odmítnutí, tj. AFTERLIFE jako správce, hned dál), pak zapíše odkaz do `inbox` a do 10 s celkem čeká, až ho běžící okno převezme. Když ne, soubor smaže, ukáže krátké hlášení a skončí. Mutex vlastněný procesem správce už nevyhodí výjimku (bere se jako „běží jiný“).
- `Listen`: víc instancí roury (nová hned po připojení klienta), čtení 5 s, každá výjimka jde do chybového logu a smyčka pokračuje. Poslouchá se **před** stavbou okna (dřív až po něm, takže rychlé druhé kliknutí při startu AFTERLIFE našlo prázdno a otevřelo okno; i to mohla být příčina „30 oken“).
- `Afterlife.Infrastructure/LinkInbox.cs` (nový): `inbox` vedle `settings.json`, zápis `.tmp` a přejmenování na `.link`, čtení nejstarší první, soubor se po přečtení smaže. Přijímá jen `activate`, odkaz, který projde `NxmLink.TryParse`, **a navíc** odkaz na kolekci (`CollectionLink.TryParse`, schéma nxm): Mod Manager Download u kolekce jde také přes nxm a bez toho by se přes inbox ztratil. Odkaz starší než 10 min se zahodí (nestáhne se později bez vědomí uživatele). Běžící okno složku hlídá `FileSystemWatcher` a navíc ji projde při startu a každé 3 s.
- `MainViewModel.Import.cs`: místo jednoho `_pendingLink` fronta; po otevření knihovny se spustí všechny čekající odkazy stejně jako odkazy, které přijdou za běhu (souběžně, ne po jednom: kolekce čeká na další odkazy, postupné zpracování by ji zablokovalo). `HandleExternalAsync` přesunuto z `MainViewModel.cs` (nad 600 řádků) sem.
- Testy `LinkInboxTests` (filtr obsahu, zápis a čtení, cizí a starý soubor, pořadí pěti odkazů). Řádek v `CHANGELOG.md`, řádek v `docs/HOW-IT-WORKS.md`.

**Neověřeno** (okno, roura a správce mimo Windows nejdou vidět běžet): celé předání mezi dvěma spuštěními, roura proti AFTERLIFE spuštěnému jako správce, sledování složky `inbox`, hlášení při selhání, fronta odkazů před otevřením knihovny.

Mimo balík (nesahal jsem): když AFTERLIFE poprvé ukazuje úvodní nastavení knihovny, čekající odkazy se po jeho dokončení nespustí (tak to bylo i dřív s jedním odkazem; patří to do `InitializeAsync` v `MainViewModel.cs`, který je nad 600 řádků).

Pro uživatele (A28 v `UKOLY.md`): **Vyzkoušet: jedno okno při Mod Manager Download.** Ze zkušebního zipu `AFTERLIFE-full-test` (běh výše) spusť AFTERLIFE normálně, na Nexusu u tří různých modů rychle za sebou klikni na Mod Manager Download: má zůstat jedno okno a přijít tři stažení. Pak AFTERLIFE zavři, spusť ho pravým tlačítkem „Spustit jako správce“ a zopakuj totéž: zase jedno okno a tři stažení. Napiš, jestli se někde otevřelo druhé okno nebo se objevilo hlášení „AFTERLIFE is already running…“.

## Co do fronty nepatří

- Odpověď pisateli na Nexusu: uživatel ji vkládá sám (návrh dostal ve vlákně), jeho odpověď je D5.

## Jak pustit

Do nového vlákna s modelem Opus: „Přečti `D:/_AI/afterlife/repo/FRONTA.md` a udělej balík O28.“

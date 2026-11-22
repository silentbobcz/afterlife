# AFTERLIFE: fronta pro modely (2. dávka)

Sestaveno 22. 11. 2026 po vydání 1.0.0-beta.1. **Nejnovější stav: větev `main`** (sloučeno 22. 11. včetně nastavení v záložkách, bez instalátoru). Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). První dávka je v `archiv/FRONTA-1-davka.md` (jen archiv, stav z ní nečti).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej balík O10.“

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
| O10 | Sonnet | Náhled volného pořadí v MODS a kategoriích | nic | čeká |
| O11 | Opus | Volné pořadí v kódu: tažení uvnitř MODS a kategorií, čísla pořadí, A→Z | B4 | čeká |
| O12 | Sonnet | Posun z klávesnice, „Load before…“ v Konfliktech, ztlumení modů bez archivů | O11 | čeká |
| F-pořadí | Fable | Kontrola O11 a O12 | O12 | čeká |
| O13 | Sonnet | Srozumitelnější texty dialogu při vypnutí modu | nic | čeká |

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

## Volné pořadí načítání (O10 až O12)

Proč: uživatel z Nexusu se vrátil k jinému správci, protože v AFTERLIFE nejde mod jen přetáhnout nahoru nebo dolů. Uvnitř MODS a každé kategorie se dnes řadí natvrdo podle názvu (`MainViewModel.cs`, řazení v `Apply` s komentářem „MODS and every category alphabetically“) a tažení řádku v rámci téže sekce končí hláškou (`MainViewModel.Blocks.cs`, `MoveModsAsync`). Jediná cesta je kategorie pro jeden mod. Cíl: řádek jde přetáhnout kamkoli uvnitř MODS a kategorií jako dnes v odemčené kolekci; bloky a kategorie fungují dál.

### O10. Náhled

Sonnet. Statický náhled `docs/previews/free-load-order.html` (jeden soubor, bez kódu aplikace, tokeny barev a písma jako v `App.xaml`), dnešní vzhled seznamu. Ukázat: (1) číslo pořadí na řádku (#1, #2 …) jen v pohledu LOAD ORDER, při řazení pro zobrazení (⇅) schované; (2) čáru, kam mod při tažení dopadne; (3) tlačítko „A→Z“ v hlavičce MODS a každé kategorie, které jednou seřadí podle názvu, s potvrzením; (4) jak vypadá mod s místními úpravami („└ …“), který se táhne s rodičem. Rozsah MODS asi 40 řádků se dvěma kategoriemi, ať je vidět čísla přes hranici kategorie. Pod náhledem krátce: co se změní oproti dnešku a texty hlášek. Pro uživatele položka B4 v `UKOLY.md`.

### O11. Volné pořadí v kódu

Opus. Čeká na B4 (schválený náhled). Rozhodnuto 22. 11. (C9): nový mod padá na konec MODS, bez kategorie.
- Zobrazení: MODS a kategorie v pořadí profilu, ne podle názvu. Řazení v `Apply` nahradit pořadím z profilu; „└“ vrstvy dál hned pod rodičem (táhnou se s ním, samy se netáhnou mimo něj). Kolekce a CORE MODS beze změny.
- `MoveModsAsync`: zrušit větev s hláškou, drop uvnitř téže sekce mění pořadí přes stávající `ApplyLayoutAsync` (`BlockLayout.Compose` pořadí uvnitř sekce už zachovává). Víc vybraných modů se přesune jako skupina v dnešním pořadí.
- Číslo pořadí na řádku podle náhledu; počítá se z `CurrentOrder()` po CORE MODS, ať odpovídá `modlist.txt`.
- „A→Z“ v hlavičce MODS a kategorie: jednorázově seřadí jen tu sekci, zapíše do profilu, nic jiného.
- Nové mody (import, stažení) na konec MODS bez kategorie; aktualizace drží místo staré verze. Stávající knihovny: dnešní (abecední) pořadí se převezme beze změny, nikomu se po aktualizaci nic nepřehází. Ověř zvlášť profil s `OrderIsCustom = false` (výchozí pořadí hry je podle názvu archivu, ne modu).
- Pořadí patří profilu, kategorie jsou společné: přepnutí profilu ukáže jeho pořadí.
- Testy: přesun uvnitř sekce, přes hranici kategorie, skupina, vrstva s rodičem, A→Z, nový mod. Opravit texty v `docs/USER-GUIDE.md` (oddíly 3, 10 a tabulka v 22: „sorted by name“ pryč, postup s kategorií pro jeden mod nahradit tažením), `README.md` (řádek Conflicts and load order), jeden řádek v `CHANGELOG.md`, 1–3 řádky v `docs/HOW-IT-WORKS.md`.
- Pro uživatele položka A v `UKOLY.md` (tažení, čísla, A→Z, APPLY ukáže změnu pořadí, přepnutí profilu).

### O12. Pohodlnější řazení

Sonnet. Čeká na O11. Ctrl+↑/↓ posune vybrané mody o řádek, Ctrl+Home/End na začátek a konec sekce (tabulka klávesnice v návodu). V záložce Conflicts u souboru s jiným vítězem tlačítko „LOAD BEFORE ‹mod›“, které mod přesune těsně před soupeře, pokud jsou oba v MODS nebo kategoriích (jinak tlačítko chybí). Mody bez souborů `.archive` mají číslo pořadí šedé a v tooltipu „Load order does not affect this mod“. Návod, CHANGELOG, položka A v `UKOLY.md`.

### F-pořadí

Fable. Projít O11 a O12 proti náhledu a předávkám, hlavně: pořadí v `modlist.txt` po APPLY odpovídá číslům v seznamu; řazení ⇅ ani filtr pořadí nemění; nic se nepřehází při startu se starou knihovnou. Testy, překlad obou edic, opravy zapsat. Pak dávku uzavřít podle skillu `ukoly-a-fronty`.

## O13. Srozumitelnější dialog při vypnutí modu

Sonnet. Uživatel 22. 11. dialog vyzkoušel: funguje, ale logice nerozumí. Soubor `ViewModels/MainViewModel.Library.cs` (nadpisy „ALSO SWITCH OFF WHAT NEEDS IT?“, „ONLY IT NEEDED, NOW ON“, „IT NEEDS, STAYS ON“ a věty kolem), šablona v `Views/Dialogs.xaml`. Chování se nemění, jen texty a případně pořadí tří částí.
1. Napiš návrh nových textů jako tabulku „dnes → nově“ do předávky: každá část jednou celou větou, která říká, co se stane s kterými mody (například „These mods need it and will stop working: switch them off too?“). Žádné zkratky typu „IT NEEDS“.
2. Pro uživatele položka B v `UKOLY.md` s tou tabulkou; do kódu až po schválení.

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


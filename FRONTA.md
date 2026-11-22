# AFTERLIFE: fronta pro modely (1. dávka)

Živá verze fronty. **Nejnovější stav dávky: větev `claude/modest-edison-qznx06`** (O1–O7, F1–F3). Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md).
Úkoly pro uživatele: [`UKOLY.md`](UKOLY.md) v této větvi.

Spouštěcí věta (až bude B3 schválené): „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev claude/modest-edison-qznx06) a udělej balík O8.“
Doma na počítači: `git fetch origin claude/modest-edison-qznx06` a `git checkout claude/modest-edison-qznx06`, pak stejná věta.

## Pravidla dávky

- Sestavení pro uživatele se nedělá, dokud nejsou hotové všechny úpravy a uživatel neřekne. Testy a kontrolní překlad si balík pouští sám.
- Na GitHub jen do pracovní větve; `main`, tagy a vydání jsou rozhodnutí uživatele (C3).
- Co nešlo vidět běžet: v předávce „Neověřeno“ a uživateli položka A v dokumentu úkolů.
- Předávka pod balíkem, nejvýš deset řádků.

## Stav balíků

| Balík | Model | Co | Čeká na | Stav |
|---|---|---|---|---|
| O1 | Opus | Překryvná okna rostou s oknem, okno začíná vystředěné | nic | hotovo `b1aa34c`, zkontrolováno |
| O2 | Opus | Dialog při vypnutí modu ukáže i to, co mod potřebuje | nic | hotovo `9400745`, zkontrolováno |
| O3 | Opus | Rešerše a návrhy seznamu modů (bez kódu) | nic | hotovo |
| O4 | Opus | Panel filtrů ve třech stavech | nic | hotovo `c75c70f`, zkontrolováno |
| O5 | Opus | Návrhy celého okna W1/W2/W3 (bez kódu) | nic | hotovo, odloženo jako alternativa |
| F1 | Fable | Hromadné propojení ručně přidaných ZIPů s Nexusem | nic | hotovo `e1b10cd`, sloučeno do větve `23df26c` |
| O6 | Opus | Tlačítko hromadného propojení i v nastavení Nexusu | nic | hotovo `a75ded6`, zkontrolováno |
| F2 | Fable | Řazení seznamu a virtualizace při 300+ modech | nic | hotovo (bez kontroly, psal Fable) |
| F3 | Fable | Kontrola aktualizací při 600+ modech | nic | hotovo (bez kontroly, psal Fable) |
| O7 | Opus | Nové nastavení se záložkami vlevo (návrh, bez kódu) | nic | hotovo, náhled čeká na B3 |
| O8 | Opus | Přestavba nastavení v kódu podle schváleného náhledu | B3 | čeká na B3 |
| F-kontrola | Fable | Kontrola O1, O2, O4, O6 | nic | hotovo, bez nálezu; uzavření dávky čeká na C3 a C4 |

## O6. Hromadné propojení i v nastavení Nexusu

Opus, malý. Hotovo. F1 dal příkaz „Link all unlinked mods…“ jen do detailu modu, protože `Views/Panels.xaml` tehdy měnil O1.
1. V `Views/Panels.xaml`, oddíl NEXUS v nastavení, přidej tlačítko se stejným příkazem jako v detailu modu (najdi ho podle `MainViewModel.Linking.cs`).
2. V edici Offline schované nebo zamčené jako ostatní věci Nexusu (`LockedOffline`).
3. Řádek do `CHANGELOG.md`, sestavení a testy.

## F2. Řazení seznamu a virtualizace při 300+ modech

Fable. Proč Fable: pořadí v seznamu je při vlastním pořadí zároveň pořadí načítání (`SyncSortedOrderAsync` ho zapisuje do profilu), řazení pro oko nesmí změnit hru; virtualizace se potkává s přetahováním a sbalováním bloků.
Soubory: `MainViewModel.cs`, `MainViewModel.Blocks.cs`, `MainWindow.xaml` (`ModsList`), `App.xaml` (`PlainList`).
1. Řazení jen pro zobrazení (název, datum přidání, stav, velikost), oddělené od pořadí načítání. Při jiném řazení vypnout přetahování a říct to nad seznamem. `SyncSortedOrderAsync` se kvůli řazení nesmí spustit.
2. Test: přepnutí řazení nezmění `Entries` profilu ani `modlist.txt` v plánu.
3. Virtualizace: zapnout `VirtualizingPanel.IsVirtualizingWhenGrouping` a projít místa, která počítají s vykresleným řádkem (přetahování, značky vložení, posun na vybraný mod, sbalení bloku).
4. Kde si bez spuštění nejsi jistý, dát virtualizaci za přepínač v nastavení (výchozí vypnuto) a zapsat položku A.
5. Do předávky: co má uživatel změřit (otevření okna, plynulost rolování s 300 mody, s přepínačem a bez).

## F3. Kontrola aktualizací při 600+ modech

Fable. Hotovo. Zadání potvrzeno uživatelem 21. 11. (C4); volby jsou zatím v prostém oddílu UPDATES, na své místo je přesune O8. Soubory: `MainViewModel.Updates.cs`, `Nexus.cs` (`NexusUpdates`), nastavení, `NexusTests.cs`.
1. Zrušit strop 60 modů na jednu kontrolu: přečte se vše, co je na řadě, a kontrola se zastaví jen o limit Nexusu (`RateLimited`), zbytek příště. Před tím ověřit v dokumentaci Nexusu skutečné limity dotazů.
2. Pořadí: core mody a mody zapnuté ve vybraném profilu první, pak ostatní.
3. Tlačítko CHECK UPDATES použije seznam změněných modů (dnes ho obchází a čte všech 600+). Plná kontrola všech jako zvláštní volba.
4. Nastavení: kontrola při startu a každou hodinu ano/ne (core mody vždy); rozsah při startu (jen zapnuté mody, nebo všechny).
5. Pojistka: ke každému modu zvlášť datum posledního plného přečtení. Mezi tím jen rozdíly ze seznamu změn; jednou za N dní se mod přečte celý. N je v nastavení, výchozí 7. Mody rozložit rovnoměrně, aby nepřišly na řadu všechny ve stejný den. Dnes se „poslední kontrola“ přepisuje i při odpovědi „beze změny“, takže plné přečtení při pravidelném používání neproběhne nikdy.
6. Testy: výběr modů ke kontrole (rozdíly, pojistka po N dnech, rozložení, pořadí, zastavení o limit).

## O7. Nové nastavení se záložkami vlevo (návrh, bez kódu)

Opus. Může začít hned. Uživatel 21. 11. rozhodl nastavení přestavět: vzniklo pro pár voleb a dnes jich je kolem dvaceti v jednom rolovacím sloupci šířky 680 (`Views/Panels.xaml`, šablona `SettingsVm`).
1. Statický náhled nastavení se záložkami vlevo: Game a Library · Nexus Mods (účet, propojení modů, nxm://) · Updates (volby z F3; kontrola při startu a kontrola každou hodinu jsou od 21. 11. dva samostatné přepínače) · Display (animace, vykreslování řádků) · Storage (uložené soubory) · Diagnostics (log, diagnostika) · FIXER. Odinstalace samostatně až dole, mimo běžné volby.
2. V paletě a písmech ze skillu grafika-cyberpunk, v rozměru okna 1612 × 910 i 2560 × 1440. Všechny dnešní volby musí mít své místo; vypiš, kam se která přesunula.
3. Odkaz na náhled zapsat k B3 v `UKOLY.md`.

## O8. Přestavba nastavení v kódu

Opus. Čeká na B3 (schválený náhled z O7). Soubory: `Views/Panels.xaml` (šablona nastavení do vlastního souboru ve `Views/`), `ViewModels/Overlays/SettingsVm.cs`. Logika voleb se nemění, jen se přesunou. Dvě nové drobnosti z náhledu: u každého druhu uložených souborů odkaz „Show“, který otevře jeho složku v Průzkumníku; u FIXERu rámeček „In development · not released yet“ s popisem, co FIXER je. Do alpha.11, pokud uživatel neřekne jinak.

## F-kontrola

Fable. Projít commity O1, O2, O4, O6 proti zadání a předávkám, nejvíc „Neověřeno“ a „Rozhodl jsem sám“; testy a sestavení; chyby opravit a zapsat. F1 a F2 nekontroluj (psal je Fable). Pak dávku uzavřít: trvalé věci do `docs/HOW-IT-WORKS.md` a `CHANGELOG.md`, tento soubor do `archiv/FRONTA-1-davka.md`.

## Předávky

**O1** (21. 11., Opus): seznamy v 15 překryvných oknech rostou o to, o co je okno vyšší než 910 px (`GrowWithWindow`); okno se zmenší na pracovní plochu už v konstruktoru, takže ho CenterScreen vystředí ve skutečné velikosti. Neověřeno ve Windows (A2).

**O2** (21. 11., Opus): dialog při vypnutí: závislé mody, „ONLY IT NEEDED, NOW ON“ (vlastní zaškrtávátko, výchozí ne), „IT NEEDS, STAYS ON“ ke čtení (`KeptRequirements`). Rozhodl jsem sám: core mody ani v informačním seznamu; kolekce se ptají dál vlastním dotazem. Neověřeno ve Windows (A3).

**O4** (21. 11., Opus): `ModFilter` (Infrastructure, čistá logika, 6 testů), `FilterPanelVm`, `Views/FilterPanel.xaml`, sloupec mezi PROFILES a MOD LIBRARY, tlačítko „⧩ FILTERS“ u hledání (cyklus otevřený → pruh → skrytý), stav v meta `ui.filterpanel`. Rozhodl jsem sám: automatické sbalení do pruhu pod šířkou okna 1500 px (ne podle šířky seznamu 760 px, to by sbalovalo už při výchozí velikosti); bez filtru „FIXER quests“ (balíček FIXERu se v datech nepozná); štítky kolekcí a OPTIONAL/REQUIRED nejsou mezi štítky. Ověřeno: 287 testů, sestavení aplikace. Neověřeno: vzhled a klikání ve Windows (A5).

**Sloučení F1** (21. 11., Opus): větev `claude/kind-hopper-az2rax` (F1 + O1 + O2) sloučena do pracovní větve, jediný konflikt v `CHANGELOG.md` (obě položky ponechány). Pozor: nadpis v CHANGELOGu je teď „1.0.0-alpha.10“ (přišlo z F1), verze v `Directory.Build.props` se neměnila. Ověřeno: 296 testů, sestavení plné i Offline edice.

**O6** (21. 11., Opus): `SettingsActions.LinkMods` a `SettingsVm.LinkModsCommand`; odkaz „Link all unlinked mods…“ v nastavení, oddíl 03 · NEXUS MODS, viditelný s uloženým klíčem. Zavře nastavení a spustí stejný příkaz jako detail (`LinkGuessedModsCommand`). V Offline edici je celý oddíl Nexusu skrytý a příkaz zamčený. Neověřeno: klikání ve Windows (A6).

**F-kontrola** (21. 11., Fable): prošel jsem commity O1, O2, O4 a O6 proti zadání a předávkám. Chybu jsem nenašel, nic jsem neopravoval. Poznámky: překryvná okna rostou až od výšky okna 910 (na menší obrazovce jako dřív); panel filtrů se sbaluje podle šířky okna 1500, ne podle šířky seznamu (rozhodnutí O4, ponecháno); `CLAUDE.md` říkal „Unreleased“, nadpis je „1.0.0-alpha.10“ (opraveno v `CLAUDE.md`). Dávka zatím není uzavřená: čeká F3 (C4) a odeslání do `main` (C3), proto tento soubor zůstává živý a nejde do archivu.

**F2** (21. 11., Fable)
- Stav: hotovo. Nové `ModSort.cs` (Infrastructure, čistá logika, 7 testů) a `MainViewModel.Sorting.cs`; změny v `MainViewModel.cs`, `MainViewModel.Blocks.cs`, `Rows.cs`, `MainWindow.xaml`, `Views/Panels.xaml`, `SettingsVm.cs`, `MainViewModel.Settings.cs`, `AppSettings.cs`.
- Řazení: tlačítko „⇅“ vedle FILTERS prochází LOAD ORDER → NAME → NEWEST → STATE → SIZE. Řadí se uvnitř bloků, core mody a pořadí bloků zůstávají, vrstva „my edits“ zůstává pod svým modem. Volba se pamatuje (`ui.sort`).
- Bezpečnost pořadí: okno drží řádky dvakrát. `_loadOrdered` je pořadí načítání a čte ho `CurrentOrder()`, tedy i `SyncSortedOrderAsync`, přesuny bloků a kategorie; `Mods` je to, co je vidět. Dřív `CurrentOrder()` četl zobrazený seznam, takže by seřazený pohled přepsal pořadí ve hře. Přepnutí řazení nic nečte ani nezapisuje kromě zapamatované volby.
- Při řazení je přetahování modů vypnuté (`ModRowVm.SortLocked`) a nad seznamem je řádek s vysvětlením a „Back to load order“. Šipky u bloků fungují dál.
- Virtualizace: přepínač v Settings → 05 · DISPLAY, výchozí vypnuto (`AppSettings.VirtualizeList`), platí hned. Kód přetahování, značek vložení a posunu na vybraný mod jsem prošel: pracuje s řádkem pod myší a s `ScrollIntoView`, na vykreslení všech řádků nespoléhá.
- Ověřeno: 303 testů, překlad plné i Offline edice.
- Neověřeno: vzhled tlačítka a řádku, chování virtualizace při rolování, přetahování a sbalování bloků (A7, A8).
- Rozhodl jsem sám: řazení platí i uvnitř kolekcí (jen pro oko); při řazení nejde mod přetáhnout ani do jiné kategorie; „NEWEST“ je podle data importu do knihovny; „STATE“ řadí problémy první, pak neaplikované, zapnuté, vypnuté.
- Co změřit (A8): čas od spuštění po zobrazení seznamu a plynulost rolování s 300+ mody, jednou s přepínačem a jednou bez.

**F3** (21. 11., Fable)
- Stav: hotovo. Nové `UpdatePlan.cs` (Infrastructure, čistá logika, 6 testů); přepsaná kontrola v `MainViewModel.Updates.cs`; volby v `AppSettings.cs`, `SettingsVm.cs`, `MainViewModel.Settings.cs`, dočasný oddíl UPDATES ve `Views/Panels.xaml`.
- Strop 60 je pryč: přečte se vše, co je na řadě, v pořadí core mody, mody v profilu, ostatní; zastaví se jen o limit Nexusu a zbytek zůstane na řadě.
- CHECK UPDATES jde přes seznam změněných modů a čte jen ty. Plná kontrola všech je odkaz „Check every mod now…“ v nastavení.
- Pojistka: každý balíček má vlastní datum posledního plného přečtení (`nexus:fullread:<balíček>`) a vlastní bod v periodě odvozený z id, takže se každý den čte zhruba stejný díl. Knihovna zkontrolovaná před touto změnou se rozloží přes jednu periodu, nepřečte se naráz.
- Nastavení: automatická kontrola ano/ne (core mody vždy), jen mody zapnuté v profilu, počet dní mezi plnými čteními (výchozí 7, 0 = nikdy).
- Ověřeno: 309 testů (mimo jiné 700 modů přes 7 dní: každý přečten právě jednou, 60 až 140 denně), překlad plné i Offline edice.
- Neověřeno: skutečné limity dotazů Nexusu jsem v dokumentaci neověřil, kód se řídí jen odpovědí 429; chování v okně (A9).
- Rozhodl jsem sám: při vypnuté automatické kontrole se dál kontrolují jen core mody; plná kontrola je v nastavení, ne v hlavním okně; průběh v dolní liště se ukazuje jen u ruční kontroly s víc než 20 mody.

**F3, doplněk: pojistka limitu Nexusu** (21. 11., Fable)
- Nové `NexusLimits.cs` (Infrastructure, 4 testy). Limity podle nápovědy Nexusu: 20 000 dotazů denně a 500 za hodinu na klíč, pro všechny programy dohromady (ne 2 500 a 100, jak jsem dřív uváděl z paměti).
- Aplikace čte zbývající dotazy z hlaviček odpovědí, kontrolu zastaví 50 dotazů před limitem (aby šlo dál stahovat), uloží pauzu do celé hodiny (nebo do půlnoci GMT u denního limitu) a do té doby se na nic neptá. Pak pokračuje sama, i s nedokončenou plnou kontrolou všech modů.
- Hláška při dosažení limitu říká, kolik modů čeká a do kdy; když je počet dní mezi plnými čteními na knihovnu moc nízký, pošle uživatele do Settings → UPDATES.
- V nastavení je řádek „kolik dotazů denně to stojí pro tvých N modů“ a oranžové varování, když plná čtení berou víc než desetinu denního limitu (s doporučeným počtem dní). U „Check every mod now…“ je upozornění, pokud je modů víc, než se vejde do hodiny.
- Ověřeno: 313 testů, překlad obou edic. Neověřeno: názvy hlaviček `x-rl-hourly-remaining` a `x-rl-daily-remaining` mám z cizího klienta, ne z dokumentace; když je Nexus nepošle, funguje jen zastavení při odpovědi 429. Limity jsem četl z výsledku hledání, stránku nápovědy jsem neotevřel.
- Poznámka uživatele k verzi (21. 11.): tuhle dávku považuje za velkou aktualizaci a chce posunout číslování verzí. Číslo vybere v C7; do té doby se verze v `Directory.Build.props` nemění a nadpis v `CHANGELOG.md` zůstává pracovní.

**O7** (21. 11., Opus)
- Stav: hotovo. Náhled `docs/previews/settings-tabs.html` (jeden soubor, bez kódu aplikace): klikací záložky, přepínač okna 1612 × 910 / 2560 × 1440 a edice FULL / OFFLINE, pod náhledem tabulka „kam se která volba přesunula“ a důvody.
- Záložky: 01 Game & library · 02 Nexus Mods (Account, Link mods, Download links) · 03 Updates · 04 Display · 05 Kept files · 06 Diagnostics · 07 FIXER; „Remove AFTERLIFE from this PC…“ dole pod seznamem záložek, oddělené.
- Rozhodl jsem sám: dialog má pevnou velikost 214 + 660 × 520 px a při větším okně neroste (nejdelší je Updates v Offline, 486 px); Game a Library v jedné záložce; v Offline je záložka Updates vidět se zámkem a zašedlými volbami (dnes je oddíl schovaný); na záložkách krátké údaje (součet Kept files, zelená tečka u funkčního klíče Nexusu); dole pod záložkami verze aplikace.
- Ověřeno: náhled v Chromiu, všechny záložky v obou edicích bez rolování.
- Pro O8: šablona do `Views/SettingsView.xaml`, výběr záložky v `SettingsVm` (zapamatovat poslední), texty beze změny kromě rozdělení dlouhých popisků na řádek + šedou poznámku.
- Doplněno 22. 11. podle zadání po F3: v Updates dva samostatné spínače (při startu, každou hodinu), „only switched-on“ zašedlé, když jsou oba vypnuté, a oddíl REQUESTS TO NEXUS MODS (dotazy denně a oranžové varování, v náhledu počítané stejným vzorcem jako `NexusLimits`). Upozornění u „Check every mod now…“ je jako v aplikaci jen v tooltipu.
- Pro uživatele: B3 v UKOLY.md.

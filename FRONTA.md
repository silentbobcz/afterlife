# AFTERLIFE: fronta pro modely (1. dávka)

Živá verze fronty. **Nejnovější stav dávky: větev `claude/kind-hopper-az2rax`** (O1, O2, O4, O6, F1, F2). Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md).
Úkoly pro uživatele: [`UKOLY.md`](UKOLY.md) v této větvi.

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev claude/kind-hopper-az2rax) a udělej balík F3.“
Doma na počítači: `git fetch origin claude/kind-hopper-az2rax` a `git checkout claude/kind-hopper-az2rax`, pak stejná věta.

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
| F3 | Fable | Kontrola aktualizací při 600+ modech | C6 | zadání potvrzeno, čeká na C6 |
| O7 | Opus | Přehlednější nastavení (návrh, bez kódu) | C6 | čeká na C6 |
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

Fable. Zadání potvrzeno uživatelem 21. 11. (C4). Čeká jen na C6: kam v nastavení přijdou nové volby. Soubory: `MainViewModel.Updates.cs`, `Nexus.cs` (`NexusUpdates`), nastavení, `NexusTests.cs`.
1. Zrušit strop 60 modů na jednu kontrolu: přečte se vše, co je na řadě, a kontrola se zastaví jen o limit Nexusu (`RateLimited`), zbytek příště. Před tím ověřit v dokumentaci Nexusu skutečné limity dotazů.
2. Pořadí: core mody a mody zapnuté ve vybraném profilu první, pak ostatní.
3. Tlačítko CHECK UPDATES použije seznam změněných modů (dnes ho obchází a čte všech 600+). Plná kontrola všech jako zvláštní volba.
4. Nastavení: kontrola při startu a každou hodinu ano/ne (core mody vždy); rozsah při startu (jen zapnuté mody, nebo všechny).
5. Pojistka: ke každému modu zvlášť datum posledního plného přečtení. Mezi tím jen rozdíly ze seznamu změn; jednou za N dní se mod přečte celý. N je v nastavení, výchozí 7. Mody rozložit rovnoměrně, aby nepřišly na řadu všechny ve stejný den. Dnes se „poslední kontrola“ přepisuje i při odpovědi „beze změny“, takže plné přečtení při pravidelném používání neproběhne nikdy.
6. Testy: výběr modů ke kontrole (rozdíly, pojistka po N dnech, rozložení, pořadí, zastavení o limit).

## O7. Přehlednější nastavení (návrh, bez kódu)

Opus. Čeká na C6. Dnes je nastavení jeden rolovací sloupec šířky 680 se šesti oddíly (`Views/Panels.xaml`, šablona `SettingsVm`).
1. Statický náhled nastavení s novým oddílem UPDATES (volby z F3) a s úpravami podle C6.
2. U každé změny jedna věta proč. Odkaz na náhled zapsat k C6.

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

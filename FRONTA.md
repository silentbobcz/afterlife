# AFTERLIFE: fronta pro modely (1. dávka)

Živá verze fronty (dřív v Claude Docs). Větev `claude/adoring-gates-k9aqtu`, pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md).
Úkoly pro uživatele: Claude Docs „AFTERLIFE: co čeká na tebe“ (https://claude.ai/artifact/CmwJyL6CakSib1ZHdXchN9).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev claude/adoring-gates-k9aqtu) a udělej balík O6.“

## Pravidla dávky

- Sestavení pro uživatele se nedělá, dokud nejsou hotové všechny úpravy a uživatel neřekne. Testy a kontrolní překlad si balík pouští sám.
- Na GitHub jen do pracovní větve; `main`, tagy a vydání jsou rozhodnutí uživatele (C3).
- Co nešlo vidět běžet: v předávce „Neověřeno“ a uživateli položka A v dokumentu úkolů.
- Předávka pod balíkem, nejvýš deset řádků.

## Stav balíků

| Balík | Model | Co | Čeká na | Stav |
|---|---|---|---|---|
| O1 | Opus | Překryvná okna rostou s oknem, okno začíná vystředěné | nic | hotovo `b1aa34c`, čeká na kontrolu |
| O2 | Opus | Dialog při vypnutí modu ukáže i to, co mod potřebuje | nic | hotovo `9400745`, čeká na kontrolu |
| O3 | Opus | Rešerše a návrhy seznamu modů (bez kódu) | nic | hotovo |
| O4 | Opus | Panel filtrů ve třech stavech | nic | hotovo `c75c70f`, čeká na kontrolu |
| O5 | Opus | Návrhy celého okna W1/W2/W3 (bez kódu) | nic | hotovo, odloženo jako alternativa |
| F1 | Fable | Hromadné propojení ručně přidaných ZIPů s Nexusem | nic | hotovo, ale **jen na počítači uživatele** (soubor `F1-link-mods-to-nexus.patch`); musí se dostat do větve |
| O6 | Opus | Tlačítko hromadného propojení i v nastavení Nexusu | F1 ve větvi | čeká |
| F2 | Fable | Řazení seznamu a virtualizace při 300+ modech | nic | čeká |
| F-kontrola | Fable | Kontrola O1, O2, O4, O6 a uzavření dávky | O6 | čeká |

## F1 do větve (uživatel nebo vlákno na jeho počítači)

Na počítači v klonu repozitáře: `git fetch origin claude/adoring-gates-k9aqtu`, `git checkout claude/adoring-gates-k9aqtu`,
`git am F1-link-mods-to-nexus.patch`, vyřešit konflikty (F1 mění i `MainWindow.xaml`, `Rows.cs`, `CHANGELOG.md`, které mezitím
změnily O1, O2 a O4), `dotnet test`, `git push`. Bez toho O6 a F-kontrola nemohou začít.

## O6. Hromadné propojení i v nastavení Nexusu

Opus, malý. Čeká na F1 ve větvi. F1 dal příkaz „Link all unlinked mods…“ jen do detailu modu, protože `Views/Panels.xaml` tehdy měnil O1.
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

## F-kontrola

Fable. Projít commity O1, O2, O4, O6 proti zadání a předávkám, nejvíc „Neověřeno“ a „Rozhodl jsem sám“; testy a sestavení; chyby opravit a zapsat. F1 a F2 nekontroluj (psal je Fable). Pak dávku uzavřít: trvalé věci do `docs/HOW-IT-WORKS.md` a `CHANGELOG.md`, tento soubor do `archiv/FRONTA-1-davka.md`.

## Předávky

**O1** (21. 11., Opus): seznamy v 15 překryvných oknech rostou o to, o co je okno vyšší než 910 px (`GrowWithWindow`); okno se zmenší na pracovní plochu už v konstruktoru, takže ho CenterScreen vystředí ve skutečné velikosti. Neověřeno ve Windows (A2).

**O2** (21. 11., Opus): dialog při vypnutí: závislé mody, „ONLY IT NEEDED, NOW ON“ (vlastní zaškrtávátko, výchozí ne), „IT NEEDS, STAYS ON“ ke čtení (`KeptRequirements`). Rozhodl jsem sám: core mody ani v informačním seznamu; kolekce se ptají dál vlastním dotazem. Neověřeno ve Windows (A3).

**O4** (21. 11., Opus): `ModFilter` (Infrastructure, čistá logika, 6 testů), `FilterPanelVm`, `Views/FilterPanel.xaml`, sloupec mezi PROFILES a MOD LIBRARY, tlačítko „⧩ FILTERS“ u hledání (cyklus otevřený → pruh → skrytý), stav v meta `ui.filterpanel`. Rozhodl jsem sám: automatické sbalení do pruhu pod šířkou okna 1500 px (ne podle šířky seznamu 760 px, to by sbalovalo už při výchozí velikosti); bez filtru „FIXER quests“ (balíček FIXERu se v datech nepozná); štítky kolekcí a OPTIONAL/REQUIRED nejsou mezi štítky. Ověřeno: 287 testů, sestavení aplikace. Neověřeno: vzhled a klikání ve Windows (A5).

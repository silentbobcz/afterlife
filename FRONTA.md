# AFTERLIFE: fronta pro modely (2. dávka)

Sestaveno 22. 11. 2026 po vydání 1.0.0-beta.1. **Nejnovější stav: větev `main`** (sloučeno 22. 11. včetně volného pořadí O11 a O12). Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md), úkoly pro uživatele v [`UKOLY.md`](UKOLY.md). První dávka je v `archiv/FRONTA-1-davka.md` (jen archiv, stav z ní nečti).

Spouštěcí věta: „Přečti FRONTA.md v repozitáři silentbobcz/afterlife (větev main) a udělej balík O15.“ Balík O17 jde pustit vedle něj (jiné soubory).

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
| O10 | Sonnet | Náhled volného pořadí v MODS a kategoriích | nic | hotovo, schváleno (B4) |
| O11 | Opus | Volné pořadí v kódu: tažení uvnitř MODS a kategorií, čísla pořadí, A→Z | nic | hotovo, zkontrolováno |
| O12 | Sonnet | Posun z klávesnice, „Load before…“ v Konfliktech, ztlumení modů bez archivů | nic | hotovo, zkontrolováno |
| F-pořadí | Fable | Kontrola O11 a O12 | nic | hotovo, vyzkoušeno uživatelem (A13) |
| O13 | Sonnet | Srozumitelnější texty dialogu při vypnutí modu | nic | návrh schválen (B5); do kódu jako O17 |
| O14 | Opus | Export a import load listu: návrh formátu souboru (bez kódu) | nic | hotovo, schváleno (B6) |
| O15 | Opus | Export a import load listu v kódu | nic | může začít |
| O16 | Sonnet | Tlačítka FILTERS a SORT: jiná barva a popisek, náhled | nic | hotovo, vyzkoušeno uživatelem (A14) |
| O17 | Opus | Nové texty dialogu při vypnutí modu v kódu a oprava výpočtu jeho částí | nic | může začít |
| F-kontrola 3 | Fable | Kontrola O15 a O17 | O15, O17 | čeká |

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

## Load list: export a import (O14, O15)

Proč: uživatel chce pořadí načítání vynést do souboru a zase načíst (záloha, přenos na jiný počítač, sdílení s někým dalším, ruční úprava v editoru). Není to dnešní EXPORT LIST (`ExportVm`: soupis aktivních modů s verzemi pro hlášení chyb); ten zůstává. Zadání uživatele z 22. 11.:
- Export v čitelné textové podobě s jasnou syntaxí, aby si ho člověk mohl upravit ručně; kolekce a kategorie jsou označené znaky.
- Import: kolekce a kategorie, které v knihovně nejsou, se založí a mody se k nim zatřídí.
- Mody ze souboru, které v knihovně nejsou: buď nabídnout ke stažení, nebo přeskočit (rozhodne uživatel podle návrhu). Mody v knihovně, které v souboru nejsou: návrh řekne, kam přijdou.

### O14. Návrh formátu (bez kódu)

Opus. Může začít hned. Výstup je dokument `docs/previews/load-list-format.md`, žádný kód.
1. Projdi, co pořadí dnes tvoří: profil (`ProfileState.Entries`, zapnuto/vypnuto), `BlockLayout` (bloky, kategorie), kolekce (zámek pořadí, kurátorovo pořadí), vrstvy „└“, CORE MODS, `modlist.txt`. Sepiš, co z toho do souboru patří a co ne.
2. Navrhni formát: jeden textový soubor, řádek na mod, značky pro kolekci a kategorii, komentáře, hlavička s verzí formátu. Ukaž celý příklad na knihovně o zhruba 15 modech se dvěma kategoriemi a jednou kolekcí. Zvaž, jestli být čitelný i pro jiné správce (prostý seznam archivů jako `modlist.txt`) a řekni proč ano nebo ne.
3. Čím se mod pozná při importu: id a soubor na Nexusu, když je mod propojený; jinak jméno a verze. Co když sedí jméno, ale jiná verze. Plná edice vs. Offline (Offline nemá id z Nexusu a nic nestahuje).
4. Pravidla importu jako tabulka případ → co se stane: kategorie chybí (založit), kolekce chybí (založit jako blok, nebo nabídnout přidání kolekce z Nexusu), mod chybí v knihovně, mod je v knihovně a není v souboru, mod je v zamčené kolekci, core mod v souboru, vrstva. U sporných případů dvě až tři možnosti s doporučením.
5. Co import mění: jen pořadí a zatřídění ve vybraném profilu, nebo i zapnuto/vypnuto; návrh náhledu před importem („co se změní“) a že do hry se nic nedostane před APPLY.
6. Kde to bude v okně (vedle EXPORT LIST a RESET ORDER) a texty tlačítek a hlášek.
Pro uživatele položka B6 v `UKOLY.md`: schválit formát a rozhodnout sporné případy.

### O15. Load list v kódu

Opus. Může začít hned: formát je schválen 23. 11. (B6) podle `docs/previews/load-list-format.md`, u bodů D1 až D7 platí doporučené možnosti A, tlačítka se jmenují EXPORT ORDER a IMPORT ORDER, poznámky kontroly v oddíle 8 jsou závazné (shoda jen podle jména je v náhledu vidět jednotlivě). Čtení a zápis formátu jako čistá logika v `Afterlife.Core` s testy (zápis a zpětné čtení dá totéž, ručně upravený soubor s chybou dá srozumitelnou hlášku s číslem řádku), napojení v okně novým souborem, zápis pořadí přes stávající `ApplyLayoutAsync`. Návod (oddíl 10 a 16), FAQ, CHANGELOG, položka A v `UKOLY.md`. Proč Opus a ne Sonnet: import zakládá kategorie a mění profil, chyba by přeházela pořadí.

## O16. Tlačítka FILTERS a SORT

Sonnet. Může začít hned. Uživatel 22. 11.: tlačítka FILTERS a ⇅ splývají s odkazy nahoře; chce jinou barvu a u řazení slovo SORT. Dnes: `FilterPanelVm.ButtonLabel` („⧩ FILTERS“), `MainViewModel.Sorting.cs` `SortLabel` („⇅ LOAD ORDER“), tlačítka v `MainWindow.xaml` vedle hledání.
1. Statický náhled `docs/previews/filter-sort-buttons.html`: horní část okna s odkazy a hledáním, tři varianty barvy tlačítek jen z tokenů `App.xaml` (například žlutý obrys, fialová jako kategorie, plné tmavé tlačítko se světlým textem), vždy ve stavu klidném, aktivním (filtr zapnutý, seznam seřazený) a pod myší.
2. Popisek řazení: „⇅ SORT · LOAD ORDER“, „⇅ SORT · NAME“ … a varianta kratší; ukaž, jak se vejde na nejmenší šířku okna.
3. Do kódu až po schválení (položka B7 v `UKOLY.md`); pak je to změna stylu dvou tlačítek a jednoho textu, s úpravou návodu tam, kde se tlačítko jmenuje.

## O17. Dialog při vypnutí modu: nové texty a oprava výpočtu

Opus. Může začít hned; texty schválil uživatel 23. 11. (B5). Proč Opus: mění se i výpočet toho, co dialog nabízí.
1. Texty podle tabulky v `docs/previews/switch-off-dialog-texts.md`, všechny varianty (jeden mod, výběr, kategorie, všechny mody, core mody, druhý dialog kolekce). Jednotné a množné číslo dvěma větami, žádné „mod(s)“. Stejné znění dostanou i zaškrtávátka v „DELETE EVERYWHERE?“ a „REMOVE FROM GAME?“, která dnešní text sdílejí.
2. Věta pod nadpisem každé části: nové místo v `ConfirmVm` (`ViewModels/Overlays/Dialogs.cs`) a v šabloně `Views/Dialogs.xaml`, styl `Meta`, zalamování. Dlouhé jméno modu v nadpisu zkrať na zhruba 30 znaků s výpustkou.
3. Oprava výpočtu: části „NOT NEEDED ANY MORE“ a „STAYS ON“ se dnes počítají jen z vypínaného modu (`OverviewBuilder.UnneededRequirements` a `KeptRequirements` dostávají jen `ids`). Když je zaškrtnuté vypnutí závislých modů, počítej je z celé množiny, která se opravdu vypne; po změně zaškrtnutí se obě části přepočítají. Logika patří do `Afterlife.Infrastructure` s testy (mod A potřebuje L, mod B potřebuje L a A: vypnutí A i B nabídne L).
4. Kolekce: dva dialogy po sobě zůstávají; Cancel ve druhém dál ruší celé vypnutí, ale text druhého dialogu to řekne („Cancel leaves the collection on.“). Výchozí zaškrtnutí druhého dialogu sjednoť s dialogem jednoho modu (nezaškrtnuto).
5. CHANGELOG, návod (oddíl 9), položka A v `UKOLY.md` s třemi příklady k vyzkoušení.

## F-kontrola 3

Fable. Projít O15 a O17 proti schváleným návrhům a předávkám; u O15 hlavně: zápis a zpětné čtení dá totéž, import nemění nic mimo vybraný profil kromě kategorií a pořadí bloků, záloha před importem vzniká, chybný soubor nic nezmění. Testy, překlad obou edic, opravy zapsat. Pak dávku uzavřít podle skillu `ukoly-a-fronty` (fronta má přes 20 kB: hotové balíky a předávky do `archiv/`).

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


**O10** (22. 11., Sonnet jako podřízený agent; kontrola Fable)
- Stav: hotovo. `docs/previews/free-load-order.html`: čísla pořadí, čára při tažení, A→Z s potvrzením, vrstva s rodičem, přepínač LOAD ORDER / ⇅ NAME.
- Kontrola opravila větu v nápovědě čísla (vyhrává mod výš, ne níž). Viděna horní část náhledu; čára při tažení a dialog A→Z okem neprojity.
- Pro uživatele: B4.

**O11** (22. 11., Opus jako podřízený agent; kontrola Fable)
- Stav: hotovo, sloučeno do `main`; psáno bez čekání na B4. Jádro `Afterlife.Core/LoadOrder.cs` (`MoveGroup`, `SortSection`, `Arrange`, převzetí starých profilů), okno `MainViewModel.LoadOrder.cs`, styly `Views/LoadOrder.xaml`, testy `FreeLoadOrderTests.cs`.
- Rozhodnuto při výrobě: profil bez vlastního pořadí se dál ukazuje podle názvu a nic se nezapisuje, vlastním se stane prvním tažením (do té doby nový mod padá podle abecedy); každý profil staré knihovny se jednou ukáže podle názvu (klíč `order.takeover`); vrstva sedí vždy v sekci svého modu; A→Z jen u profilu, který jde upravit.
- Kontrola: kolize názvu testovací třídy opravena; logika přesunu, řazení sekce a převzetí bez nálezu. Vědomě ponecháno: když dřívější aktualizace přejmenovala archivy, první start srovná pořadí archivů v profilu a APPLY ukáže změnu `modlist.txt` bez zásahu uživatele.
- Ověřeno: sestavení obou edic a testy na GitHubu. Neověřeno: vše v okně (A13).

**O12** (22. 11., Sonnet jako podřízený agent; kontrola Fable)
- Stav: hotovo, sloučeno do `main`. `LoadOrder.MoveInSection` a `IsOrderedArchive`, `MainViewModel.OrderKeys.cs`, tlačítko LOAD BEFORE v záložce Conflicts, tlumené číslo u modů bez archivů.
- Rozhodnuto při výrobě: klávesy berou zaškrtnuté mody, jinak vybraný řádek; přeskakují i řádky skryté hledáním; vítěz konfliktu se hledá podle přesného jména modu (u dvou verzí téhož jména první); tlumená barva je `TagLine`.
- Kontrola: odstraněna zbloudilá vsuvka v `MainWindow.xaml.cs`, která by nešla přeložit. Ověřeno: sestavení a 335 testů. Neověřeno: vše v okně (A13), hlavně fokus kláves v seznamu a čitelnost tlumeného čísla.

**O13** (22. 11., Sonnet jako podřízený agent; kontrola Fable)
- Stav: návrh textů v `docs/previews/switch-off-dialog-texts.md`, v kódu nic. Čeká na B5.
- Kontrola potvrdila dvě věci v logice, které návrh textů neřeší a patří k zapracování: (1) části „only it needed“ a „stays on“ se počítají jen z vypínaného modu, bez závislých modů, které se zaškrtnutou volbou vypnou zároveň (`OverviewBuilder.UnneededRequirements`, `KeptRequirements` dostávají jen `ids`); (2) u kolekce jdou dva dialogy po sobě a Cancel ve druhém zruší celé vypnutí (`MainViewModel.Collections.cs`). Zapracování textů potřebuje malé místo pro větu pod nadpisem části (`ConfirmVm`, `Dialogs.xaml`).

**Předávka O16** (22. 11., Sonnet)
- Stav: hotovo. Uživatel vybral B (fialová) a dlouhý popisek; v kódu styl `ViewButton` v `App.xaml` a popisek „⇅ SORT · režim“.
- Změněno: `docs/previews/filter-sort-buttons.html` (nový, bez kódu aplikace).
- Obsah: dnešní vzhled a tři varianty (A žlutý obrys, B fialová, C tmavé plné) ve stavu klidném, aktivním a pod myší; popisek „SORT · režim“ dlouhý i krátký; pásy pro šířku panelu 760 a 640 px.
- Doporučení: C, dlouhý popisek (v klidu nejtišší, aktivní stav žlutě).
- Ověřeno: stránka se v prohlížeči načte a vykreslí.
- Neověřeno: vzhled ve WPF, přesná šířka panelu při okně 1612 px.
- Kontrola (Fable): kód bez nálezu, sestavení a 335 testů na GitHubu. Neověřeno: vzhled v okně a šířka popisku při nejmenším okně (A14).

**O14** (22. 11., Opus)
- Stav: hotovo. Návrh `docs/previews/load-list-format.md`, bez kódu: formát `AFTERLIFE LOAD LIST 1`, sekce `@core`/`@collection`/`@mods`/`@category`, řádky `+`/`-` s volitelnými poli `verze | nexus= | file= | md5=`, příklad na 16 modech.
- Zjištěno v kódu: kategorie a pořadí bloků (`BlockLayout`, `ui.layout`) jsou jedny pro celou knihovnu, ne pro profil; import do jednoho profilu tedy mění zatřídění ve všech. Návrh to říká v náhledu (případ 9).
- Rozhodl jsem sám: čitelnost pro jiné správce ne (důvod v oddíle 2); `modlist.txt` a vítězové jednotlivých souborů do souboru nejdou; řádek s chybou zastaví celý import; členství v kolekci soubor nikdy nemění; tlačítka vedle EXPORT LIST (kontrola je přejmenovala na EXPORT ORDER a IMPORT ORDER).
- Sporné body D1 až D6 s doporučením v oddíle 4; podklad pro O15 v oddíle 7.
- Ověřeno: jen dokument, testy netřeba. Pro uživatele: B6.
- Kontrola (Fable): návrh dává smysl a odpovídá zadání. Doplněno: případy 16 (pořadí sekcí, které okno neumí) a 17 (profil ve výchozím pořadí hry), bod D7 (vrstvy jen jako komentář), přejmenování tlačítek, poznámky v oddíle 8 (shoda jen podle jména musí být v náhledu vidět jednotlivě).

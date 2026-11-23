# AFTERLIFE: fronta pro modely (4. dávka)

Sestaveno 23. 11. 2026 z námětů a odložených věcí v [`UKOLY.md`](UKOLY.md) (stav po vydání 1.0.0-beta.1) a z odložených nálezů revize z 19. 11. Pravidla a kontroly v [`CLAUDE.md`](CLAUDE.md). Starší dávky jsou v `archiv/` (jen archiv, stav z nich nečti).
Další volná čísla: S2 O27 F6

Nejnovější kód: větev `main`. Každý balík má vlastní větev a vlastní pracovní složku (`git worktree add <složka> -b <větev> origin/main`); hlavní klon zůstává na `main`. Do `main` se slučuje jen na výslovný pokyn uživatele.
Kontroly: `dotnet test tests/Afterlife.Tests` a `dotnet build src/Afterlife.App -p:EnableWindowsTargeting=true`; na počítači uživatele není .NET 10 SDK, tam `git push` větve a `gh workflow run build.yml --ref <větev>`.
Skilly: alt, ukoly-a-fronty, kohn, afterlife, u náhledů grafika-cyberpunk (zdroje v `D:/_AI/_skills/<skill>/SKILL.md`, když nejsou nahrané).

## Pravidla pro všechny balíky

- Vše v repozitáři anglicky (kód, komentáře, commity, texty v okně); bez jmen modelů a čísel balíků v kódu.
- Co volá Nexus Mods, začíná `LockedOffline(...)`; edice Offline se nesmí nikam připojit.
- Na žurnálované nasazení (`Deployment.cs`, `Deploy.cs`) a obnovu po pádu se nesahá.
- Soubory nad 600 řádků (C#) nebo 800 (XAML) nedostanou nový kód: založ soubor vedle. Platí hned pro `Nexus.cs`, `Overview.cs`, `MainViewModel.cs` a `MainViewModel.Collections.cs`.
- Každý balík s kódem: jeden řádek do `CHANGELOG.md` pod nejvyšší nadpis verze, 1–3 řádky do `docs/HOW-IT-WORKS.md`, když se mění stavba.
- Vzhled nejdřív jako statický náhled v `docs/previews/`, kód až po schválení. Barvy a písma jen z tokenů v `App.xaml`.
- Posuzuj při 300 až 600 modech v knihovně.
- Okno WPF mimo Windows nespustíš: co jsi neviděl běžet, napiš do předávky jako „Neověřeno“.

## Stav balíků

| Balík | Kde běží | Co | Stav |
|---|---|---|---|
| F4 | Fable | Kategorie NEW pro nové mody: návrh a náhled (bez kódu) | ☐ čeká |
| F5 | Fable | Přesun knihovny do jiné složky z aplikace | ☐ čeká |
| O22 | sub-Opus | Chyba redscriptu: čtení logu a nalezení viníka (bez okna) | ☐ čeká |
| O23 | sub-Opus | Chyba redscriptu: náhled okna a texty (návrh, bez kódu) | ☐ čeká |
| O24 | sub-Opus | Chyba redscriptu: okno a tlačítko v aplikaci | ☐ čeká na O22 a B11 |
| O25 | sub-Opus | Import load listu: fronta stránek pro účty zdarma | ☐ čeká |
| O26 | sub-Opus | Drobné opravy z revize: počítadlo „pracuji“, jeden klient HTTP | ☐ čeká |
| S1 | sub-Sonnet | Texty: „mod(s)“ ve hláškách, „selected“ a „checked“ v návodu | ☐ čeká na O24, O25 a O26 |
| F-kontrola | Fable | Kontrola dávky a její uzavření | ☐ čeká na vše |

## F4. Kategorie NEW pro nově nainstalované mody (návrh, bez kódu)

Proč Fable: kategorie nese pořadí načítání; návrh nesmí potichu přeskládat načítání u profilů se stovkami modů.
Může začít hned. Zadání uživatele je v `UKOLY.md` (námět z 23. 11.): nový mod padá do kategorie NEW, ta je ve výchozím stavu nahoře, jde posunout jako jiná kategorie, prázdná se schová a pozici si pamatuje.

1. Přečti v `docs/HOW-IT-WORKS.md` oddíl „The list and the load order“ a k němu `LoadOrder` (Core), `MainViewModel.LoadOrder.cs`, `MainViewModel.Blocks.cs` a to, kam dnes padá nový mod (import, stažení, kolekce, schránka FIXER, import load listu).
2. Napiš `docs/previews/new-category.md` (anglicky): jak NEW funguje a jak odpovídá na čtyři otázky ze zadání: (a) pořadí načítání: mod v NEW se načítá kde? Vyber řešení, při kterém instalace nového modu nezmění pořadí načítání ostatních modů, a napiš, co uvidí uživatel v číslech řádků; (b) mody doplněné importem load listu; (c) aktualizace existujícího modu do NEW nepatří; (d) instalace mnoha modů najednou (kolekce o stovkách modů). Dále: NEW a profily (kategorie jsou společné všem profilům), NEW a mody vypnuté po instalaci, co se stane se stávající knihovnou po aktualizaci aplikace (nic se nesmí pohnout).
3. Kde má návrh dvě rozumné cesty, popiš obě s tím, co přinesou a co stojí, a jednu doporuč (nejvýš tři taková rozhodnutí).
4. Udělej statický náhled `docs/previews/new-category.html` ve stylu ostatních náhledů ve složce: seznam s NEW nahoře se třemi mody, NEW posunutá níž, a stav po přetažení posledního modu (NEW zmizí).
5. Do předávky napiš seznam souborů a odhad kroků pro kód (podklad pro balík v další dávce) a do `UKOLY.md` doplň položku B10 (odkaz na oba soubory a rozhodnutí z bodu 3).

Hotovo, když: oba soubory jsou ve větvi, čtyři otázky mají odpověď opřenou o kód (odkazy na soubory a metody) a B10 říká, co má uživatel rozhodnout.

## F5. Přesun knihovny do jiné složky z aplikace

Proč Fable: stěhují se desítky GB dat uživatele; přerušení, plný disk a souběh s nasazením nesmí knihovnu ani hru poškodit.
Může začít hned. Dnes aplikace knihovnu přesunout neumí (návod radí starou složku nechat); cesta je v nastavení (`AppSettings`, klíč `libraryRoot`) a sdílejí ji obě edice.

1. Zmapuj, co na cestě ke knihovně visí: `AppSettings.cs`, `Coordinator.cs`, `CoordinatorHost.cs`, `LibraryService.cs`, databáze, schránka FIXER (`Fixer.cs`, `status.json`), stažené archivy, složka Removed, exporty, a hlavně jestli nasazené soubory ve hře odkazují do knihovny (pevné odkazy, kopie, záznam žurnálu).
2. Napiš službu přesunu v novém souboru v `Afterlife.Infrastructure`: předběžná kontrola (cíl prázdný nebo neexistuje, není ve složce hry ani hra v něm, dost místa, práva k zápisu, žádná nedokončená změna v žurnálu), na stejném disku přejmenování, jinak kopie s ověřením a smazání zdroje až po úspěchu a po zápisu nové cesty do nastavení. Přerušený přesun musí jít dokončit nebo vrátit: stav si zapisuj tak, aby po pádu bylo jasné, která složka platí.
3. Pokud nasazení ve hře závisí na umístění knihovny, vyřeš to bez změny `Deployment.cs` a `Deploy.cs` (například přesun povol jen ve stavu „nic nenasazeno / vše použito“ a řekni to uživateli); když to bez zásahu do nich nejde, zastav bod, popiš důvod v předávce a zapiš položku C do `UKOLY.md`.
4. Oprav přitom nález S7 z revize (`Coordinator.cs`, kontrola „knihovna nesmí být ve složce hry“): cesta ke hře končící zpětným lomítkem a opačný směr (hra uvnitř knihovny) při změně složky hry.
5. Okno: v Nastavení v záložce s knihovnou tlačítko MOVE LIBRARY, výběr složky, potvrzení přes stávající `ConfirmVm` (odkud, kam, velikost, že hra zůstane beze změny) a průběh; během přesunu jsou ostatní akce zamčené. Nový kód dej do nového souboru vedle `MainViewModel.Settings.cs`.
6. Testy v novém `LibraryMoveTests.cs`: stejný disk, cílová složka neprázdná, cíl ve složce hry, přerušení v polovině a dokončení, přerušení a návrat, cesta s lomítkem na konci.
7. Uprav `docs/USER-GUIDE.md` (oddíl Updating AFTERLIFE a místo, kde se píše, že přesun nejde) a `docs/FAQ.md`.

Hotovo, když: testy procházejí, obě edice se sestaví a předávka říká, co se smí a nesmí dít se hrou během přesunu.

## O22. Chyba redscriptu: čtení logu a nalezení viníka (bez okna)

Může začít hned. Zadání: námět z 23. 11. v `UKOLY.md`. Když překlad redscriptu spadne, nefungují všechny skriptové mody; `<hra>/r6/logs/redscript_rCURRENT.log` pak končí oddílem `REDScript compilation has failed` se seznamem modů „caused by“ a nad ním jsou řádky chyb s cestou k souboru `.reds`, řádkem a sloupcem.

1. V `Afterlife.Core` napiš čistý rozbor textu logu (nový soubor, např. `RedscriptLog.cs`): výsledek překladu (v pořádku / selhal / log chybí nebo je starší než poslední spuštění), seznam chyb (soubor, řádek, sloupec, text) a jména ze seznamu „caused by“. Musí snést log v jiném kódování, prázdný a oříznutý.
2. V `Afterlife.Infrastructure` napiš přiřazení k modům knihovny: nejdřív přesně podle cesty souboru z chyby proti souborům, které AFTERLIFE do hry nasadil (záznam nasazení se jen čte), teprve potom podle jména složky v `r6/scripts`. Soubor, který nepatří žádnému modu z knihovny, označ jako ručně instalovaný. Odhad podle podobnosti názvu nedělej.
3. Výsledek je jeden záznam pro okno: viníci z knihovny (mod, soubory, chyby), viníci mimo knihovnu, a u každého, jestli je zapnutý ve vybraném profilu.
4. Sestav text „dotaz pro AI“, který si uživatel zkopíruje: verze hry, viníci s verzí, chybové řádky, seznam zapnutých frameworků. Text musí projít `LogPrivacy.Scrub` (cesty obsahují jméno uživatele).
5. `FrameworkCatalog.InferFromContent` neměň: pravidlo na jeden typ ve skriptu tenhle problém neřeší (výslovně v zadání).
6. Testy v `RedscriptLogTests.cs` na vzorových lozích uložených v testech jako text: úspěšný překlad, jedna chyba jednoho modu, víc modů, chyba v ručně instalovaném skriptu, oříznutý log. Když uživatel dodal skutečný log (D4 v `UKOLY.md`), použij ho po odstranění cest a jmen; když ne, vyjdi ze známého formátu a napiš to do „Neověřeno“.

Hotovo, když: testy procházejí a záznam z bodu 3 jde získat jedním voláním s cestou ke hře a vybraným profilem. Okno je balík O24.

## O23. Chyba redscriptu: náhled okna a texty (návrh, bez kódu)

Může začít hned, vedle O22 (nesahá do kódu).

1. Projdi `docs/previews/` (styl náhledů) a v aplikaci `ConfirmVm`, lištu pod seznamem a nabídku LIST & ORDER (`Views/ToolsMenu.xaml`), ať návrh používá hotové prvky.
2. Udělej `docs/previews/redscript-error.html` se čtyřmi stavy: (a) upozornění po návratu ze hry nebo po startu aplikace, že překlad skriptů selhal (pruh nebo řádek ve stavové části, ne vyskakovací okno přes všechno); (b) okno s viníky z knihovny: mod, verze, chybový řádek, zaškrtávátko „switch off“ a tlačítko, které je vypne přes běžné vypnutí modu (dialog se závislostmi zůstává); (c) viník mimo knihovnu (ručně instalovaný skript): jen název a cesta, bez vypínání; (d) blok „Ask an AI“ s textem k zkopírování a tlačítkem COPY.
3. Navrhni, odkud se rozbor pouští ručně (tlačítko ANALYZE REDSCRIPT ERROR): doporuč jedno místo a druhé uveď jako alternativu.
4. Texty všech stavů anglicky do `docs/previews/redscript-error-texts.md`; věcně, bez strašení, vždy s větou, že AFTERLIFE mod jen vypne a nic nemaže.
5. Do `UKOLY.md` doplň položku B11: odkaz na náhled a co rozhodnout (umístění tlačítka; jestli se log čte sám po návratu ze hry a po startu, nebo jen tlačítkem).

Hotovo, když: náhled se otevře dvojklikem bez sítě a B11 má rozhodnutí s možnostmi.

## O24. Chyba redscriptu: okno a tlačítko v aplikaci

Čeká na O22 (rozbor) a B11 (schválený náhled). Mění `MainViewModel.Apply.cs` (návrat ze hry) a nabídku pod seznamem: nepouštět vedle O25 a O26 ve stejné pracovní kopii.

1. Podle schváleného náhledu přidej okno (`Overlays`, nový soubor) nad záznamem z O22 a nový dílčí soubor `MainViewModel.Redscript.cs`.
2. Ruční spuštění z místa schváleného v B11; automatické čtení podle B11 (po `LaunchGame` v `MainViewModel.Apply.cs` a po startu), jen když je log novější než poslední zpracovaný, aby se upozornění neopakovalo.
3. Vypnutí viníků jde stávající cestou (`WithRelatedOffAsync`), tedy s dialogem závislostí a bez APPLY; uživatel pak použije změny sám.
4. Tlačítko COPY dá do schránky text z O22; nic se nikam neodesílá, v obou edicích stejně.
5. Kapitola do `docs/USER-GUIDE.md` a otázka do `docs/FAQ.md` („Script mods stopped working“).

Hotovo, když: sestavení a testy projdou; v předávce je „Neověřeno: okno ve Windows, skutečný pád překladu ve hře“ a v `UKOLY.md` položka A s kroky, jak chybu vyvolat (zapnout zastaralý skriptový mod).

## O25. Import load listu: fronta stránek pro účty zdarma

Může začít hned. Dnes účet bez Premium dostane při IMPORT ORDER u chybějících modů jen hlášku a odkazy (`MainViewModel.LoadList.cs`, kolem `IsPremium`); kolekce přitom účet zdarma vedou stránku po stránce.

1. Najdi, jak kolekce vedou účet zdarma přes stránky modů (`MainViewModel.Collections.cs`, `CollectionOverlays.cs`, hledej `IsPremium`) a jak aplikace přebírá odkaz „Mod Manager Download“.
2. Společnou část vytáhni do nového souboru (do `MainViewModel.Collections.cs` nic nepřidávej, je nad limitem) a použij ji pro chybějící mody z load listu: seznam chybějících se stavem (čeká, stránka otevřena, staženo, přeskočeno), otevření stránky souboru na Nexusu, převzetí staženého souboru do knihovny vypnutého, a po dokončení fronty nabídka načíst stejný load list znovu.
3. Vazba jen potvrzená: do fronty jdou řádky load listu, které nesou soubor na Nexusu; řádek jen se jménem zůstává odkazem.
4. Šetři dotazy na Nexus (limit 500 za hodinu na klíč): na řádek nejvýš to, co už dnes dělá cesta pro Premium.
5. V edici Offline zůstává vše zamčené (`LockedOffline`). Testy na čistou část (stavy fronty) a úprava `docs/USER-GUIDE.md` u IMPORT ORDER.

Hotovo, když: testy a sestavení obou edic projdou; „Neověřeno: průchod s účtem zdarma“ jde do `UKOLY.md` jako položka A.

## O26. Drobné opravy z revize: počítadlo „pracuji“, jeden klient HTTP

Může začít hned. Nálezy S6 a E12 z revize 19. 11.

1. S6: `IsBusy` v `MainViewModel.Ui.cs` je jeden přepínač; když běží dvě akce přes sebe, první dokončená odemkne tlačítka. Udělej z něj počítadlo se stejnou vlastností navenek; projdi všechna místa, která ho nastavují, ať každé zvýšení má snížení i při výjimce.
2. E12: `Nexus.cs` zakládá nový `HttpClient` na každé stahování a view modely nového klienta Nexusu na každou akci. Zaveď jednoho sdíleného klienta (nový soubor, `Nexus.cs` je nad limitem), klíč a hlavičky po požadavku, ne na klientovi; edice Offline klienta nikdy nevytvoří.
3. Test na počítadlo (dvě překrývající se akce) a kontrola, že `NexusTests.cs` prochází beze změny chování.

Hotovo, když: testy a sestavení obou edic projdou.

## S1. Texty: „mod(s)“ a „selected“ / „checked“

Čeká na O24, O25 a O26 (mění hlášky ve stejných souborech). Při ručním pouštění přibal k O26 a udělej jako poslední.

1. Hlášky s „mod(s)“ (asi 49 výskytů v `src/Afterlife.App/ViewModels/`): nahraď správným tvarem podle počtu jednou sdílenou pomocnou funkcí; text beze změny významu.
2. `docs/USER-GUIDE.md` a `docs/FAQ.md`: pro zaškrtnuté řádky všude „checked“, „selected“ jen pro vybraný profil a vybraný řádek; stejně v textech okna, kde se to plete.
3. Ověř, že žádný test neporovnává změněný text; pokud ano, uprav test.

Hotovo, když: v `src` není „mod(s)“, testy projdou a web se z dokumentů sestaví.

## F-kontrola

Projít commity dávky proti zadání a předávkám (nejvíc „Neověřeno“ a „Rozhodl jsem sám“), spustit kontroly na GitHubu, nálezy opravit a zapsat. Při výrobě jedním vláknem ji nahrazuje kontrola po každém agentovi. Na konci dávku uzavřít: přenést, co má žít dál (`docs/HOW-IT-WORKS.md`, `CHANGELOG.md`, položky A v `UKOLY.md`), a soubor přesunout do `archiv/FRONTA-4-davka.md` (v archivu nechat tři poslední dávky).

## Co do fronty nepatří

- Kód kategorie NEW: až po schválení B10, balík v další dávce (podklad dá předávka F4).
- Z revize 19. 11. zůstává odloženo: S5 (úklid osiřelých složek v knihovně: mazání dat, nejdřív rozhodnout, jestli mazat, nebo jen ukázat), E10 (přesun do Removed přejmenováním), rozdělení `ImportAnalyzer.Analyze` a `SnapshotBuilder.Build`. Nízký přínos, další dávka.
- Odpověď pisateli na Nexusu: čeká na D1.
- Přestavba okna (W1 až W3), styl MUTED, hustší řádek, ruční závislosti pro Offline: odložené nebo neschválené, bez pokynu se neotvírají.
- Plná edice na Nexus Mods: čeká, až Nexus zaregistruje aplikaci.
- Sestavení k vyzkoušení a vydání: jen na pokyn uživatele.

## Jak pustit

- **Výroba jedním vláknem (doporučeno), model Fable:** „Přečti `D:/_AI/afterlife/repo/FRONTA.md` a vyrob vše, co nečeká.“ Vlákno udělá F4 a F5 samo a O22, O23, O25 a O26 pustí jako podřízené agenty vedle sebe.
- Po schválení B11: „Přečti `D:/_AI/afterlife/repo/FRONTA.md` a vyrob O24 a S1.“
- Ručně po jednom, model Opus: „Přečti `D:/_AI/afterlife/repo/FRONTA.md` a udělej balík O22.“ Vedle sebe mohou běžet O22, O23, O25 a O26 (každý ve vlastní pracovní složce); F4 a F5 patří na Fable.

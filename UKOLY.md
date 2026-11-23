# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 23. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A18 hotovo“ nebo „C11 první možnost“) v kterémkoli vláknu.
Další volná čísla: A26 B12 C12 D5 · balíky S2 O27 F6

**Stav (23. 11.):** verze **1.0.0-beta.1 je vydaná** na GitHubu (<https://github.com/silentbobcz/afterlife/releases/tag/v1.0.0-beta.1>): instalátor a zip plné edice a Offline zip, každý se `.sha256`. Všechno je v `main`, žádná pracovní větev nezůstala.

**Fronta modelů:** [FRONTA.md](FRONTA.md) (`D:/_AI/afterlife/repo/FRONTA.md`), čtvrtá dávka. **Stav 23. 11.:** hotové a zkontrolované je vše kromě O24 (okno chyby redscriptu, čeká na tvé B11) a kódu kategorie NEW (čeká na B10). Vše je sloučené v `main`. Napiš „sestavení“, až budeš chtít zkušební verzi k A22 až A25.

**Námět (23. 11., od uživatele): analýza chyby redscriptu.** Spadne-li překlad redscriptu, přestanou fungovat **všechny** skriptové mody, ale AFTERLIFE o tom neví a uživatel vidí jen poruchu nějakého jiného modu. Chyba je přitom čitelná: `<hra>/r6/logs/redscript_rCURRENT.log` končí oddílem `REDScript compilation has failed` a jmenovitým seznamem „caused by“ modů. Námět: po spuštění hry (nebo tlačítkem ANALYZE REDSCRIPT ERROR) log přečíst, viníka najít v knihovně a nabídnout vypnutí; co model nerozluští sám, vypsat jako hotový dotaz pro AI, který si uživatel zkopíruje. Nedělat to záplatou v `FrameworkCatalog.InferFromContent` (pravidlo na jeden typ v textu skriptu) — to řeší jeden mod, ne třídu problému.

**Námět (23. 11., od uživatele): kategorie NEW pro nově nainstalované mody.** Dnes nový mod padá na konec volného bloku MODS a mezi stovkami modů se ztratí. Námět: každý nově nainstalovaný mod spadne do kategorie NEW, která je ve výchozím stavu úplně nahoře; uživatel ji může posunout jako jinou kategorii a mody z ní přetahuje, kam patří. Prázdná NEW se schová, pozici si pamatuje a s dalším novým modem se objeví na stejném místě (rozhodnuto 23. 11.). Před návrhem ověřit: jak NEW ovlivní pořadí načítání (kategorie nese pořadí, NEW nahoře nesmí potichu přeskládat načítání), co s mody z importu load listu, aktualizací existujícího modu (ta do NEW nepatří) a s instalací více modů najednou (kolekce).

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A22 | Volitelný požadavek z Nexusu (oprava `2317e5b`) | Příští zkušební sestavení plné edice, mod RedFileSystem v seznamu | 1. Otevři AFTERLIFE s klíčem Nexusu. 2. Najdi RedFileSystem a podívej se na jeho požadavky. 3. Nesmí hlásit „missing: RedData“; napiš, co ukazuje. | ☐ čeká na sestavení |
| A23 | Přesun knihovny | Zkušební sestavení → Settings → 01 · Game & library → MOVE… | 1. Vyber prázdnou složku na stejném disku a potvrď; má to být hned a seznam modů zůstat stejný. 2. Totéž na jiný disk; sleduj procenta, po dokončení zkontroluj, že stará složka zmizela a hra jde spustit s mody. 3. Napiš, co se stalo. Neověřeno: okno, velká knihovna mezi disky. Pracuje se skutečnou knihovnou: napoprvé zkus stejný disk. | ☐ čeká na sestavení |
| A24 | Import load listu s účtem zdarma | Zkušební sestavení plné edice → LIST & ORDER → IMPORT ORDER | Jen pokud máš po ruce účet Nexus bez Premium: 1. Načti load list, ve kterém chybí mod se souborem z Nexusu. 2. Klikni „Get from Nexus, page by page“ a na stránce Mod Manager Download. 3. Po posledním souboru LOAD THE LIST AGAIN. Napiš, kde se to zadrhlo. | ☐ čeká na sestavení |
| A25 | Nové popisky a hlášky | Zkušební sestavení, běžná práce | Hlášky už nepíšou „mod(s)“. Přejmenováno: „Check all mods“ (dřív Select all mods), „REMOVE CHECKED FROM GAME“, v nápovědě „CHECKING AND MOVING“. Napiš jen, kdyby některá věta zněla špatně nebo ti nový popisek vadil. | ☐ čeká na sestavení |

## B. Promyslet a rozhodnout

| # | Co | Kde to najdu | Co mám rozhodnout | Stav |
|---|---|---|---|---|
| B10 | Kategorie NEW: návrh a náhled | [docs/previews/new-category.html](docs/previews/new-category.html) (dvojklik, nahoře čtyři stavy k přepnutí); popis v [new-category.md](docs/previews/new-category.md) | Tři rozhodnutí, napiš třeba „B10: 1A 2A 3A“. **1. Kde se mod v NEW načítá:** A (doporučuji) tam, kde NEW v seznamu stojí; nahoře tedy nový mod vyhrává střety souborů, dokud ho nepřesuneš, a řádek to ukáže · B NEW se ukazuje nahoře, ale načítá se vždy poslední (nic nepřebije, ale jediné místo, kde pořadí v seznamu neplatí). **2. Kam jde NEW posunout:** A (doporučuji) mezi bloky (nad a pod kolekce a MODS) · B i mezi kategorie uvnitř MODS (asi dvojnásobek práce). **3. Vypínač v Nastavení:** A (doporučuji) žádný, kdo NEW nechce, zmáčkne MOVE ALL TO MODS · B vypínač „nové mody do NEW“. | ☐ čeká |
| B11 | Chyba redscriptu: náhled okna a texty | [docs/previews/redscript-error.html](docs/previews/redscript-error.html) (dvojklik); texty v [redscript-error-texts.md](docs/previews/redscript-error-texts.md) | Schválit vzhled a texty a dvě rozhodnutí, napiš třeba „B11: 1A 2A“. **1. Kde je tlačítko ANALYZE REDSCRIPT ERROR:** A (doporučuji) první řádek nabídky LIST & ORDER · B odkaz pod JACK IN (vždy na očích, ale řádek navíc) · C v Nastavení. **2. Kdy se log čte sám:** A (doporučuji) po návratu ze hry a při startu aplikace, jen když je log novější než naposledy zpracovaný · B jen po návratu ze hry spuštěné z AFTERLIFE · C jen tlačítkem. Po schválení se pouští balík O24. | ☐ čeká |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |
| D4 | Log s chybou redscriptu (nepovinné, zpřesní balík O22) | `<složka hry>/r6/logs/redscript_rCURRENT.log` z doby, kdy skripty nefungovaly (hra ho při dalším spuštění přepíše). Vlož text do vlákna, které frontu vyrábí; cesty se jménem uživatele se před uložením do repozitáře odstraní. | ☐ čeká |

## Fronty modelů

Zadání a spouštěcí věty jsou ve [FRONTA.md](FRONTA.md).

| # | Model | Co | Stav |
|---|---|---|---|
| F4 | Fable | Kategorie NEW: návrh a náhled | ✅ hotovo, čeká na B10 |
| F5 | Fable | Přesun knihovny do jiné složky | ✅ hotovo, zkouší se v A23 |
| O22 | Opus | Chyba redscriptu: čtení logu a viník | ✅ zkontrolováno |
| O23 | Opus | Chyba redscriptu: náhled okna a texty | ✅ hotovo, čeká na B11 |
| O24 | Opus | Chyba redscriptu: okno v aplikaci | ☐ čeká na B11 |
| O25 | Opus | Import load listu: fronta stránek pro účty zdarma | ✅ zkontrolováno, zkouší se v A24 |
| O26 | Opus | Opravy z revize: počítadlo „pracuji“, jeden klient HTTP | ✅ zkontrolováno |
| S1 | Sonnet | Texty: „mod(s)“, „selected“ a „checked“ | ✅ zkontrolováno, zkouší se v A25 |

## Hotovo

Starší řádky jsou v [archiv/UKOLY-hotovo.md](archiv/UKOLY-hotovo.md).

| Kdy | Co |
|---|---|
| 23. 11. | Čtvrtá dávka vyrobena a sloučena do `main`: návrh kategorie NEW (B10), přesun knihovny (A23), rozbor chyby redscriptu bez okna a náhled okna (B11), fronta stránek pro účty zdarma u IMPORT ORDER (A24), počítadlo „pracuji“ a sdílený klient HTTP, texty (A25). Zbývá O24 po B11. |
| 23. 11. | A21 schváleno: úvodní stránka webu se dvěma edicemi a oddíl Updating AFTERLIFE v návodu. D3 hotovo: Offline zip 1.0.0-beta.1 a nový popis jsou na Nexus Mods. |
| 23. 11. | Opraveno v `main` (`2317e5b`): volitelný požadavek z Nexusu se pozná podle slova „optional“ kdekoli v poznámce, RedFileSystem už nehlásí „missing: RedData“. Ve hře neověřeno, přijde s dalším sestavením. |
| 23. 11. | **1.0.0-beta.1 vydána** (C8, C11): sloučeno vše včetně lišty se zaškrtnutými mody a nabídky LIST & ORDER, tag `v1.0.0-beta.1`, vydání má instalátor a zip plné edice a Offline zip. Třetí dávka fronty je uzavřená a v archivu. |
| 23. 11. | A18 (lišta APPLY při zaškrtnutých modech), A19 (nabídka LIST & ORDER) a A20 (instalátor) zkontrolovány a schváleny. Instalátor ani odinstalátor nemažou knihovnu ani nastavení; ověřeno i zkušební instalací a odinstalací. |
| 23. 11. | A15 (dialog při vypnutí modu), A16 (export a import load listu) a A17 (odkaz na web v patičce, doplněný rychlý návod) schváleny. A15 a A16 jsou v `main`, A17 ve větvi vydání. |
| 23. 11. | O20, O18 a O19 hotové ve větvi `beta1-release`: workflow vyrábí instalátor a zip plné edice a Offline zip; README, návod, FAQ a web říkají, co se kde stahuje; nový oddíl Updating AFTERLIFE. Nainstalovaná kopie nenabízí knihovnu ve vlastní složce. Zkouší se v A20 a A21. |
| 23. 11. | D2: doména afterlife.silentbob.cz je připojená a web na ní odpovídá (u některých poskytovatelů se adresa ještě šíří). |
| 23. 11. | B9 schváleno: pod seznamem zůstává REFRESH, CHECK UPDATES a UPDATE ALL, čtyři občasná tlačítka jsou v nabídce LIST & ORDER (náhled `docs/previews/tools-menu.html`). V kódu, zkouší se v A19. |
| 23. 11. | B8 schváleno: při zaškrtnutí modů se vymění jen řada malých tlačítek, stavový řádek a APPLY zůstávají (náhled `docs/previews/bulk-bar.html`). V kódu (O21), zkouší se v A18. |
| 23. 11. | Web má adresu afterlife.silentbob.cz: aplikace (patička, About, rychlý návod), README, návod a changelog odkazují jen na ni. |
| 23. 11. | Rozhodnuto: na GitHubu se vydává plná edice (instalátor a zip) a vedle ní Offline zip, který jde na Nexus Mods; na Nexusu zůstává jen Offline. Instalátor jen pro plnou edici. |
| 23. 11. | C10: web s návodem běží na Cloudflare Pages a obnovuje se po každé změně v `main`. |
| 23. 11. | O15 (export a import load listu) a O17 (nový dialog při vypnutí modu) hotové a zkontrolované; druhá dávka fronty je uzavřená a v archivu. |
| 23. 11. | Vyzkoušeno na pracovní verzi `1c1256e`: A2, A7, A8, A13 (volné pořadí modů) a A14 (fialová tlačítka) v pořádku. A12 (web a dokumentace): bez výhrad. |
| 23. 11. | Schváleno: B4 (náhled volného pořadí), B5 (texty dialogu při vypnutí modu) a B6 (formát load listu). |

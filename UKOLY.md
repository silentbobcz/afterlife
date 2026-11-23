# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 23. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A18 hotovo“ nebo „C11 první možnost“) v kterémkoli vláknu.
Další volná čísla: A23 B12 C12 D5 · balíky S2 O27 F6

**Stav (23. 11.):** verze **1.0.0-beta.1 je vydaná** na GitHubu (<https://github.com/silentbobcz/afterlife/releases/tag/v1.0.0-beta.1>): instalátor a zip plné edice a Offline zip, každý se `.sha256`. Všechno je v `main`, žádná pracovní větev nezůstala.

**Fronta modelů:** [FRONTA.md](FRONTA.md) (`D:/_AI/afterlife/repo/FRONTA.md`), čtvrtá dávka z 23. 11. Do nového vlákna na modelu Fable stačí napsat: „Přečti `D:/_AI/afterlife/repo/FRONTA.md` a vyrob vše, co nečeká.“ Oba náměty níže jsou zadáním balíků F4 (kategorie NEW) a O22 až O24 (chyba redscriptu); dřív odložené náměty (přesun knihovny, texty, fronta stránek pro účty zdarma) jsou balíky F5, S1 a O25.

**Námět (23. 11., od uživatele): analýza chyby redscriptu.** Spadne-li překlad redscriptu, přestanou fungovat **všechny** skriptové mody, ale AFTERLIFE o tom neví a uživatel vidí jen poruchu nějakého jiného modu. Chyba je přitom čitelná: `<hra>/r6/logs/redscript_rCURRENT.log` končí oddílem `REDScript compilation has failed` a jmenovitým seznamem „caused by“ modů. Námět: po spuštění hry (nebo tlačítkem ANALYZE REDSCRIPT ERROR) log přečíst, viníka najít v knihovně a nabídnout vypnutí; co model nerozluští sám, vypsat jako hotový dotaz pro AI, který si uživatel zkopíruje. Nedělat to záplatou v `FrameworkCatalog.InferFromContent` (pravidlo na jeden typ v textu skriptu) — to řeší jeden mod, ne třídu problému.

**Námět (23. 11., od uživatele): kategorie NEW pro nově nainstalované mody.** Dnes nový mod padá na konec volného bloku MODS a mezi stovkami modů se ztratí. Námět: každý nově nainstalovaný mod spadne do kategorie NEW, která je ve výchozím stavu úplně nahoře; uživatel ji může posunout jako jinou kategorii a mody z ní přetahuje, kam patří. Prázdná NEW se schová, pozici si pamatuje a s dalším novým modem se objeví na stejném místě (rozhodnuto 23. 11.). Před návrhem ověřit: jak NEW ovlivní pořadí načítání (kategorie nese pořadí, NEW nahoře nesmí potichu přeskládat načítání), co s mody z importu load listu, aktualizací existujícího modu (ta do NEW nepatří) a s instalací více modů najednou (kolekce).

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A22 | Volitelný požadavek z Nexusu (oprava `2317e5b`) | Příští zkušební sestavení plné edice, mod RedFileSystem v seznamu | 1. Otevři AFTERLIFE s klíčem Nexusu. 2. Najdi RedFileSystem a podívej se na jeho požadavky. 3. Nesmí hlásit „missing: RedData“; napiš, co ukazuje. | ☐ čeká na sestavení |

## B. Promyslet a rozhodnout

| # | Co | Kde to najdu | Co mám rozhodnout | Stav |
|---|---|---|---|---|
| B10 | Kategorie NEW: návrh a náhled | vznikne v balíku F4 (`docs/previews/new-category.html`, dvojklik) | doplní F4 | ☐ rezervováno 23. 11., čeká na F4 |
| B11 | Chyba redscriptu: náhled okna a texty | vznikne v balíku O23 (`docs/previews/redscript-error.html`, dvojklik) | doplní O23 | ☐ rezervováno 23. 11., čeká na O23 |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |
| D4 | Log s chybou redscriptu (nepovinné, zpřesní balík O22) | `<složka hry>/r6/logs/redscript_rCURRENT.log` z doby, kdy skripty nefungovaly (hra ho při dalším spuštění přepíše). Vlož text do vlákna, které frontu vyrábí; cesty se jménem uživatele se před uložením do repozitáře odstraní. | ☐ čeká |

## Fronty modelů

Zadání a spouštěcí věty jsou ve [FRONTA.md](FRONTA.md).

| # | Model | Co | Stav |
|---|---|---|---|
| F4 | Fable | Kategorie NEW: návrh a náhled | ☐ čeká |
| F5 | Fable | Přesun knihovny do jiné složky | ☐ čeká |
| O22 | Opus | Chyba redscriptu: čtení logu a viník | ☐ čeká |
| O23 | Opus | Chyba redscriptu: náhled okna a texty | ☐ čeká |
| O24 | Opus | Chyba redscriptu: okno v aplikaci | ☐ čeká na O22 a B11 |
| O25 | Opus | Import load listu: fronta stránek pro účty zdarma | ☐ čeká |
| O26 | Opus | Opravy z revize: počítadlo „pracuji“, jeden klient HTTP | ☐ čeká |
| S1 | Sonnet | Texty: „mod(s)“, „selected“ a „checked“ | ☐ čeká na O24 až O26 |

## Hotovo

Starší řádky jsou v [archiv/UKOLY-hotovo.md](archiv/UKOLY-hotovo.md).

| Kdy | Co |
|---|---|
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

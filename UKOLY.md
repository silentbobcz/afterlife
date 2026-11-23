# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 24. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A18 hotovo“ nebo „C11 první možnost“) v kterémkoli vláknu.
Další volná čísla: A29 B12 C12 D7 · balíky S2 O29 F6

**Stav (24. 11.):** verze **1.0.0-beta.2 je vydaná** na GitHubu (<https://github.com/silentbobcz/afterlife/releases/tag/v1.0.0-beta.2>): instalátor a zip plné edice a Offline zip, každý se `.sha256`. Na Nexus Mods je zatím beta.1 (D6). Všechno je v `main`, žádná pracovní větev nezůstala.

**Fronta modelů:** [FRONTA.md](FRONTA.md) (`D:/_AI/afterlife/repo/FRONTA.md`), 5. dávka: O28 (odkaz nxm otevíral další okna) je hotový a v `main`, čeká na vyzkoušení A28. Čtvrtá dávka je v `archiv/FRONTA-4-davka.md`. Napiš „sestavení“, až budeš chtít zkušební verzi k A22 až A28. Odložené: z revize S5 (úklid osiřelých složek v knihovně), E10 (přesun do Removed přejmenováním) a rozdělení dvou dlouhých metod; čtyři hlášky s „(s)“ v souborech nasazení; D4 (skutečný log redscriptu) by zpřesnil rozbor chyby.

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A24 | Import load listu s účtem zdarma | Zkušební sestavení plné edice → LIST & ORDER → IMPORT ORDER | Jen pokud máš po ruce účet Nexus bez Premium: 1. Načti load list, ve kterém chybí mod se souborem z Nexusu. 2. Klikni „Get from Nexus, page by page“ a na stránce Mod Manager Download. 3. Po posledním souboru LOAD THE LIST AGAIN. Napiš, kde se to zadrhlo. | ☐ čeká na sestavení |
| A28 | Jedno okno při Mod Manager Download (O28) | Zkušební sestavení plné edice (`AFTERLIFE-full-test`) a Nexus Mods | 1. Spusť AFTERLIFE normálně a u tří různých modů rychle za sebou klikni na Mod Manager Download: má zůstat jedno okno a přijít tři stažení. 2. AFTERLIFE zavři, spusť ho pravým tlačítkem „Spustit jako správce“ a zopakuj to. Napiš, jestli se otevřelo druhé okno nebo se objevilo hlášení „AFTERLIFE is already running…“. | ☐ čeká na sestavení |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D6 | Nahrát Offline zip 1.0.0-beta.2 na Nexus Mods | `AFTERLIFE-Offline-1.0.0-beta.2-win-x64.zip` z vydání na GitHubu (<https://github.com/silentbobcz/afterlife/releases/tag/v1.0.0-beta.2>); text změn pro Nexus jsi dostal ve vlákně, které vydání dělalo. | ☐ čeká |
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |
| D4 | Log s chybou redscriptu (nepovinné, zpřesní balík O22) | `<složka hry>/r6/logs/redscript_rCURRENT.log` z doby, kdy skripty nefungovaly (hra ho při dalším spuštění přepíše). Vlož text do vlákna, které frontu vyrábí; cesty se jménem uživatele se před uložením do repozitáře odstraní. | ☐ čeká |
| D5 | Odpověď pisatele o „30 oknech“ (nepovinné, zpřesní O28) | Nexus Mods, komentář o oknech při stahování: běží AFTERLIFE jako správce? A poslední záznam z `%LOCALAPPDATA%\AFTERLIFE\logs\error-….log`, pokud existuje. Vlož do vlákna s O28. | ☐ čeká |


## Hotovo

Starší řádky jsou v [archiv/UKOLY-hotovo.md](archiv/UKOLY-hotovo.md).

| Kdy | Co |
|---|---|
| 24. 11. | **1.0.0-beta.2 vydána**: tag `v1.0.0-beta.2`, vydání má instalátor a zip plné edice a Offline zip. Proti beta.1 přibyl blok NEW, okno chyby redscriptu, přesun knihovny, jedno okno při Mod Manager Download (O28, na Windows nevyzkoušeno, A28) a oprava kontroly aktualizací při startu (prázdná odpověď Nexusu na požadavky modu ji dřív zastavila). |
| 23. 11. | A23 uzavřeno bez zkoušky (rozhodnutí uživatele): přesun knihovny (MOVE… v Nastavení) zůstává v aplikaci, kryjí ho testy, v okně a na skutečné knihovně ověřen není. |
| 23. 11. | Vyzkoušeno na zkušebním sestavení `380579e`: A22 (RedFileSystem už nehlásí chybějící RedData), A25 (nové popisky a hlášky), A26 (okno chyby redscriptu) a A27 (blok NEW) v pořádku. |
| 23. 11. | Čtvrtá dávka uzavřena a celá v `main`: přibylo okno chyby redscriptu (O24, zkouší se v A26) a blok NEW (O27, zkouší se v A27). Fronta je v archivu. |
| 23. 11. | B10 rozhodnuto: blok NEW je ve výchozím stavu nahoře, jde přesunout mezi bloky (nad a pod kolekce a MODS) a mod v něm se načítá tam, kde NEW stojí; žádný vypínač v Nastavení (kdo NEW nechce, použije MOVE ALL TO MODS). Kód je balík O27. |
| 23. 11. | B11 schváleno podle doporučení: tlačítko ANALYZE REDSCRIPT ERROR je první řádek nabídky LIST & ORDER; log se čte sám po návratu ze hry a při startu, jen když je novější než naposledy zpracovaný. Kód je balík O24. |
| 23. 11. | Čtvrtá dávka vyrobena a sloučena do `main`: návrh kategorie NEW (B10), přesun knihovny (A23), rozbor chyby redscriptu bez okna a náhled okna (B11), fronta stránek pro účty zdarma u IMPORT ORDER (A24), počítadlo „pracuji“ a sdílený klient HTTP, texty (A25). Zbývá O24 po B11. |
| 23. 11. | A21 schváleno: úvodní stránka webu se dvěma edicemi a oddíl Updating AFTERLIFE v návodu. D3 hotovo: Offline zip 1.0.0-beta.1 a nový popis jsou na Nexus Mods. |
| 23. 11. | Opraveno v `main` (`2317e5b`): volitelný požadavek z Nexusu se pozná podle slova „optional“ kdekoli v poznámce, RedFileSystem už nehlásí „missing: RedData“. Ve hře neověřeno, přijde s dalším sestavením. |
| 23. 11. | **1.0.0-beta.1 vydána** (C8, C11): sloučeno vše včetně lišty se zaškrtnutými mody a nabídky LIST & ORDER, tag `v1.0.0-beta.1`, vydání má instalátor a zip plné edice a Offline zip. Třetí dávka fronty je uzavřená a v archivu. |
| 23. 11. | A18 (lišta APPLY při zaškrtnutých modech), A19 (nabídka LIST & ORDER) a A20 (instalátor) zkontrolovány a schváleny. Instalátor ani odinstalátor nemažou knihovnu ani nastavení; ověřeno i zkušební instalací a odinstalací. |
| 23. 11. | A15 (dialog při vypnutí modu), A16 (export a import load listu) a A17 (odkaz na web v patičce, doplněný rychlý návod) schváleny. A15 a A16 jsou v `main`, A17 ve větvi vydání. |
| 23. 11. | O20, O18 a O19 hotové ve větvi `beta1-release`: workflow vyrábí instalátor a zip plné edice a Offline zip; README, návod, FAQ a web říkají, co se kde stahuje; nový oddíl Updating AFTERLIFE. Nainstalovaná kopie nenabízí knihovnu ve vlastní složce. Zkouší se v A20 a A21. |

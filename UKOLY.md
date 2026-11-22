# AFTERLIFE: co čeká na tebe

Naposledy upraveno: 23. 11. 2026. Když něco vyřídíš, napiš číslo úkolu (třeba „A1 hotovo“ nebo „C4 první možnost“) v kterémkoli vláknu.

**Fronta modelů:** [FRONTA.md](FRONTA.md). Nejnovější stav je v `main`. Druhá dávka je uzavřená a v archivu; pro modely teď nic nečeká, další balíky vzniknou z toho, co najdeš při zkoušení.

Poslední pracovní verze plné edice je `AFTERLIFE-test-1c1256e-win-x64.zip` ve složce `zkusebni` (22. 11.). Neobsahuje ještě load list (A16) ani nový dialog při vypnutí modu (A15); na ty je potřeba nová pracovní verze, řekni si o ni. Pracuje s tvou skutečnou knihovnou a hrou, stejně jako nainstalovaná verze.

## A. Vyzkoušet

| # | Co | Kde to najdu | Co mám udělat | Stav |
|---|---|---|---|---|
| A15 | Nový dialog při vypnutí modu (O17) | Další pracovní verze; seznam modů | 1. Vypni mod, který potřebuje jiný zapnutý mod: nadpis „SWITCH OFF ‹JMÉNO›?“, část WILL STOP WORKING s větou a zaškrtnutým „Switch off this mod too“. 2. Najdi mod A, který potřebuje knihovnu L, a mod B, který potřebuje L i A. Vypni A: L je v části NOT NEEDED ANY MORE; odškrtni vypnutí B a L se má hned přesunout do STAYS ON. 3. Vypni kolekci, která používala mod mimo sebe: druhý dialog „SWITCH OFF UNUSED MODS?“ má prázdné zaškrtnutí a větu, že Cancel nechá kolekci zapnutou. Neověřeno: nic z toho neběželo v okně. | ☐ čeká na sestavení |
| A16 | Export a import load listu (O15) | Další pracovní verze; tlačítka EXPORT ORDER a IMPORT ORDER vedle EXPORT LIST pod seznamem | 1. EXPORT ORDER: ulož soubor a otevři ho v Poznámkovém bloku, je čitelný? 2. V souboru přehoď dva řádky, jeden mod přepni z `+` na `-`, přidej řádek `@category Zkouska` s jedním modem pod ním a dej IMPORT ORDER: náhled má přesně říct, co se změní; po LOAD se seznam změní a APPLY ukáže změnu pořadí. 3. Naimportuj týž soubor podruhé: má říct, že se nic nemění. V knihovně ve složce `exports` má být záloha původního pořadí. Neověřeno: nic z toho neběželo v okně; stahování chybějících modů u Premium a přidání kolekce z náhledu. | ☐ čeká na sestavení |

## C. Rozhodnout (stačí odpověď)

Doporučená možnost je vždy první.

| # | Co | O co jde | Možnosti | Stav |
|---|---|---|---|---|
| C10 | Zveřejnění webu s návodem na Cloudflare | Web se staví z repozitáře (složka `site`), ale projekt v Cloudflare Pages musíš založit ty ve svém účtu. Postup a hodnoty jsou v `site/README.md`. Potom se sám obnoví po každé změně v `main`. | 1. Založit projekt v Cloudflare Pages podle `site/README.md` a napsat adresu; doplní se do README, na Nexus a do aplikace. 2. Zatím nezveřejňovat, návod číst jen na GitHubu. | ☐ čeká |
| C8 | Tag a vydání 1.0.0-beta.1 | Kód je v `main` s verzí 1.0.0-beta.1, od 22. 11. i s nastavením v záložkách (A10). Vydání na GitHubu spustí až tag, a ten zakládáš ty. Vydá se edice Offline; plnou edici máš jako zkušební zip. | 1. Nejdřív projít A2 až A10 na zkušebním sestavení, pak založit tag `v1.0.0-beta.1` na GitHubu (Releases → Draft a new release → tag `v1.0.0-beta.1`, cíl `main`). 2. Založit tag hned. | ☐ čeká |

## D. Dodat

| # | Co | Kde to vezmu | Stav |
|---|---|---|---|
| D1 | Odpověď pisatele komentáře | Komentáře u AFTERLIFE na Nexus Mods: snímek maximalizovaného okna, rozlišení a měřítko, příklad vypnutého modu, názvy ZIPů, pohledy, které chce. Vlož ji do vlákna, které na AFTERLIFE pracuje. | ☐ čeká |

## Hotovo

| Kdy | Co |
|---|---|
| 23. 11. | O15 (export a import load listu) a O17 (nový dialog při vypnutí modu) hotové a zkontrolované, 375 testů; zkouší se v A16 a A15. Druhá dávka fronty je uzavřená a v archivu. |
| 23. 11. | Vyzkoušeno na pracovní verzi `1c1256e`: A2, A7, A8, A13 (volné pořadí modů) a A14 (fialová tlačítka) v pořádku. A12 (web a dokumentace): bez výhrad teď, úpravy případně později. |
| 23. 11. | Schváleno: B4 (náhled volného pořadí), B5 (nové texty dialogu při vypnutí modu, do kódu jde jako O17) a B6 (formát load listu; u bodů D1 až D7 platí doporučené možnosti A, tlačítka EXPORT ORDER a IMPORT ORDER; do kódu jde jako O15). |
| 22. 11. | B7: vybrána varianta B (fialová) a dlouhý popisek; O16 je v kódu a zkontrolované, zkouší se v A14. O14 a O16 sloučeny do `main`. |
| 22. 11. | O14: návrh formátu load listu hotový (`docs/previews/load-list-format.md`), čeká na B6. |
| 22. 11. | Dva nové nápady zapsané do fronty: export a import load listu (O14 návrh formátu, O15 kód) a jiná barva a popisek tlačítek FILTERS a SORT (O16). |
| 22. 11. | Výroba jedním vláknem: O10 (náhled), O11 (volné pořadí v kódu), O12 (klávesy, LOAD BEFORE) a návrh textů O13 hotové a zkontrolované; sestavení a 335 testů sloučeno do `main`. Kontrola opravila tři věci (text v náhledu, kolizi názvu testů, nepřeložitelnou vsuvku). |
| 22. 11. | C9: nové mody při volném pořadí padají na konec MODS (bez kategorie). Aktualizace drží místo staré verze. |
| 22. 11. | Vyzkoušeno na sestavení `34f6b02`: A4, A5, A6, A9, A10 a A11 v pořádku. A3 v pořádku, ale logika dialogu při vypnutí modu není srozumitelná: texty se přepíšou (balík O13). A2: historie stahování v pořádku, Scan manual mods je krátké (zůstává otevřené). A7: čekal jsi ruční pořadí uvnitř MODS a kategorií; to je O10 až O12. |
| 22. 11. | B3 schváleno, O8 hotové a zkontrolované (bez nálezu): nastavení se záložkami vlevo je v kódu, zkouší se v A10. Instalátor (O9) je zrušený a jeho zkušební výroba vrácená. |
| 22. 11. | Balení: zůstává složka v zipu, k jednomu samorozbalovacímu exe se nevracíme. Samostatný instalátor se dělat nebude (zrušeno 22. 11.). |
| 22. 11. | C3 a C7: vše sloučeno do `main`, verze 1.0.0-beta.1. První dávka fronty je uzavřená a v archivu. Vydání čeká na tvůj tag (C8). |
| 22. 11. | Kontrola náhledu nastavení (O7): odpovídá aplikaci, všechny volby mají místo, texty sedí s tím, co je v kódu. Rozhodnutí je dál tvoje (B3). |
| 22. 11. | Logy: uživatelské jméno, název počítače, složky a tajné části odkazů se před zápisem nahrazují značkami. |
| 21. 11. | O7: náhled nastavení se záložkami vlevo je hotový, posuzuješ ho v B3. |
| 21. 11. | Pojistka limitu Nexusu hotová: aplikace zná limit, zastaví se před ním, počká a pokračuje sama; nastavení varuje před příliš častou plnou kontrolou. 313 testů. |
| 21. 11. | F3 hotové: kontrola aktualizací bez stropu 60, plné čtení každého modu jednou za N dní, volby v nastavení. 309 testů, překlad obou edic. |
| 21. 11. | C6: nastavení se přestaví celé, se záložkami vlevo. Nejdřív náhled (O7, B3), pak kód (O8). Volby z F3 jsou do té doby v prostém oddílu UPDATES. |
| 21. 11. | C4: kontrola aktualizací bude bez stropu 60, zapnuté a core mody první, tlačítko přes rozdíly, přepínač kontroly při startu a plné přečtení každého modu jednou za N dní (N v nastavení). Zadání je v balíku F3. |
| 21. 11. | A1: v maximalizovaném okně alpha.9 hlavní seznam vyplní šířku i výšku, chyba v něm není. Pisatelovo „nezvětší se“ nejspíš míří na překryvná okna (opravuje O1); potvrdí to jeho snímek (D1). |
| 21. 11. | Fronta a úkoly jsou v repozitáři (`FRONTA.md`, `UKOLY.md`), aby je našla všechna vlákna i počítač doma. Dokumenty v Claude Docs už se nevedou. |
| 21. 11. | F2 hotové: řazení jen pro zobrazení a přepínač virtualizace. Kontrola O1, O2, O4 a O6 bez nálezu. 303 testů, překlad obou edic. |
| 21. 11. | F1 a O6 hotové: propojení modů ze ZIPů s Nexusem, z detailu modu i z nastavení. |
| 21. 11. | B1 a B2: zůstává dnešní vzhled s panelem filtrů (O4). Přestavba celého okna (W1, W2, W3) a styl MUTED jsou odložené jako alternativy. |
| 21. 11. | C2: nic se nesestavuje, dokud neřekneš; testy si vlákna pouštějí sama. |
| 21. 11. | C1 a C5: do pracovních větví na GitHubu se zapisuje průběžně. |
| 21. 11. | Rozbor komentáře z Nexus Mods proti kódu a anglické otázky pro pisatele. |
